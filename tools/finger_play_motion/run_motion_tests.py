"""Offline extractor/compiler/runner for the P1.2C motion sampling regression.

Production code is read, never written: methods and fields are extracted verbatim from
Assets/Scripts/Coin/FingerPlayHarness.cs, compiled against deterministic Unity/SDK doubles, and run
without Unity, Oculus assemblies, native calls, physics or XR input. Nothing here validates real
engine behaviour, real SDK event ordering, XR tracking or subjective feel.

Usage:
    python tools/finger_play_motion/run_motion_tests.py --unity-editor "G:/Dev/Unity/Hub/Editor/6000.0.66f2/Editor"

Artifacts are written to a new temporary directory outside the repository. Deliberate defect
injections are applied only to temporary copies of the extracted snippets.
"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]
PRODUCTION = ROOT / "Assets/Scripts/Coin/FingerPlayHarness.cs"
GATE = ROOT / "Assets/Scripts/Coin/TossIntentGate.cs"
BACKUP = Path("G:/Dev/Toss-VR_AB_Backup/FingerPlayHarness.P1_2C.cs")
HERE = Path(__file__).resolve().parent
METHODS = ["UpdateMotionSampling", "GetEstimatedVelocities", "GetSelectingPointsCount", "OnGrabBegin",
           "OnGrabMove", "OnGrabCancel", "OnGrabEnd", "UpdateSettlingPhysics", "CompleteSettle",
           "EnterInteractionAuthority", "ExitInteractionAuthority", "Update", "FixedUpdate",
           "HandlePointerEvent", "ResyncSelectionState"]
TOSS_METHODS = ["LateUpdate", "ResetTossObservation", "FindRawTossSource", "BeginTossObservation",
                "ObserveTossInput", "ResolvePendingToss", "UpdateTossFlight", "RecoverToss", "Filter",
                "OnDisable", "ResetToInitialPose", "Subscribe", "Unsubscribe"]
HELPERS = ["IsFinite", "IsFiniteVector", "IsFiniteRotation", "FiniteVelocityOrZero", "ResetMotionSampling",
           "CacheReleaseProvenance", "DescribeProvenanceObject", "ObserveReleaseProvenance"]


def digest(path):
    # Preserve missing tracked paths in both snapshots so source moves remain testable.
    try:
        return hashlib.sha256(Path(path).read_bytes()).hexdigest()
    except FileNotFoundError:
        return None


def mask(text):
    """Blank out comments and string literals while preserving offsets, for brace matching."""
    token = r'//[^\r\n]*|/\*[\s\S]*?\*/|@"(?:""|[^"])*"|\$?"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\''
    return re.sub(token, lambda m: re.sub(r'[^\r\n]', ' ', m.group()), text)


def block(text, pattern):
    """Return (start, end, verbatim source) for the single method/type matching pattern."""
    masked = mask(text)
    hits = list(re.finditer(pattern, masked, re.M))
    assert len(hits) == 1, (pattern, len(hits))
    opening = masked.index('{', hits[0].end())
    depth, end = 1, opening + 1
    while depth:
        if masked[end] == '{':
            depth += 1
        elif masked[end] == '}':
            depth -= 1
        end += 1
    return hits[0].start(), end, text[hits[0].start():end]


def fixture_source(snippets):
    out = ('// GENERATED OFFLINE FIXTURE: production members copied verbatim; Unity/SDK types are doubles.\n'
           + snippets['imports'][2] + 'namespace Toss.Coin\n{\n')
    out += snippets['ReleaseProfile'][2] + '\n' + snippets['FingerPlayState'][2] + '\n'
    out += '    public partial class FingerPlayHarness : MonoBehaviour\n    {\n' + snippets['fields'][2]
    out += '\n\n'.join(snippets[name][2] for name in METHODS + HELPERS + TOSS_METHODS if name in snippets)
    out += '\n    }\n}\n'
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity-editor', required=True, type=Path,
                        help='Installed Editor directory (the one containing Unity.exe). Unity itself is never started.')
    parser.add_argument('--skip-defect-injection', action='store_true')
    parser.add_argument('--safety-only', action='store_true')
    parser.add_argument('--source-revision', help='Read production from git; use --skip-defect-injection for old revisions.')
    parser.add_argument('--compare-normal', type=Path, help='Compare against a saved pre-fix normal.run.log.')
    parser.add_argument('--compare-lifecycle', type=Path, help='Compare lifecycle side effects, excluding only provenance logs.')
    args = parser.parse_args()

    data = args.unity_editor.resolve() / 'Data'
    framework = data / 'MonoBleedingEdge/lib/mono/4.5'
    mono, csc = data / 'MonoBleedingEdge/bin/mono.exe', framework / 'csc.exe'
    references = [framework / (name + '.dll') for name in
                  ('mscorlib', 'System', 'System.Core', 'System.Numerics', 'System.Numerics.Vectors')]
    needed = [mono, csc, *references, PRODUCTION, GATE, BACKUP, HERE / 'MotionDoubles.cs', HERE / 'MotionTests.cs']
    for path in needed:
        if not Path(path).is_file():
            parser.error(f'Missing required file: {path}')

    before_production, before_backup, before_gate = digest(PRODUCTION), digest(BACKUP), digest(GATE)
    tracked = [p for p in subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT, text=True).split('\0') if p]
    tracked_before = {p: digest(ROOT / p) for p in tracked}

    source_bytes = (subprocess.check_output(['git', 'show', args.source_revision + ':' + PRODUCTION.relative_to(ROOT).as_posix()], cwd=ROOT)
                    if args.source_revision else PRODUCTION.read_bytes())
    gate_bytes = GATE.read_bytes()  # Always compile the actual canonical gate, not a fixture implementation.
    text = source_bytes.decode('utf-8-sig').replace('\r\n', '\n')
    imports_end = text.index('namespace Toss.Coin')
    snippets = {'imports': (0, imports_end, text[:imports_end])}
    for enum in ('ReleaseProfile', 'FingerPlayState'):
        snippets[enum] = block(text, rf'^[ \t]*public enum {enum}\b')
    toss_enabled = 'ResetTossObservation' in mask(text)
    assert toss_enabled or args.source_revision, 'Current production must include the Toss lifecycle'
    for method in METHODS + (TOSS_METHODS if toss_enabled else []):
        snippets[method] = block(text, rf'^[ \t]*(?:private|public) (?:static )?(?:void|int|bool|FromOVRHandDataSource) {method}\(')
    for helper in HELPERS:
        pattern = rf'^[ \t]*private (?:static )?(?:void|bool|Vector3|string) {helper}\('
        if re.search(pattern, mask(text), re.M):
            snippets[helper] = block(text, pattern)
    start, stop = text.index('        [Header("Component References")]'), text.index('        private void Awake()')
    snippets['fields'] = (start, stop, text[start:stop])

    manifest = {'status': ['FINGER PLAY / TOSS OFFLINE REGRESSION', 'SYNTHETIC INPUT ONLY',
                           'PRODUCTION SOURCES READ ONLY DURING TESTS', 'RUNTIME VALIDATION PENDING'],
                'production': {'path': str(PRODUCTION), 'sha256_before': before_production,
                               'source_revision': args.source_revision, 'extracted_sha256': hashlib.sha256(source_bytes).hexdigest(),
                               'methods_extracted': [n for n in METHODS + HELPERS + TOSS_METHODS if n in snippets], 'verbatim_except_line_endings': True},
                'gate': {'path': str(GATE), 'sha256_before': before_gate,
                         'compiled_sha256': hashlib.sha256(gate_bytes).hexdigest(), 'verbatim': True},
                'test_source_sha256': {p.name: digest(p) for p in (HERE / 'MotionTests.cs', HERE / 'MotionDoubles.cs', Path(__file__))},
                'backup': {'path': str(BACKUP), 'sha256_before': before_backup}, 'limits': [
                    'No Unity/Oculus assemblies, native calls, physics integration or XR input.',
                    'Unity value types and Rigidbody are deterministic managed doubles.',
                    'Verifies arithmetic, clamping, queue bounds, state clearing and diagnostics on supplied evidence.',
                    'Nested callback orders are supplied by tests, not produced by the real SDK.',
                    'Not XR verification, not engine physics, not subjective weight validation.'],
                'steps': [], 'defect_injection': []}

    out = Path(tempfile.mkdtemp(prefix='TossVR-motion-sampling-'))
    env = dict(os.environ, TEMP=str(out), TMP=str(out), PYTHONDONTWRITEBYTECODE='1')
    print(f'ARTIFACTS {out}')
    print(f'PRODUCTION sha256 {before_production}')
    print(f'CANONICAL GATE sha256 {before_gate}')
    gate_fixture = out / 'TossIntentGate.cs'
    gate_fixture.write_bytes(gate_bytes)

    def write(path, content):
        Path(path).write_text(content, encoding='utf-8', newline='\n')

    def step(label, command, quiet=False):
        result = subprocess.run([str(item) for item in command], cwd=out, env=env,
                                capture_output=True, text=True, timeout=180)
        text_out = result.stdout + result.stderr
        write(out / (label + '.log'), text_out)
        print(f'{label}: exit={result.returncode}')
        if not quiet:
            print(text_out, end='')
        manifest['steps'].append({'label': label, 'exit_code': result.returncode})
        return result.returncode, text_out

    def build(label, source_text):
        fixture = out / (label + '.Fixture.cs')
        write(fixture, source_text)
        common = [mono, csc, '/nologo', '/noconfig', '/nostdlib+', '/langversion:9.0', '/warnaserror+',
                  '/deterministic+', '/nowarn:0169,0414,0649,0219']
        common += [f'/reference:{item}' for item in references]
        if 'CacheReleaseProvenance' in snippets:
            common += ['/define:RELEASE_PROVENANCE']
        if toss_enabled:
            common += ['/define:TOSS_LIFECYCLE']
        code, log = step(label + '.compile', [*common, '/target:exe', f'/out:{out}/{label}.Tests.exe',
                                              fixture, gate_fixture, HERE / 'MotionDoubles.cs', HERE / 'MotionTests.cs'])
        if code:
            return None
        return step(label + '.run', [mono, out / (label + '.Tests.exe'),
                                    *(['--safety-only'] if args.safety_only else [])])

    baseline = fixture_source(snippets)
    verified = build('baseline', baseline)
    success = verified is not None and verified[0] == 0
    if verified:
        match = re.search(r'OFFLINE RESULT: (\d+)/(\d+) cases passed, assertions=(\d+), failed assertions=(\d+)',
                          verified[1])
        manifest['baseline_totals'] = dict(zip(('passed', 'total', 'assertions', 'failed_assertions'),
                                               map(int, match.groups()))) if match else None

    if verified is not None:
        trace_code, trace = step('normal.run', [mono, out / 'baseline.Tests.exe', '--normal-trace'], quiet=True)
        manifest['normal_trace'] = {'exit_code': trace_code, 'sha256': digest(out / 'normal.run.log'),
                                    'lines': len(trace.splitlines()), 'expected_lines': 6240}
        success = success and trace_code == 0 and len(trace.splitlines()) == 6240
        if args.compare_normal:
            identical = (out / 'normal.run.log').read_bytes() == args.compare_normal.read_bytes()
            manifest['normal_trace']['reference'] = str(args.compare_normal)
            manifest['normal_trace']['identical'] = identical
            print('NORMAL TRAJECTORIES IDENTICAL=' + str(identical))
            success = success and identical
        lifecycle_code, lifecycle = step('lifecycle.run', [mono, out / 'baseline.Tests.exe', '--lifecycle-trace'], quiet=True)
        manifest['lifecycle_trace'] = {'exit_code': lifecycle_code, 'sha256': digest(out / 'lifecycle.run.log'),
                                       'lines': len(lifecycle.splitlines()), 'expected_lines': 192,
                                       'excludes': 'RELEASE_PROVENANCE logs only'}
        success = success and lifecycle_code == 0 and len(lifecycle.splitlines()) == 192
        if args.compare_lifecycle:
            identical = (out / 'lifecycle.run.log').read_bytes() == args.compare_lifecycle.read_bytes()
            manifest['lifecycle_trace'].update(reference=str(args.compare_lifecycle), identical=identical)
            print('LIFECYCLE SIDE EFFECTS IDENTICAL=' + str(identical))
            success = success and identical

    mutations = [
        ('tiny-dt gate removed', 'UpdateMotionSampling', 'dt <= 1e-4f', 'dt < 0f', 'H1 invalid time'),
        ('non-finite dt accepted', 'UpdateMotionSampling', '!IsFinite(dt) || ', '', 'H5 non-finite time'),
        ('invalid pose accepted', 'UpdateMotionSampling',
         ' || !IsFiniteVector(currentPos) || !IsFiniteRotation(currentRot)', '', 'H4 invalid poses'),
        ('finite velocity fallback removed', 'FiniteVelocityOrZero',
         'return IsFiniteVector(velocity) ? velocity : Vector3.zero;', 'return velocity;', 'H3 finite release'),
        ('newest weighting flattened', 'GetEstimatedVelocities', '                float weight = index;',
         '                float weight = 1;', 'non-uniform intervals'),
        ('Cancel keeps sampling queue', 'OnGrabCancel', '            _motionSamples.Clear();\n', '',
         'history contamination'),
        ('>180 fold threshold raised', 'UpdateMotionSampling', 'if (angleDeg > 180f) angleDeg -= 360f;',
         'if (angleDeg > 1800f) angleDeg -= 360f;', 'quaternion wrap'),
        ('dynamic switch omitted before velocity write', 'OnGrabEnd',
         '                    _rigidbody.isKinematic = false;\n                    _rigidbody.useGravity = false;',
         '                    _rigidbody.useGravity = false;', 'release clamp'),
    ]
    if not args.skip_defect_injection:
        for label, method, old, new, expected in mutations:
            assert snippets[method][2].count(old) == 1, (label, 'pattern not found exactly once')
            mutated = dict(snippets)
            mutated[method] = (0, 0, snippets[method][2].replace(old, new, 1))
            result = build('defect-' + re.sub(r'\W+', '-', label), fixture_source(mutated))
            if result is None:
                manifest['defect_injection'].append({'label': label, 'expected_case': expected,
                                                     'detected': False, 'note': 'compile failure'})
                success = False
                continue
            code, log = result
            failing = [line for line in log.splitlines() if line.startswith('FAIL ')]
            detected = code == 1 and any(expected.lower() in line.lower() for line in failing)
            manifest['defect_injection'].append({'label': label, 'expected_case': expected, 'run_exit': code,
                                                 'detected': detected, 'failing_cases': failing})
            print(f'DEFECT INJECTION {label}: detected={detected}')
            success = success and detected

    after_production, after_backup, after_gate = digest(PRODUCTION), digest(BACKUP), digest(GATE)
    tracked_changed = [p for p, value in tracked_before.items() if digest(ROOT / p) != value]
    manifest['production']['sha256_after'] = after_production
    manifest['production']['unchanged'] = after_production == before_production
    manifest['gate']['sha256_after'] = after_gate
    manifest['gate']['unchanged'] = after_gate == before_gate == manifest['gate']['compiled_sha256']
    manifest['backup']['sha256_after'] = after_backup
    manifest['backup']['unchanged'] = after_backup == before_backup
    manifest['tracked_files'] = {'count': len(tracked_before), 'changed': tracked_changed,
                                 'missing_at_start': [p for p, value in tracked_before.items() if value is None]}
    manifest['success'] = bool(success and manifest['production']['unchanged'] and manifest['gate']['unchanged']
                               and manifest['backup']['unchanged'] and not tracked_changed)
    write(out / 'manifest.json', json.dumps(manifest, indent=2))
    print('TRACKED FILES CHECKED=' + str(len(tracked_before)) + ' CHANGED=' + str(tracked_changed))
    print('PRODUCTION, GATE AND BACKUP UNCHANGED=' + str(manifest['production']['unchanged']
                                                       and manifest['gate']['unchanged'] and manifest['backup']['unchanged']))
    print('MANIFEST ' + str(out / 'manifest.json'))
    print('OFFLINE ONLY: doubles are not Unity, not the Oculus Interaction SDK and not XR input.')
    print('P1.2C IMPLEMENTATION CHECK | RUNTIME VALIDATION PENDING')
    return 0 if manifest['success'] else 1


if __name__ == '__main__':
    sys.exit(main())
