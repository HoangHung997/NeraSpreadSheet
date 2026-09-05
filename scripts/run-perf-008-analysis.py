"""PERF-008 baseline-only calibration and paired analysis. Standard library only."""

import hashlib
import json
import math
import random
import statistics as stats
import sys
from pathlib import Path

RULE = "perf008-v3:6-aa;12-abba;mad3;floor5pct;noise10pct;bootstrap95;alloc1pct-or1B;toggle4096-128;completion32768-1024;cache262144-4096"
ENVIRONMENT_KEYS = ("runtime", "framework", "os", "architecture", "processors", "configuration", "tieredCompilation", "serverGc")


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def median(values):
    return stats.median(values)


def mad(values):
    center = median(values)
    return 1.4826 * median([abs(value - center) for value in values])


def quantile(values, fraction):
    ordered = sorted(values)
    position = (len(ordered) - 1) * fraction
    left = int(position)
    return ordered[left] + (ordered[min(left + 1, len(ordered) - 1)] - ordered[left]) * (position - left)


def interval(values):
    rng = random.Random(8008)
    boot = [median(rng.choices(values, k=len(values))) for _ in range(10_000)]
    return quantile(boot, .05), quantile(boot, .95)


def load_pairs(root, phase, count):
    pairs = []
    expected_environment = None
    for index in range(count):
        pair = []
        for side in ("baseline", "candidate"):
            document = json.loads((root / f"{phase}-{index:02}-{side}.json").read_text(encoding="utf-8-sig"))
            assert document["schema"] == "perf008-worker-v1" and document["mode"] == "measure"
            assert document["configuration"] == "Release" and document["tieredCompilation"] == "0"
            assert not document["serverGc"]
            assert len(document["measurements"]) == 11, "Missing workload"
            environment = {key: document[key] for key in ENVIRONMENT_KEYS}
            if expected_environment is None:
                expected_environment = environment
            assert environment == expected_environment, "Worker environment changed"
            pair.append({item["Name"]: item for item in document["measurements"]})
            assert len(pair[-1]) == 11, "Duplicate workload name"
        assert pair[0].keys() == pair[1].keys(), "Workload mismatch"
        pairs.append(pair)
    return pairs


def fingerprint(measurement):
    return [measurement[key] for key in ("InputHash", "OutputHash", "Operations", "Warmup")]


def series(pairs, name, field):
    return [[pair[side][name][field] for pair in pairs] for side in (0, 1)]


def calibrate(root):
    pairs = load_pairs(root, "calibration", 6)
    budgets = {}
    for name in pairs[0][0]:
        expected = fingerprint(pairs[0][0][name])
        assert all(fingerprint(side[name]) == expected for pair in pairs for side in pair), "Unstable baseline fingerprint"
        before, after = series(pairs, name, "MicrosecondsPerOperation")
        log_ratios = [math.log(b / a) for a, b in zip(before, after)]
        values = before + after
        dispersion = mad(values) / median(values)
        pair_noise = median([abs(value) for value in log_ratios])
        limit = max(.05, math.expm1(3 * mad(log_ratios)))
        allocations = sum(series(pairs, name, "BytesPerOperation"), [])
        budgets[name] = dict(fingerprint=expected, medianMicroseconds=median(values),
                             p95BatchMicroseconds=quantile(values, .95), p99BatchMicroseconds=quantile(values, .99),
                             relativeMad=dispersion, pairedNoise=pair_noise,
                             latencyTolerance=limit, allocationToleranceBytes=max(1, .01 * median(allocations)),
                             medianBytes=median(allocations),
                             quality="stable" if max(dispersion, pair_noise) <= .10 else "inconclusive-noise")
    files = {path.name: digest(path) for path in sorted(root.glob("calibration-*.json"))}
    first = json.loads((root / "calibration-00-baseline.json").read_text(encoding="utf-8-sig"))
    return dict(rule=RULE, calibrationFiles=files, budgets=budgets,
                environment={key: first[key] for key in ENVIRONMENT_KEYS})


