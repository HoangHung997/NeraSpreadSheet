"""Fail closed on incomplete solution graphs or non-Release Nera outputs. Stdlib only."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET


def graph(root):
    root = Path(root).resolve()
    solution = root / 'NeraSpreadSheet.Avalonia.slnx'
    declared = {(root / node.attrib['Path'].replace('\\', '/')).resolve()
                for node in ET.parse(solution).iter('Project')}
    if not declared:
        raise ValueError('The Avalonia solution is empty.')
    visited, pending = set(), list(declared)
    while pending:
        project = pending.pop()
        if project in visited:
            continue
        if not project.is_relative_to(root) or not project.is_file():
            raise ValueError(f'Invalid project path: {project}')
        visited.add(project)
        for node in ET.parse(project).iter('ProjectReference'):
            value = node.attrib.get('Include', '')
            if not value or any(token in value for token in ('$', '*', ';')):
                raise ValueError(f'ProjectReference requires explicit evaluation: {value}')
            pending.append((project.parent / value.replace('\\', '/')).resolve())
    missing = visited - declared
    if missing:
        raise ValueError('Transitive projects missing from solution: ' + ', '.join(
            sorted(str(path.relative_to(root)) for path in missing)))
    return visited


def verify_log(projects, text):
    names = {}
    for project in projects:
        name = ET.parse(project).findtext('.//AssemblyName') or project.stem
        if name in names:
            raise ValueError(f'Duplicate assembly name: {name}')
        names[name] = project
    outputs = {}
    text = re.sub(r'\x1b\[[0-9;]*m', '', text)
    for line in text.splitlines():
        match = re.match(r'^\s*(NeraSpreadSheet[\w.]*)\s+->\s+(.+\.dll)\s*$', line)
        if not match:
            continue
        name, value = match.groups()
        if name not in names:
            raise ValueError(f'Unexpected Nera output: {name}')
        output = Path(value.strip()).resolve()
        expected_root = (names[name].parent / 'bin' / 'Release').resolve()
        if not output.is_relative_to(expected_root) or output.name != name + '.dll':
            raise ValueError(f'Non-Release or foreign output for {name}: {output}')
        if not output.is_file():
            raise ValueError(f'Build output does not exist: {output}')
        outputs[name] = output
    missing = set(names) - set(outputs)
    if missing:
        raise ValueError('Missing build-output evidence: ' + ', '.join(sorted(missing)))
    return outputs


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def verify_copies(outputs, consumer_dir):
    copied = {file.stem: file for file in Path(consumer_dir).glob('NeraSpreadSheet*.dll')}
    if not copied:
        raise ValueError('Consumer output has no Nera assemblies.')
    for name, target in copied.items():
        if name not in outputs or digest(target) != digest(outputs[name]):
            raise ValueError(f'Consumer assembly does not match the Release build: {name}')
    return len(copied)


class GateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.a = self.project('NeraSpreadSheet.A', '../NeraSpreadSheet.B/NeraSpreadSheet.B.csproj')
        self.b = self.project('NeraSpreadSheet.B')
        self.solution([self.a, self.b])

    def project(self, name, reference=None):
        path = self.root / 'src' / name / (name + '.csproj')
        path.parent.mkdir(parents=True)
        body = f'<ItemGroup><ProjectReference Include="{reference}" /></ItemGroup>' if reference else ''
        path.write_text('<Project>' + body + '</Project>', encoding='utf-8')
        return path

    def solution(self, paths):
        body = ''.join(f'<Project Path="{path.relative_to(self.root).as_posix()}" />' for path in paths)
        (self.root / 'NeraSpreadSheet.Avalonia.slnx').write_text('<Solution>' + body + '</Solution>', encoding='utf-8')

    def output(self, project, configuration='Release'):
        path = project.parent / 'bin' / configuration / 'net10.0' / (project.stem + '.dll')
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(project.stem.encode())
        return path

    def testCompleteGraphAccepted(self):
        self.assertEqual({self.a, self.b}, graph(self.root))

    def testMissingTransitiveProjectRejected(self):
        self.solution([self.a])
        with self.assertRaisesRegex(ValueError, 'missing from solution'):
            graph(self.root)

    def testReleaseOutputsAccepted(self):
        text = '\n'.join(f'{p.stem} -> {self.output(p)}' for p in (self.a, self.b))
        self.assertEqual(2, len(verify_log(graph(self.root), text)))

    def testMixedDebugOutputRejectedEvenWhenReleaseAlsoAppears(self):
        text = f'{self.b.stem} -> {self.output(self.b, "Debug")}\n' + '\n'.join(
            f'{p.stem} -> {self.output(p)}' for p in (self.a, self.b))
        with self.assertRaisesRegex(ValueError, 'Non-Release'):
            verify_log(graph(self.root), text)

    def testMissingOutputEvidenceRejected(self):
        with self.assertRaisesRegex(ValueError, 'Missing build-output'):
            verify_log(graph(self.root), f'{self.a.stem} -> {self.output(self.a)}')

    def testConsumerAssemblyMismatchRejected(self):
        source = self.output(self.a)
        consumer = self.root / 'consumer'
        consumer.mkdir()
        target = consumer / source.name
        target.write_bytes(source.read_bytes())
        self.assertEqual(1, verify_copies({self.a.stem: source}, consumer))
        target.write_bytes(b'other-build')
        with self.assertRaisesRegex(ValueError, 'does not match'):
            verify_copies({self.a.stem: source}, consumer)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', default='.')
    parser.add_argument('--build-log')
    parser.add_argument('--report')
    parser.add_argument('--self-test', action='store_true')
    args = parser.parse_args()
    if args.self_test:
        result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(GateTests))
        return 0 if result.wasSuccessful() else 1
    projects = graph(args.root)
    report = {'projectCount': len(projects), 'solutionGraph': 'complete'}
    if args.build_log:
        outputs = verify_log(projects, Path(args.build_log).read_text(encoding='utf-8-sig'))
        sample = outputs.get('NeraSpreadSheet.Avalonia.Sample')
        if sample is None:
            raise ValueError('Sample output evidence is missing.')
        report['sampleAssemblyMatches'] = verify_copies(outputs, sample.parent)
        report['outputs'] = {name: {'configuration': 'Release', 'path': str(path), 'sha256': digest(path)}
                             for name, path in sorted(outputs.items())}
    text = json.dumps(report, indent=2)
    if args.report:
        target = Path(args.report)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text + '\n', encoding='utf-8')
    print(text)
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (ValueError, OSError, ET.ParseError) as error:
        print('AVALONIA_RELEASE_GATE_FAILED: ' + str(error), file=sys.stderr)
        sys.exit(1)
