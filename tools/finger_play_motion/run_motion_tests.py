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
BACKUP = Path("G:/Dev/Toss-VR_AB_Backup/FingerPlayHarness.P1_2C.cs")
HERE = Path(__file__).resolve().parent
METHODS = ["UpdateMotionSampling", "GetEstimatedVelocities", "GetSelectingPointsCount", "OnGrabBegin",
           "OnGrabMove", "OnGrabCancel", "OnGrabEnd", "UpdateSettlingPhysics", "CompleteSettle",
           "EnterInteractionAuthority", "ExitInteractionAuthority", "Update", "FixedUpdate"]


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


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
           'using System;\nusing System.Collections.Generic;\nusing UnityEngine;\nusing Oculus.Interaction;\n'
           'namespace Toss.Coin\n{\n')
    out += snippets['ReleaseProfile'][2] + '\n' + snippets['FingerPlayState'][2] + '\n'
    out += '    public partial class FingerPlayHarness : MonoBehaviour\n    {\n' + snippets['fields'][2]
    out += '\n\n'.join(snippets[name][2] for name in METHODS)
    out += '\n    }\n}\n'
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity-editor', required=True, type=Path,
                        help='Installed Editor directory (the one containing Unity.exe). Unity itself is never started.')
    parser.add_argument('--skip-defect-injection', action='store_true')
    args = parser.parse_args()

    data = args.unity_editor.resolve() / 'Data'
    framework = data / 'MonoBleedingEdge/lib/mono/4.5'
    mono, csc = data / 'MonoBleedingEdge/bin/mono.exe', framework / 'csc.exe'
    references = [framework / (name + '.dll') for name in
                  ('mscorlib', 'System', 'System.Core', 'System.Numerics', 'System.Numerics.Vectors')]
    needed = [mono, csc, *references, PRODUCTION, BACKUP, HERE / 'MotionDoubles.cs', HERE / 'MotionTests.cs']
    for path in needed:
        if not Path(path).is_file():
            parser.error(f'Missing required file: {path}')

    before_production, before_backup = digest(PRODUCTION), digest(BACKUP)
    tracked = [p for p in subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT, text=True).split('\0') if p]
    tracked_before = {p: digest(ROOT / p) for p in tracked}

    text = PRODUCTION.read_text(encoding='utf-8-sig')
    snippets = {}
    for enum in ('ReleaseProfile', 'FingerPlayState'):
        snippets[enum] = block(text, rf'^[ \t]*public enum {enum}\b')
    for method in METHODS:
        snippets[method] = block(text, rf'^[ \t]*(?:private|public) (?:void|int) {method}\(')
    start, stop = text.index('        [Header("Component References")]'), text.index('        private void Awake()')
    snippets['fields'] = (start, stop, text[start:stop])

    manifest = {'status': ['P1.2C MOTION SAMPLING OFFLINE REGRESSION', 'ISOLATED TEST ONLY',
                           'PRODUCTION BEHAVIOUR UNCHANGED', 'RUNTIME VALIDATION PENDING'],
                'production': {'path': str(PRODUCTION), 'sha256_before': before_production,
                               'methods_extracted': METHODS, 'verbatim': True},
                'backup': {'path': str(BACKUP), 'sha256_before': before_backup}, 'limits': [
                    'No Unity/Oculus assemblies, native calls, physics integration or XR input.',
                    'Unity value types and Rigidbody are deterministic managed doubles.',
                    'Verifies arithmetic, clamping, queue bounds and state clearing only.',
                    'Not XR verification, not engine physics, not subjective weight validation.'],
                'steps': [], 'defect_injection': []}

    out = Path(tempfile.mkdtemp(prefix='TossVR-motion-sampling-'))
    env = dict(os.environ, TEMP=str(out), TMP=str(out), PYTHONDONTWRITEBYTECODE='1')
    print(f'ARTIFACTS {out}')
    print(f'PRODUCTION sha256 {before_production}')

    def write(path, content):
        Path(path).write_text(content, encoding='utf-8', newline='\n')

    def step(label, command):
        result = subprocess.run([str(item) for item in command], cwd=out, env=env,
                                capture_output=True, text=True, timeout=180)
        text_out = result.stdout + result.stderr
        write(out / (label + '.log'), text_out)
        print(f'{label}: exit={result.returncode}')
        print(text_out, end='')
        manifest['steps'].append({'label': label, 'exit_code': result.returncode})
        return result.returncode, text_out

    def build(label, source_text):
        fixture = out / (label + '.Fixture.cs')
        write(fixture, source_text)
        common = [mono, csc, '/nologo', '/noconfig', '/nostdlib+', '/langversion:9.0', '/warnaserror+',
                  '/deterministic+', '/nowarn:0169,0414,0649,0219']
        common += [f'/reference:{item}' for item in references]
        code, log = step(label + '.compile', [*common, '/target:exe', f'/out:{out}/{label}.Tests.exe',
                                              fixture, HERE / 'MotionDoubles.cs', HERE / 'MotionTests.cs'])
        if code:
            return None
        return step(label + '.run', [mono, out / (label + '.Tests.exe')])

    baseline = fixture_source(snippets)
    verified = build('baseline', baseline)
    success = verified is not None and verified[0] == 0
    if verified:
        match = re.search(r'OFFLINE RESULT: (\d+)/(\d+) cases passed, assertions=(\d+), failed assertions=(\d+)',
                          verified[1])
        manifest['baseline_totals'] = dict(zip(('passed', 'total', 'assertions', 'failed_assertions'),
                                               map(int, match.groups()))) if match else None

    mutations = [
        ('tiny-dt gate removed', 'UpdateMotionSampling', '            if (dt > 1e-4f)', '            if (dt > -1f)',
         'tiny dt threshold'),
        ('newest weighting flattened', 'GetEstimatedVelocities', '                float weight = index;',
         '                float weight = 1;', 'non-uniform intervals'),
        ('Cancel keeps sampling queue', 'OnGrabCancel', '            _motionSamples.Clear();\n', '',
         'history contamination'),
        ('>180 fold threshold raised', 'UpdateMotionSampling', '                if (angleDeg > 180f) angleDeg -= 360f;',
         '                if (angleDeg > 1800f) angleDeg -= 360f;', 'quaternion wrap'),
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

    after_production, after_backup = digest(PRODUCTION), digest(BACKUP)
    tracked_changed = [p for p, value in tracked_before.items() if digest(ROOT / p) != value]
    manifest['production']['sha256_after'] = after_production
    manifest['production']['unchanged'] = after_production == before_production
    manifest['backup']['sha256_after'] = after_backup
    manifest['backup']['unchanged'] = after_backup == before_backup
    manifest['tracked_files'] = {'count': len(tracked_before), 'changed': tracked_changed}
    manifest['success'] = bool(success and manifest['production']['unchanged']
                               and manifest['backup']['unchanged'] and not tracked_changed)
    write(out / 'manifest.json', json.dumps(manifest, indent=2))
    print('TRACKED FILES CHECKED=' + str(len(tracked_before)) + ' CHANGED=' + str(tracked_changed))
    print('PRODUCTION AND BACKUP UNCHANGED=' + str(manifest['production']['unchanged']
                                                  and manifest['backup']['unchanged']))
    print('MANIFEST ' + str(out / 'manifest.json'))
    print('OFFLINE ONLY: doubles are not Unity, not the Oculus Interaction SDK and not XR input.')
    print('P1.2C IMPLEMENTATION CHECK | RUNTIME VALIDATION PENDING')
    return 0 if success else 1


if __name__ == '__main__':
    sys.exit(main())