def evaluate(root, budget):
    assert budget["rule"] == RULE
    assert all(digest(root / name) == hash_value for name, hash_value in budget["calibrationFiles"].items()), "Calibration changed"
    pairs = load_pairs(root, "paired", 12)
    first = json.loads((root / "paired-00-baseline.json").read_text(encoding="utf-8-sig"))
    assert {key: first[key] for key in ENVIRONMENT_KEYS} == budget["environment"], "Environment changed after calibration"
    assert pairs[0][0].keys() == budget["budgets"].keys(), "Budget workload mismatch"
    results = {}
    for name, limits in budget["budgets"].items():
        assert all(fingerprint(side[name]) == limits["fingerprint"] for pair in pairs for side in pair), "Paired input/output fingerprint mismatch"
        before, after = series(pairs, name, "MicrosecondsPerOperation")
        ratios = [math.log(b / a) for a, b in zip(before, after)]
        low, high = interval(ratios)
        allocation_before, allocation_after = series(pairs, name, "BytesPerOperation")
        alloc_deltas = [b - a for a, b in zip(allocation_before, allocation_after)]
        alloc_low, alloc_high = interval(alloc_deltas)
        boundary = math.log1p(limits["latencyTolerance"])
        quality = limits["quality"] == "stable" and mad(before) / median(before) <= .10
        state = "pass" if quality and high <= boundary and alloc_high <= limits["allocationToleranceBytes"] else "inconclusive"
        if low > boundary or alloc_low > limits["allocationToleranceBytes"]:
            state = "regression"
        results[name] = dict(status=state, baselineMedianMicroseconds=median(before), candidateMedianMicroseconds=median(after),
                             medianPairedRatio=math.exp(median(ratios)), ratioInterval95OneSided=[math.exp(low), math.exp(high)],
                             latencyTolerance=limits["latencyTolerance"], baselineBytes=median(allocation_before),
                             candidateBytes=median(allocation_after), allocationDeltaInterval95OneSided=[alloc_low, alloc_high],
                             candidateP95BatchMicroseconds=quantile(after, .95), candidateP99BatchMicroseconds=quantile(after, .99),
                             baselineQuality=limits["quality"])
    states = {item["status"] for item in results.values()}
    overall = "regression" if "regression" in states else "inconclusive" if "inconclusive" in states else "pass"
    return dict(rule=RULE, status=overall, results=results,
                limitation="CPU batch averages; percentiles are across batches, not individual input events or displayed frames. P3 combined rerun required.")


def self_test():
    import tempfile
    import unittest

    class PERF008AnalysisTests(unittest.TestCase):
        def testRegressionNoiseAndFingerprintGates(self):
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                def write(phase, count, ratio=1., noise=False, tamper=False):
                    for index in range(count):
                        for side in ("baseline", "candidate"):
                            scale = (1.5 if index % 2 else .5) if noise else 1.
                            item = dict(Name="metric", InputHash="bad" if tamper and side == "candidate" else "input", OutputHash="output",
                                        Operations=100, Warmup=20, MicrosecondsPerOperation=100 * scale * (ratio if side == "candidate" else 1), BytesPerOperation=200)
                            data = dict(schema="perf008-worker-v1", mode="measure", runtime="test", framework="test", os="test", architecture="test", processors=2,
                                        configuration="Release", tieredCompilation="0", serverGc=False,
                                        measurements=[dict(item, Name=f"metric{n}") for n in range(11)])
                            (root / f"{phase}-{index:02}-{side}.json").write_text(json.dumps(data))
                write("calibration", 6)
                budget = calibrate(root)
                write("paired", 12)
                self.assertEqual("pass", evaluate(root, budget)["status"])
                write("paired", 12, ratio=1.2)
                self.assertEqual("regression", evaluate(root, budget)["status"])
                write("paired", 12, tamper=True)
                self.assertRaises(AssertionError, evaluate, root, budget)
                write("paired", 12)
                changed_path = root / "paired-11-candidate.json"
                changed = json.loads(changed_path.read_text())
                changed["runtime"] = "different-runtime"
                changed_path.write_text(json.dumps(changed))
                self.assertRaises(AssertionError, evaluate, root, budget)
                write("calibration", 6, noise=True)
                budget = calibrate(root)
                write("paired", 12)
                self.assertEqual("inconclusive", evaluate(root, budget)["status"])
                next(root.glob("calibration-*.json")).write_text("{}")
                self.assertRaises(AssertionError, evaluate, root, budget)

    result = unittest.TextTestRunner().run(unittest.defaultTestLoader.loadTestsFromTestCase(PERF008AnalysisTests))
    return 0 if result.wasSuccessful() else 1


if __name__ == "__main__":
    if sys.argv[1] == "self-test":
        sys.exit(self_test())
    mode, directory = sys.argv[1:3]
    root = Path(directory)
    if mode == "calibrate":
        result = calibrate(root)
        target = root / "budget.json"
        assert not target.exists(), "A frozen budget cannot be overwritten"
    else:
        result = evaluate(root, json.loads((root / "budget.json").read_text()))
        target = root / "comparison.json"
    target.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, indent=2))
    if mode != "calibrate":
        sys.exit(0 if result["status"] == "pass" else 2)
