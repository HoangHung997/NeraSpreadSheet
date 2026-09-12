#!/usr/bin/env python3
"""Archive and remove all integrated work branches after an exact-source main Check out passes."""
import argparse
import json
from pathlib import Path
import re
import subprocess

REPOSITORY = "HoangHung997/NeraSpreadSheet"
PREFIX = "archive/consolidation-20260912/"
COMPLETE = PREFIX + "complete"
ALLOWED_POST_VALIDATION_PATHS = {
    "Check out/README.md",
}


def run(*args, check=True):
    completed = subprocess.run(args, text=True, capture_output=True)
    if check and completed.returncode:
        raise RuntimeError(
            f"command failed ({completed.returncode}): {' '.join(args)}\n"
            f"stdout:\n{completed.stdout}\nstderr:\n{completed.stderr}")
    return completed.stdout.strip()


def gh(*args):
    return run("gh", *args)


def remote_refs(kind):
    output = run("git", "ls-remote", "--" + kind, "origin")
    result = {}
    for line in output.splitlines():
        if not line:
            continue
        sha, ref = line.split("\t", 1)
        if not ref.endswith("^{}"):
            result[ref] = sha
    return result


def is_ancestor(ancestor, descendant):
    completed = subprocess.run(
        ["git", "merge-base", "--is-ancestor", ancestor, descendant],
        text=True,
        capture_output=True,
    )
    if completed.returncode not in (0, 1):
        raise RuntimeError(completed.stderr)
    return completed.returncode == 0


def flatten_pages(raw):
    pages = json.loads(raw)
    if isinstance(pages, list) and pages and isinstance(pages[0], list):
        return [item for page in pages for item in page]
    return pages if isinstance(pages, list) else []


def verify_release(repository, validated_sha, run_id):
    tag = f"check-out-{validated_sha[:12]}-{run_id}"
    release = json.loads(gh("api", f"repos/{repository}/releases/tags/{tag}"))
    if not release.get("prerelease"):
        raise ValueError("Exact-source Check out release is not a prerelease engineering build")
    target = release.get("target_commitish")
    if target not in (validated_sha, "main"):
        raise ValueError(f"Unexpected Check out release target: {target!r}")
    body = release.get("body") or ""
    if validated_sha not in body:
        raise ValueError("Exact validated SHA is missing from immutable release notes")
    required = {
        "CHECKOUT.json",
        "SHA256SUMS.txt",
        "Nera-Avalonia-win-x64.zip",
        "Nera-Avalonia-linux-x64.zip",
        "Nera-Avalonia-osx-arm64.zip",
        "Nera-SDK-packages.zip",
    }
    present = {asset["name"] for asset in release.get("assets", []) if asset.get("size", 0) > 0}
    missing = sorted(required - present)
    if missing:
        raise ValueError("Incomplete immutable Check out release: " + ", ".join(missing))
    return tag


def verify_main_after_registry(validated_sha, main_tip):
    if not is_ancestor(validated_sha, main_tip):
        raise ValueError("Remote main is not a descendant of the validated Check out source")
    changed = [
        path for path in run("git", "diff", "--name-only", validated_sha, main_tip).splitlines()
        if path
    ]
    unexpected = [
        path for path in changed
        if path not in ALLOWED_POST_VALIDATION_PATHS and not path.startswith("Check out/Packages/")
    ]
    if unexpected:
        raise ValueError(
            "Remote main advanced after validation outside Check out registry files: "
            + ", ".join(unexpected)
        )
    return changed


def cleanup(validated_sha, repository, run_id, apply):
    if repository != REPOSITORY:
        raise ValueError("Unexpected repository")
    if not re.fullmatch(r"[0-9a-f]{40}", validated_sha):
        raise ValueError("A full exact validated SHA is required")
    if not re.fullmatch(r"[0-9]+", str(run_id)):
        raise ValueError("A numeric Check out run id is required")

    heads = remote_refs("heads")
    main_ref = "refs/heads/main"
    main_tip = heads.get(main_ref)
    if main_tip is None:
        raise ValueError("Remote main is missing")

    # Bring every candidate tip plus the post-publication main registry commits into
    # the local object database before ancestry checks. This never rewrites a ref.
    run("git", "fetch", "--prune", "origin", "+refs/heads/*:refs/remotes/origin/*")
    changed_after_validation = verify_main_after_registry(validated_sha, main_tip)
    release_tag = verify_release(repository, validated_sha, str(run_id))

    candidates = {
        ref: sha for ref, sha in heads.items()
        if ref != main_ref
    }
    if not candidates:
        report = {
            "schema": "nera.branch-cleanup.20260912.v1",
            "sourceSha": validated_sha,
            "mainTip": main_tip,
            "workflowRunId": int(run_id),
            "releaseTag": release_tag,
            "postValidationPaths": changed_after_validation,
            "archiveRefs": {},
            "deletedRefs": {},
            "closedPullRequests": [],
            "applied": False,
            "remainingBranches": heads,
            "message": "No non-main branches remained.",
        }
        Path("consolidation-result.json").write_text(
            json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print("CONSOLIDATION_NOOP " + json.dumps(report, ensure_ascii=False))
        return

    not_integrated = [
        ref for ref, sha in candidates.items()
        if not is_ancestor(sha, validated_sha)
    ]
    if not_integrated:
        raise ValueError(
            "Refusing to delete work not contained in the validated source: "
            + ", ".join(sorted(not_integrated))
        )

    tags = remote_refs("tags")
    complete_ref = "refs/tags/" + COMPLETE
    if complete_ref in tags:
        raise ValueError("Cleanup completion tag already exists while work branches still remain")

    archives = {
        f"refs/tags/{PREFIX}{index:03d}": sha
        for index, (_, sha) in enumerate(sorted(candidates.items()), start=1)
    }
    archives[f"refs/tags/{PREFIX}validated-source"] = validated_sha
    archives[f"refs/tags/{PREFIX}post-registry-main"] = main_tip
    for ref, sha in archives.items():
        existing = tags.get(ref)
        if existing is not None and existing != sha:
            raise ValueError("Archive tag already points elsewhere: " + ref)

    pulls = flatten_pages(gh(
        "api", "--paginate", "--slurp",
        f"repos/{repository}/pulls?state=open&per_page=100"))
    candidate_names = {ref.removeprefix("refs/heads/") for ref in candidates}
    closing = []
    for pr in pulls:
        head_name = pr.get("head", {}).get("ref")
        base_name = pr.get("base", {}).get("ref")
        if head_name not in candidate_names and base_name not in candidate_names:
            continue
        pr_sha = pr.get("head", {}).get("sha")
        if not pr_sha or not is_ancestor(pr_sha, validated_sha):
            raise ValueError(
                f"Open PR #{pr.get('number')} contains a head not included in validated source")
        closing.append(pr)

    report = {
        "schema": "nera.branch-cleanup.20260912.v1",
        "sourceSha": validated_sha,
        "mainTip": main_tip,
        "workflowRunId": int(run_id),
        "releaseTag": release_tag,
        "postValidationPaths": changed_after_validation,
        "archiveRefs": archives,
        "deletedRefs": candidates,
        "closedPullRequests": [pr["number"] for pr in closing],
        "applied": False,
    }
    print(json.dumps(report, ensure_ascii=False, indent=2))
    if not apply:
        Path("consolidation-result.json").write_text(
            json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        return

    # Lease against the entire branch snapshot. If any writer advances/adds/removes
    # a branch, no deletion occurs under the stale plan.
    if remote_refs("heads") != heads:
        raise ValueError("Remote branches changed before archive")

    additions = [
        f"{sha}:{ref}" for ref, sha in archives.items()
        if ref not in tags
    ]
    if additions:
        run("git", "push", "--atomic", "origin", *additions)

    verified_tags = remote_refs("tags")
    if any(verified_tags.get(ref) != sha for ref, sha in archives.items()):
        raise ValueError("Cannot verify every archive tag; refusing branch deletion")
    if remote_refs("heads") != heads:
        raise ValueError("Remote branches changed after archive")

    note = (
        f"Source của PR đã được hợp nhất vào `main` qua validated Check out source "
        f"`{validated_sha}` (run `{run_id}`). Nhánh làm việc đã được archive trước khi "
        "xóa để repository chỉ còn một nhánh canonical `main`."
    )
    for pr in closing:
        latest = json.loads(gh("api", f"repos/{repository}/pulls/{pr['number']}"))
        latest_sha = latest.get("head", {}).get("sha")
        if latest_sha != pr.get("head", {}).get("sha") or not is_ancestor(latest_sha, validated_sha):
            raise ValueError(f"PR #{pr['number']} advanced before closure")
        gh("api", f"repos/{repository}/issues/{pr['number']}/comments", "-f", "body=" + note)
        gh("api", "--method", "PATCH", f"repos/{repository}/pulls/{pr['number']}", "-f", "state=closed")

    if remote_refs("heads") != heads:
        raise ValueError("A writer changed a branch before deletion")

    leases = [
        f"--force-with-lease={ref}:{sha}" for ref, sha in sorted(candidates.items())
    ]
    deletions = [":" + ref for ref in sorted(candidates)]
    run("git", "push", "--atomic", *leases, "origin", *deletions)

    final_heads = remote_refs("heads")
    if final_heads != {main_ref: main_tip}:
        raise ValueError(
            "Cleanup finished with an unexpected branch set: "
            + json.dumps(final_heads, sort_keys=True))

    run("git", "push", "origin", f"{validated_sha}:{complete_ref}")
    report["applied"] = True
    report["remainingBranches"] = final_heads
    Path("consolidation-result.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("CONSOLIDATION_SUCCESS " + json.dumps({
        "sourceSha": validated_sha,
        "mainTip": main_tip,
        "archived": len(archives),
        "deleted": len(candidates),
        "remaining": 1,
    }, ensure_ascii=False))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--validated-sha", required=True)
    parser.add_argument("--repo", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    cleanup(args.validated_sha, args.repo, args.run_id, args.apply)
