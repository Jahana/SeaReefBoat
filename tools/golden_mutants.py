"""Golden harness mutant runner (harness-speed): the lanes' mutant scripts' loop, faster, with the same TSV and kill rules.

A lane's script keeps its mutant list and calls main():

    import os, sys
    WT = sys.argv[1]
    sys.dont_write_bytecode = True                # no tools/__pycache__ in the worktree
    sys.path.insert(0, os.path.join(WT, 'tools'))
    import golden_mutants as gm
    keys = gm.keys_of(WT)                         # keys('mission.cop.three_paths*') -> ['flow:...', ...]
    SC = keys('mission.cop.three_paths*') + ...   # the scope
    ALL = sorted(set(SC + keys(...)))             # the group
    M = [('M01', 'src/XI.QuestContent/...cs', 'old text', 'new text'), ...]   # a 5th element: that mutant's scope
    gm.main(WT, sys.argv[2], M, scope=SC, group=ALL, db='xidb_laneN', checks=['--only', 'c3 C3b'], argv=sys.argv[3:])

    usage: python -I <script> <worktree> <out.tsv> [--no-base] [ALLIDS | id ...]
      (default)   the base first, unmutated (BASE lines): each scope replayed alone, each mutant's exact replay (its
                  scope, then its group's others, --no-gate) and the fake checks (exit 0, no [FAIL]). A failing base
                  stops the run before any mutant. With no ids: only the base. --base (or BASE) is accepted, a no-op
      --no-base   no base (a rerun of a few ids on a tree whose base passed): printed as a WARNING and written to the
                  TSV as `BASE<TAB>skipped (--no-base)`
      ALLIDS      every mutant; else the ids given

group is a list, or a function of a mutant's scope that returns its group. checks: the arguments after `--` of the
fake checks (tests/XI.Verify.Quests), or None for none. `python -I -B tools/golden_mutants.py --selftest [<dir>]`
checks the selector, the kill's place and, on a made-up worktree in <dir> (default: the system's temp dir) with a fake
dotnet, the build paths, the bin checks, the base, the restore and every stop.

On any doubt the runner stops (RunnerError, exit 1) rather than guess a verdict: a wrong verdict weakens a port's proof.

Per mutant (the source restored on every path, also on an exception or Ctrl-C):
  0. <out>.inflight names the file and its original's sha (the original kept in <out>.inflight.orig) while the tree
     is mutated; a run that finds it stops unless the file is back as it was. Every write is a temp file + os.replace;
  1. the mutation applied: its old text must occur exactly once (else `BAD MUTANT: <n> matches`);
  2. the build: a mutant under src/XI.QuestContent/ builds that project alone (--no-dependencies) and its dll and pdb
     are copied into the harness's bin, checked by hash; then EVERY other dll of the harness's bin must be the base
     build's. When its reference assembly (the API other projects compile against) differs from the unmutated one, or
     the mutant is elsewhere, the harness is built whole; the bin is then dirty, and before the next mutant (and after
     the last) the restored tree is built whole again and every dll of the bin must be the base build's. A failure is
     `BUILD FAILED` with the first two error lines. The timing TSV records the path (build-path questcontent, whole,
     or whole (reference assembly changed));
  3. ONE replay: `replay <the scope's elements, then the group's others> --fail-fast --no-gate`. Every FAIL line is
     placed: a picked flow's (`FAIL <flow>:`; a missing or stale trace stops the runner), a picked flow's waiver
     (`FAIL <flow>.flow:<n>: unknown handler`), an exception thrown in a flow's replay (Program.Replay in its stack:
     the first picked flow with no line yet; a MySql exception stops the runner) or a coverage line (every flow
     passed: the scope replayed alone decides, scope when it fails there too, as the old runner's scope run did). The
     first one names the kill: `KILLED<TAB>scope` or `KILLED<TAB>group`, with the first FAIL line. Anything else stops
     the runner: a lane lock conflict, the party cleanup check (`stopped at the party cleanup check`), the item
     stacks, a parse, an exception outside a flow, any line it cannot place; so do a run with no summary line, a
     nonzero exit with no FAIL line (the database down: exit 3) and a replay over the timeout (killed);
  4. a survivor: the scope replayed alone when the group adds flows (a scope key's coverage that a group flow reached
     in the combined run): a failure is `KILLED<TAB>scope`. Then the fake checks built (tests/XI.Verify.Quests) and
     run: a printed [FAIL] kills it (`SURVIVED flows, KILLED by check`), else `SURVIVED ALL`; a failed checks build is
     `SURVIVED flows<TAB>checks build failed`; a run with no RESULT line, or a nonzero exit with no [FAIL] (a crash, an
     --only that matches nothing), stops the runner.
The harness is built whole, unmutated, before the first mutant and after the last; every dll of its bin is then the
base build's again. <out>.timing.tsv gets each mutant's phase times and build path.

--one-process (mutant-design, option 3: docs/superpowers/specs/2026-11-10-golden-mutant-process-design.md), opt-in;
without it every step above is as it was. After the whole build (and the checks' build):
  1. the schema: the mutants (their old text once: else BAD MUTANT as above) written to <out>.schema/mutants.json and
     the harness's `schema` mode run over them: each mutant a switch in a schema file of <out>.schema/gen (proven by
     projection: the schema specialised for the mutant is its text-patched file, for none the tree's file), or a
     fallback with its reason;
  2. the schema build: XI.QuestContent built from a wrapper project in <out>.schema/build (the project's own Compile
     items in their order, each schema file in its source's place); its reference assembly must be the base build's.
     Copies of the harness's bin and the checks' bin (bin/Schema beside each project's bin/Release, never those bins)
     get its dll; every other dll of each copy must be its base build's;
  3. ONE process, `mutants <out>.schema/plan.json --db <lane>` from the harness copy: the mutants a switch reached
     while the class constructors and the process's setup ran (STATICINIT) fall back; the base in it (each scope
     alone, each mutant's exact replay, unmutated: BASE lines as above; with --no-base the same selections as warm-up
     blocks, no verdict), the checks unmutated from the checks copy; then each mutant: its replay a block of the
     process (MUTANT <tag> BEGIN ... END), placed by the same rules, the scope alone in the process for a survivor
     (and a coverage line's place), the checks from the checks copy with XI_MUTANT=<switch> (no build). A mutant
     whose static variant throws when the process makes it (VARIANT-THREW) falls back. The process checks its
     fingerprints after each block and that a passing mutant's block reached its switch as the base's blocks of the
     same selection did, and runs the canary every 10 mutants and at the end; a STOP line, a block with no END, a
     block whose summary counts a failure with no FAIL line, a crash (the process gone), no line within the timeout
     (killed) stop the runner;
  4. the fallbacks: through the loop above (their own base first, out of process), then the whole build at the end.
The timing TSV marks each mutant `path one-process` or `path fallback <reason>`. --schema-only: the whole build and
step 1 only, the shares printed (no database, no replay). --one-process with no ids is refused (the base alone is the
loop's).
Limit: a run with more than one scope (or group) prints a WARNING. A cache filled by one scope's base block and read
only by another scope is not detected by the reach check; equivalence was proven on single-scope runs. No such cache
exists today; one process per scope would close it.
"""
import glob
import hashlib
import json
import os
import queue
import re
import shutil
import subprocess
import sys
import tempfile
import threading
import time

HARNESS = 'tests/XI.Verify.QuestGolden.Live/XI.Verify.QuestGolden.Live.csproj'
HARNESS_BIN = 'tests/XI.Verify.QuestGolden.Live/bin/Release/net10.0'
GOLDEN_DLL = HARNESS_BIN + '/XI.Verify.QuestGolden.Live.dll'
QC_DIR = 'src/XI.QuestContent/'
QC = QC_DIR + 'XI.QuestContent.csproj'
QC_BIN = QC_DIR + 'bin/Release/net10.0'
QC_REF = QC_DIR + 'obj/Release/net10.0/ref/XI.QuestContent.dll'
QC_FILES = ('XI.QuestContent.dll', 'XI.QuestContent.pdb')
CHECKS = 'tests/XI.Verify.Quests/XI.Verify.Quests.csproj'
CHECKS_RUN = ['dotnet', 'run', '--no-build', '-c', 'Release', '--project', 'tests/XI.Verify.Quests', '--']
BUILD_ARGS = ['-c', 'Release', '-m:1', '-v:q', '-nologo']
SUMMARY = re.compile(r'^(\d+) passed, (\d+) failed \(')
STOPPED = re.compile(r'^fail-fast: stopped at (.+?), the first failure;')
FLOW_PASS = re.compile(r'^PASS (\S+): \d+ steps as recorded')
COVERAGE_FAIL = re.compile(r'^FAIL coverage (\S+?):')
WAIVER_FAIL = re.compile(r'^FAIL (\S+)\.flow:\d+: unknown handler ')
CHECKS_RESULT = re.compile(r'RESULT: (\d+) passed, (\d+) failed')
REPLAY_FRAME = re.compile(r'\bProgram\.Replay\(')
DB_EXCEPTION = re.compile(r'MySql\w*Exception')
# a flow's FAIL that is its trace's, not its run's (Program.Replay): the tree's, never a mutant's
STALE_TRACE = ('no trace; record it', 'the flow changed since it was recorded', 'the harness seeds')
NEXT_LINE = re.compile(r'^(PASS|FAIL|SKIP|fail-fast:|\d+ passed, )')
BUILD_TIMEOUT = 1800
CHECKS_TIMEOUT = 1800
REPLAY_TIMEOUT = 3600        # until the base sets it: 5 times its longest replay, at least REPLAY_TIMEOUT_MIN
REPLAY_TIMEOUT_MIN = 1200
TMP_SUFFIX = '.golden-mutants-tmp'
# --one-process (option 3)
SCHEMA_HARNESS_BIN = 'tests/XI.Verify.QuestGolden.Live/bin/Schema'
CHECKS_BIN = 'tests/XI.Verify.Quests/bin/Release/net10.0'
SCHEMA_CHECKS_BIN = 'tests/XI.Verify.Quests/bin/Schema'
CHECKS_DLL = 'XI.Verify.Quests.dll'
QC_INFO = QC_DIR + 'obj/Release/net10.0/XI.QuestContent.AssemblyInfo.cs'
INFO_VERSION = re.compile(r'AssemblyInformationalVersionAttribute\("([^"]*)"\)')
SWITCH_ENV = 'XI_MUTANT'
CANARY_EVERY = 10
HEAP_CEILING_MB = 2048
STARTUP_TIMEOUT = 1800


class VariantThrew(Exception):
    """--one-process: the mutant's static variant threw when the process made it (VARIANT-THREW): option 1 runs it,
    where the throwing initializer poisons its whole type"""


class RunnerError(Exception):
    """a run whose outcome is no verdict (no summary line, a crash, the database down, a line it cannot place, a bin
    that is not the base build's): the runner stops"""


def keys_of(wt):
    """keys(pattern) -> ['flow:<name>', ...] of tests/golden/flows/<pattern>.flow, in name order (the lanes' keys())"""
    def keys(pat):
        return ['flow:' + os.path.basename(f)[:-5] for f in sorted(glob.glob(os.path.join(wt, 'tests/golden/flows', pat)))]
    return keys


def flow_names(wt):
    """every flow's name, in the harness's order (FlowPaths: the file names', .flow included, ordinal)"""
    files = sorted(f for f in os.listdir(os.path.join(wt, 'tests/golden/flows')) if f.endswith('.flow'))
    return [f[:-5] for f in files]


def resolve(names, elements):
    """the flows a selector's elements pick, as SelectFlows does: flow:<name> one flow, <x> the flow named x and every
    flow of key x; each once, at its first place. An element that picks nothing raises"""
    out, seen = [], set()
    for e in elements:
        if e.startswith('flow:'):
            some = [n for n in names if n == e[5:]]
        else:
            some = [n for n in names if n == e or n.split('#')[0] == e]
        if not some:
            raise RunnerError(f'{e}: no flow of that name')
        for n in some:
            if n not in seen:
                seen.add(n)
                out.append(n)
    return out


def sha(path):
    with open(path, 'rb') as f:
        return hashlib.sha256(f.read()).hexdigest()


def write_atomic(path, data):
    """data as path's bytes: a temp file beside it, then os.replace (never a half-written file)"""
    tmp = path + TMP_SUFFIX
    with open(tmp, 'wb') as f:
        f.write(data)
        f.flush()
        os.fsync(f.fileno())
    os.replace(tmp, path)


def errors_of(text):
    return [l for l in text.splitlines() if ' error ' in l][:2]


class MutantProcess:
    """the harness's mutants mode as a child process: commands to its stdin, its lines read by a thread so that a wait
    can time out (readline: a line, None at the end of its output, TimeoutError past the timeout)"""

    def __init__(self, cmd, cwd, env):
        self.p = subprocess.Popen(cmd, cwd=cwd, env=env, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                  stderr=subprocess.STDOUT, text=True, encoding='utf-8', errors='replace', bufsize=1)
        self.q = queue.Queue()
        threading.Thread(target=self._read, daemon=True).start()

    def _read(self):
        for line in self.p.stdout:
            self.q.put(line.rstrip('\r\n'))
        self.q.put(None)

    def send(self, line):
        try:
            self.p.stdin.write(line + '\n')
            self.p.stdin.flush()
        except OSError:
            pass                    # the process is gone: the next readline says so

    def readline(self, timeout):
        try:
            return self.q.get(timeout=max(1.0, timeout))
        except queue.Empty:
            raise TimeoutError from None

    def exit_code(self, wait=60):
        try:
            return self.p.wait(wait)
        except subprocess.TimeoutExpired:
            return None

    def kill(self):
        if self.p.poll() is None:
            if os.name == 'nt':
                subprocess.run(['taskkill', '/T', '/F', '/PID', str(self.p.pid)], capture_output=True)
            else:
                self.p.kill()
        try:
            self.p.wait(60)
        except subprocess.TimeoutExpired:
            pass


class Runner:
    def __init__(self, wt, out, db, checks=None, log=print):
        self.wt, self.out, self.db, self.checks, self.log = wt, out, db, checks, log
        self.names = flow_names(wt)
        self.base_ref = None
        self.base_bin = None        # {path in the bin: sha} of every dll, the unmutated whole build
        self.dirty = False          # a mutant's whole build left the bin (and the projects' outputs) mutated
        self.replay_timeout = REPLAY_TIMEOUT
        self.marker = out + '.inflight'
        self.proc = None            # --one-process: the mutants process
        self.schema_dir = os.path.abspath(out) + '.schema'    # handed to child processes, whose cwd is the worktree

    def p(self, rel):
        return os.path.join(self.wt, rel)

    def execute(self, cmd, timeout, env=None):
        """(exit code, stdout + stderr); the process tree killed and TimeoutExpired raised past the timeout"""
        p = subprocess.Popen(cmd, cwd=self.wt, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
                             encoding='utf-8', errors='replace', env=env)
        try:
            out, _ = p.communicate(timeout=timeout)
        except subprocess.TimeoutExpired:
            if os.name == 'nt':
                subprocess.run(['taskkill', '/T', '/F', '/PID', str(p.pid)], capture_output=True)
            else:
                p.kill()
            p.communicate()
            raise
        return p.returncode, out

    def run(self, cmd, timeout, env=None):
        try:
            return self.execute(cmd, timeout) if env is None else self.execute(cmd, timeout, env)
        except subprocess.TimeoutExpired:
            raise RunnerError(f'{" ".join(str(c) for c in cmd[:4])} ... ran over {timeout:.0f} s and was killed: '
                              'no verdict') from None

    def spawn(self, cmd, env):
        """--one-process: the mutants process"""
        return MutantProcess(cmd, self.wt, env)

    def write(self, line):
        with open(self.out, 'a', encoding='utf-8') as out:
            out.write(line + '\n')

    def timing(self, line):
        with open(self.out + '.timing.tsv', 'a', encoding='utf-8') as out:
            out.write(line + '\n')

    # ---- the tree: a mutated file is marked while it is

    def check_marker(self):
        """an earlier run's <out>.inflight: gone when its file is as it was, else the runner stops"""
        if not os.path.exists(self.marker):
            return
        with open(self.marker, encoding='utf-8') as f:
            rel, want = (f.read().split('\n') + ['', ''])[:2]
        full = self.p(rel)
        now = sha(full) if os.path.exists(full) else None
        backup = self.marker + '.orig'
        if now != want:
            raise RunnerError(f'{self.marker}: an earlier run stopped with {rel} mutated (sha {now}, its original '
                              f'{want}): restore it from {backup} (or git checkout -- {rel}), check the tree, then '
                              'delete the marker')
        if os.path.exists(full + TMP_SUFFIX):
            os.remove(full + TMP_SUFFIX)
        self.unmark()
        self.log(f'{self.marker}: an earlier run\'s marker, {rel} as its original: removed')

    def mark(self, rel, orig):
        write_atomic(self.marker + '.orig', orig)
        write_atomic(self.marker, f'{rel}\n{hashlib.sha256(orig).hexdigest()}\n'.encode())

    def unmark(self):
        for f in (self.marker, self.marker + '.orig'):
            if os.path.exists(f):
                os.remove(f)

    # ---- builds

    def build_whole(self):
        code, text = self.run(['dotnet', 'build', HARNESS] + BUILD_ARGS, BUILD_TIMEOUT)
        return code == 0, errors_of(text)

    def bin_hashes(self):
        """{path in the harness bin: sha} of every dll the harness loads (its own, the projects', the packages')"""
        root, out = self.p(HARNESS_BIN), {}
        for d, _, files in os.walk(root):
            for f in files:
                if f.lower().endswith('.dll'):
                    full = os.path.join(d, f)
                    out[os.path.relpath(full, root).replace('\\', '/')] = sha(full)
        return out

    def check_bin(self, what, skip=()):
        """every dll of the harness bin (but skip) the base build's, none missing or added: else the runner stops"""
        now = self.bin_hashes()
        bad = sorted(k for k in set(now) | set(self.base_bin) if k not in skip and now.get(k) != self.base_bin.get(k))
        if bad:
            raise RunnerError(f'{what}: {len(bad)} dll(s) of the harness bin are not the unmutated base build\'s: '
                              + ', '.join(bad[:8]))
        return len(now)

    def build_first(self):
        """the unmutated harness, whole: the reference assembly and every dll of the bin the mutants are checked
        against"""
        ok, errs = self.build_whole()
        if not ok:
            raise RunnerError('the unmutated harness does not build: ' + ' | '.join(errs))
        self.base_ref = sha(self.p(QC_REF))
        self.base_bin = self.bin_hashes()
        self.dirty = False
        if QC_FILES[0] not in self.base_bin:
            raise RunnerError(f'{HARNESS_BIN} holds no {QC_FILES[0]}')

    def clean(self, what):
        """the restored tree built whole again: every dll of the bin and the reference assembly the base build's"""
        t = time.monotonic()
        ok, errs = self.build_whole()
        if not ok:
            raise RunnerError(f'{what}: the restored tree does not build: ' + ' | '.join(errs))
        n = self.check_bin(what)
        if sha(self.p(QC_REF)) != self.base_ref:
            raise RunnerError(f'{what}: {QC_REF} is not the base build\'s')
        self.dirty = False
        self.log(f'{what}: the harness built whole again, all {n} dlls of its bin as the base build '
                 f'({time.monotonic() - t:.0f} s)')
        self.timing(f'CLEAN\t{what}\tbuild {time.monotonic() - t:.1f}')

    def build_mutant(self, path):
        """(ok, errors, how): XI.QuestContent alone and copied into the harness's bin (every other dll checked), or
        the harness whole (the bin dirty: clean() before the next mutant)"""
        if not path.replace('\\', '/').startswith(QC_DIR):
            self.dirty = True
            ok, errs = self.build_whole()
            return ok, errs, 'whole'
        code, text = self.run(['dotnet', 'build', QC, '--no-dependencies'] + BUILD_ARGS, BUILD_TIMEOUT)
        if code != 0:
            return False, errors_of(text), 'questcontent'
        if sha(self.p(QC_REF)) != self.base_ref:
            # the API changed: the projects that compile against it are built again, as the whole build does
            self.dirty = True
            ok, errs = self.build_whole()
            return ok, errs, 'whole (reference assembly changed)'
        for f in QC_FILES:
            src, dst = self.p(os.path.join(QC_BIN, f)), self.p(os.path.join(HARNESS_BIN, f))
            shutil.copy2(src, dst)
            if sha(src) != sha(dst):
                raise RunnerError(f'{dst}: the copy differs from {src}')
        n = self.check_bin('the fast build path, before the replay', skip=QC_FILES)
        self.log(f'the fast build path: {QC_FILES[0]} copied, the other {n - 1} dlls of the bin the base build\'s')
        return True, [], 'questcontent'

    def build_last(self):
        """the tree's code back in the bin (the source is restored): built whole, every dll the base build's"""
        self.clean('after the last mutant')

    # ---- replay

    def replay(self, elements):
        """one replay of the elements (--fail-fast --no-gate): (exit code, lines)"""
        sel = ','.join(elements)
        if len(sel) > 30000:
            raise RunnerError(f'the selection is {len(sel)} characters: name keys, not each flow')
        code, text = self.run(['dotnet', self.p(GOLDEN_DLL), 'replay', sel, '--fail-fast', '--no-gate', '--db', self.db],
                              self.replay_timeout)
        lines = text.splitlines()
        if not any(SUMMARY.match(l) for l in lines):
            raise RunnerError(f'replay exit {code}, no summary line: {" | ".join(lines[-3:])[:400]}')
        if code != 0 and not any(l.startswith('FAIL') for l in lines):
            raise RunnerError(f'replay exit {code} and no FAIL line: {" | ".join(lines[-3:])[:400]}')
        return code, lines

    def spot(self, i, lines, picked, done):
        """('flow', name) or ('coverage', key) of the FAIL line lines[i]; RunnerError when it is neither"""
        line = lines[i]
        head = line[5:].split(':', 1)[0]
        if head in picked:
            why = line[5 + len(head) + 1:]
            if any(s in why for s in STALE_TRACE):
                raise RunnerError(f'{head}: its trace is missing or stale, the tree\'s, not the mutant\'s: {line[:200]}')
            done.add(head)
            return 'flow', head
        m = WAIVER_FAIL.match(line)
        if m and m.group(1) in picked:
            return 'flow', m.group(1)
        m = COVERAGE_FAIL.match(line)
        if m:
            return 'coverage', m.group(1)
        if line.startswith('FAIL exception:'):
            stack = [line]
            for l in lines[i + 1:]:
                if NEXT_LINE.match(l):
                    break
                stack.append(l)
            text = '\n'.join(stack)
            if DB_EXCEPTION.search(text):
                raise RunnerError(f'a database exception (no verdict): {line[:200]}')
            if not REPLAY_FRAME.search(text):
                raise RunnerError(f'an exception outside any flow\'s replay (no Program.Replay in its stack): {line[:200]}')
            rest = [n for n in picked if n not in done]
            if not rest:
                raise RunnerError(f'an exception in a replay after every flow had its line: {line[:200]}')
            done.add(rest[0])
            return 'flow', rest[0]
        raise RunnerError('a FAIL line that is no picked flow\'s and no coverage line (a lane lock, the party cleanup '
                          f'check, the item stacks, a parse, ...): {line[:200]}')

    def place(self, lines, scope_names, picked, scope_fails=None):
        """(scope or group, the first FAIL line) of a failed replay of picked (in order), every FAIL line placed (see
        the module's text). scope_fails: () -> whether the scope alone fails (a coverage line's place); None: picked
        is the scope"""
        stopped = next((m.group(1) for m in map(STOPPED.match, lines) if m), None)
        if stopped is not None and stopped not in picked:
            raise RunnerError(f'the replay stopped at {stopped}, before any flow: no verdict')
        done = {m.group(1) for m in map(FLOW_PASS.match, lines) if m}
        spots = []
        for i, l in enumerate(lines):
            m = FLOW_PASS.match(l)
            if m:
                done.add(m.group(1))
            elif l.startswith('FAIL'):
                spots.append((self.spot(i, lines, picked, done), l))
        if not spots:
            raise RunnerError('a failed replay with no FAIL line')
        (kind, what), first = spots[0]
        if stopped is not None and (kind, what) != ('flow', stopped):
            raise RunnerError(f'the replay stopped at {stopped}, but its first FAIL line is {first[:160]}')
        if kind == 'flow':
            return ('scope' if what in scope_names else 'group'), first
        if any(n not in done for n in picked):
            raise RunnerError(f'a coverage line while flows had no line: {first[:160]}')
        if scope_fails is None or all(n in scope_names for n in picked):
            return 'scope', first
        return ('scope' if scope_fails() else 'group'), first

    def scope_alone(self, scope, scope_flows, times):
        """the first FAIL line of the scope replayed alone (every FAIL line placed), or None"""
        t = time.monotonic()
        code, lines = self.replay(scope)
        times['scope'] = times.get('scope', 0) + time.monotonic() - t
        if not any(l.startswith('FAIL') for l in lines):
            return None
        return self.place(lines, set(scope_flows), scope_flows)[1]

    def combined(self, scope, group):
        rest = [g for g in group if g not in scope]
        return list(scope) + rest

    # ---- the fake checks

    def run_checks(self):
        """(built, exit code, [FAIL] lines); RunnerError when the run is no verdict (no RESULT line, or a nonzero exit
        with no [FAIL]: a crash, an --only that matches nothing)"""
        code, text = self.run(['dotnet', 'build', CHECKS] + BUILD_ARGS, BUILD_TIMEOUT)
        if code != 0:
            return False, code, []
        code, text = self.run(CHECKS_RUN + list(self.checks), CHECKS_TIMEOUT)
        lines = text.splitlines()
        chk = [l.strip() for l in lines if '[FAIL]' in l]
        result = next((l.strip() for l in lines if CHECKS_RESULT.search(l)), None)
        if result is None:
            raise RunnerError(f'the checks printed no RESULT line (exit {code}): {" | ".join(lines[-3:])[:300]}')
        if code != 0 and not chk:
            raise RunnerError(f'the checks exit {code} with no [FAIL] line: {result[:200]}')
        return True, code, chk

    # ---- the run

    def base(self, plan, prefix=''):
        """plan: the distinct (scope, group) of the mutants. Each scope alone, each mutant's exact replay, the fake
        checks: True when all pass (the first failure stops). prefix: the BASE lines' (the fallbacks' own base)"""
        scopes = []
        for s, _ in plan:
            if s not in scopes:
                scopes.append(s)
        runs = []
        for s, g in plan:
            c = self.combined(s, g)
            if c not in runs:
                runs.append(c)
        items = [(prefix + ('SC' if len(scopes) == 1 else f'SC{i + 1}'), s) for i, s in enumerate(scopes)]
        items += [(prefix + ('RUN' if len(runs) == 1 else f'RUN{i + 1}'), c) for i, c in enumerate(runs)]
        longest = 0
        for label, sel in items:
            t = time.monotonic()
            code, lines = self.replay(sel)
            took = time.monotonic() - t
            longest = max(longest, took)
            fails = [l for l in lines if l.startswith('FAIL')]
            last = lines[-1][:120] if lines else ''
            self.write(f'BASE {label}\t{len(sel)} keys\t{"FAIL " + fails[0][:150] if fails else "pass"}\t{last}')
            self.timing(f'BASE {label}\treplay\t{took:.1f}')
            self.log(f'BASE {label}: {"FAIL" if fails else "pass"} ({took:.0f} s, {len(sel)} keys)')
            if fails or code != 0:
                return False
        self.replay_timeout = max(REPLAY_TIMEOUT_MIN, 5 * longest)
        if self.checks is not None:
            t = time.monotonic()
            built, code, chk = self.run_checks()
            ok = built and code == 0 and not chk
            why = 'pass' if ok else ('FAIL checks build failed' if not built else f'FAIL exit {code} {(chk or [""])[0][:150]}')
            self.write(f'BASE {prefix}CHECKS\t{" ".join(self.checks)}\t{why}')
            self.timing(f'BASE {prefix}CHECKS\tchecks\t{time.monotonic() - t:.1f}')
            self.log(f'BASE {prefix}CHECKS: {why} ({time.monotonic() - t:.0f} s)')
            if not ok:
                return False
        self.log(f'the base passes; a replay over {self.replay_timeout:.0f} s is killed')
        return True

    def mutant(self, mid, path, old, new, scope, group, note=None):
        full = self.p(path)
        with open(full, 'rb') as f:
            orig = f.read()
        o, n = old.encode(), new.encode()
        if orig.count(o) != 1:
            self.write(f'{mid}\tBAD MUTANT: {orig.count(o)} matches')
            return 'BAD MUTANT'
        times, how = {}, None
        t0 = time.monotonic()
        self.mark(path, orig)
        try:
            write_atomic(full, orig.replace(o, n))
            t = time.monotonic()
            ok, errs, how = self.build_mutant(path)
            times['build'] = time.monotonic() - t
            if not ok:
                self.write(f'{mid}\tBUILD FAILED\t{" | ".join(errs)}')
                return 'BUILD FAILED'
            picked_elements = self.combined(scope, group)
            picked = resolve(self.names, picked_elements)
            scope_flows = resolve(self.names, scope)
            scope_names = set(scope_flows)
            t = time.monotonic()
            code, lines = self.replay(picked_elements)
            times['replay'] = time.monotonic() - t
            if any(l.startswith('FAIL') for l in lines):
                w, line = self.place(lines, scope_names, picked,
                                     lambda: self.scope_alone(scope, scope_flows, times) is not None)
                self.write(f'{mid}\tKILLED\t{w}\t{line[:160]}')
                return f'KILLED {w}'
            # review M2: a scope key's coverage a group flow reached in the combined run fails with the scope alone
            if len(scope_flows) < len(picked):
                line = self.scope_alone(scope, scope_flows, times)
                if line is not None:
                    self.write(f'{mid}\tKILLED\tscope\t{line[:160]}')
                    return 'KILLED scope'
            if self.checks is None:
                self.write(f'{mid}\tSURVIVED flows\tno checks')
                return 'SURVIVED flows'
            t = time.monotonic()
            built, _, chk = self.run_checks()
            times['checks'] = time.monotonic() - t
            if not built:
                self.write(f'{mid}\tSURVIVED flows\tchecks build failed')
                return 'SURVIVED flows'
            verdict = 'SURVIVED flows, KILLED by check' if chk else 'SURVIVED ALL'
            self.write(f'{mid}\t{verdict}\t{(chk[0][:150] if chk else "")}')
            return verdict
        finally:
            write_atomic(full, orig)
            if sha(full) != hashlib.sha256(orig).hexdigest():
                raise RunnerError(f'{path}: the restore did not take; {self.marker} kept')
            self.unmark()
            times['total'] = time.monotonic() - t0
            self.timing(f'{mid}\t' + '\t'.join(f'{k} {v:.1f}' for k, v in times.items()) + f'\tbuild-path {how}'
                        + (f'\tpath {note}' if note else ''))


class OneProcess:
    """--one-process (see the module's text): the schema, its build, the mutants in one process, the fallbacks"""

    def __init__(self, r):
        self.r = r
        self.checks_base = None     # {path in the checks' bin: sha}
        self.canaries = 0
        self.canary_secs = 0.0  # the canaries' time, kept out of the mutants' rows (each canary has its own)

    # ---- the schema

    def schema(self, ms):
        """{mid: (status, kind or reason)} of the mutants (old text once each) after the harness's schema mode;
        ms: (mid, path, old, new, n)"""
        r = self.r
        os.makedirs(r.schema_dir, exist_ok=True)
        path = os.path.join(r.schema_dir, 'mutants.json')
        with open(path, 'w', encoding='utf-8') as f:
            json.dump({'mutants': [{'id': m, 'n': n, 'path': p, 'old': o, 'new': w} for m, p, o, w, n in ms]}, f, indent=1)
        # --db although the schema mode reads no database: the golden exe never runs without the lane's
        code, text = r.run(['dotnet', r.p(GOLDEN_DLL), 'schema', path, '--out', r.schema_dir, '--db', r.db], BUILD_TIMEOUT)
        if code != 0:
            raise RunnerError(f'the schema mode exit {code}: {" | ".join(text.splitlines()[-3:])[:400]}')
        with open(os.path.join(r.schema_dir, 'schema.json'), encoding='utf-8') as f:
            doc = json.load(f)
        out = {}
        for m in doc['mutants']:
            out[m['id']] = (m['status'], m['kind'] if m['status'] == 'schema' else m['reason'])
        missing = [m for m, *_ in ms if m not in out]
        if missing:
            raise RunnerError(f'the schema mode left out {" ".join(missing[:8])}')
        self.files = doc['files']
        for line in text.splitlines():
            if line.startswith('schema: '):
                r.log(line)
        return out

    def compile_items(self):
        """the XI.QuestContent project's Compile items, in its order: (relative path, full path)"""
        r = self.r
        code, text = r.run(['dotnet', 'msbuild', QC, '-getItem:Compile', '-p:Configuration=Release', '-nologo'], BUILD_TIMEOUT)
        if code != 0:
            raise RunnerError(f'msbuild -getItem:Compile exit {code}: {text[-300:]}')
        items = json.loads(text[text.index('{'):])['Items']['Compile']
        return [(i['Identity'].replace('\\', '/'), i['FullPath']) for i in items]

    def build(self):
        """XI.QuestContent with the schema files built from a wrapper project in <out>.schema/build: the project's
        Compile items in order, each schema file in its source's place, the base build's informational version; its
        reference assembly must be the base build's (QC_REF). Returns the schema dll's and pdb's paths"""
        r = self.r
        gen = {f['path'][len(QC_DIR):]: f['gen'] for f in self.files}
        items = self.compile_items()
        used = set()
        sources = []
        for rel, full in items:
            if rel in gen:
                used.add(rel)
                sources.append(gen[rel])
            else:
                sources.append(full)
        if used != set(gen):
            raise RunnerError(f'schema files with no Compile item: {sorted(set(gen) - used)[:4]}')
        with open(r.p(QC_INFO), encoding='utf-8-sig') as f:
            m = INFO_VERSION.search(f.read())
        if not m:
            raise RunnerError(f'{QC_INFO}: no informational version')
        build = os.path.join(r.schema_dir, 'build')
        if os.path.isdir(build):
            shutil.rmtree(build)
        os.makedirs(build)
        esc = lambda t: t.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;').replace('"', '&quot;')
        proj = ['<Project Sdk="Microsoft.NET.Sdk">', '  <PropertyGroup>',
                '    <TargetFramework>net10.0</TargetFramework>', '    <Nullable>disable</Nullable>',
                '    <ImplicitUsings>disable</ImplicitUsings>', '    <LangVersion>latest</LangVersion>',
                '    <AssemblyName>XI.QuestContent</AssemblyName>', '    <RootNamespace>XI.QuestContent</RootNamespace>',
                '    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>',
                '    <EnableSourceControlManagerQueries>false</EnableSourceControlManagerQueries>',
                '    <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>',
                f'    <InformationalVersion>{esc(m.group(1))}</InformationalVersion>', '  </PropertyGroup>', '  <ItemGroup>']
        proj += [f'    <Compile Include="{esc(os.path.abspath(x))}" />' for x in sources]
        proj += ['  </ItemGroup>', '  <ItemGroup>',
                 f'    <ProjectReference Include="{esc(r.p("src/XI.Quests/XI.Quests.csproj"))}" />', '  </ItemGroup>',
                 '</Project>']
        csproj = os.path.join(build, 'XI.QuestContent.csproj')
        with open(csproj, 'w', encoding='utf-8') as f:
            f.write('\n'.join(proj) + '\n')
        code, text = r.run(['dotnet', 'build', csproj, '--no-dependencies'] + BUILD_ARGS, BUILD_TIMEOUT)
        if code != 0:
            raise RunnerError('the schema build fails: ' + ' | '.join(errors_of(text)))
        ref = os.path.join(build, 'obj/Release/net10.0/ref/XI.QuestContent.dll')
        if sha(ref) != r.base_ref:
            raise RunnerError('the schema build\'s reference assembly is not the base build\'s: a schema changed the API')
        dll = os.path.join(build, 'bin/Release/net10.0/XI.QuestContent.dll')
        return dll, dll[:-4] + '.pdb'

    def place_bins(self, dll, pdb):
        """copies of the harness's and the checks' bins (bin/Schema) with the schema dll; every dll of each copy its
        base build's but XI.QuestContent.dll, which is the schema build's"""
        r = self.r
        r.check_bin('before the schema bins')
        for src, dst, base in [(HARNESS_BIN, SCHEMA_HARNESS_BIN, r.base_bin), (CHECKS_BIN, SCHEMA_CHECKS_BIN, self.checks_base)]:
            if base is None:
                continue
            if os.path.isdir(r.p(dst)):
                shutil.rmtree(r.p(dst))
            shutil.copytree(r.p(src), r.p(dst))
            for f, x in zip(QC_FILES, (dll, pdb)):
                shutil.copy2(x, r.p(os.path.join(dst, f)))
            now = {}
            root = r.p(dst)
            for d, _, files in os.walk(root):
                for f in files:
                    if f.lower().endswith('.dll'):
                        full = os.path.join(d, f)
                        now[os.path.relpath(full, root).replace('\\', '/')] = sha(full)
            want = dict(base)
            want[QC_FILES[0]] = sha(dll)
            bad = sorted(k for k in set(now) | set(want) if now.get(k) != want.get(k))
            if bad:
                raise RunnerError(f'{dst}: {len(bad)} dll(s) are not the base build\'s (or the schema build\'s): ' + ', '.join(bad[:8]))
            r.log(f'{dst}: the schema {QC_FILES[0]}, the other {len(now) - 1} dlls the base build\'s')

    def build_checks(self):
        r = self.r
        code, text = r.run(['dotnet', 'build', CHECKS] + BUILD_ARGS, BUILD_TIMEOUT)
        if code != 0:
            raise RunnerError('the checks do not build unmutated: ' + ' | '.join(errors_of(text)))
        root, out = r.p(CHECKS_BIN), {}
        for d, _, files in os.walk(root):
            for f in files:
                if f.lower().endswith('.dll'):
                    full = os.path.join(d, f)
                    out[os.path.relpath(full, root).replace('\\', '/')] = sha(full)
        if out.get(QC_FILES[0]) != r.base_bin.get(QC_FILES[0]):
            raise RunnerError(f'{CHECKS_BIN}/{QC_FILES[0]} is not the harness\'s base build\'s')
        self.checks_base = out

    # ---- the process

    def start(self, plan):
        """the mutants process from the harness copy; the switches its class constructors reached (STATICINIT)"""
        r = self.r
        path = os.path.join(r.schema_dir, 'plan.json')
        with open(path, 'w', encoding='utf-8') as f:
            json.dump(plan, f, indent=1)
        env = {k: v for k, v in os.environ.items() if k != SWITCH_ENV}
        r.proc = r.spawn(['dotnet', r.p(SCHEMA_HARNESS_BIN + '/XI.Verify.QuestGolden.Live.dll'), 'mutants', path,
                          '--db', r.db], env)
        self.plog = open(r.out + '.process.log', 'a', encoding='utf-8')
        reached = None
        deadline = time.monotonic() + STARTUP_TIMEOUT
        while True:
            line = self.line(deadline, 'the mutants process\'s start')
            if line.startswith('STATICINIT'):
                reached = {int(x) for x in line.split()[1:]}
            elif line == 'READY':
                break
        if reached is None:
            raise RunnerError('the mutants process printed no STATICINIT line')
        return reached

    def line(self, deadline, what):
        """the process's next line (logged); a STOP line, its end or no line before the deadline stop the runner"""
        r = self.r
        try:
            line = r.proc.readline(deadline - time.monotonic())
        except TimeoutError:
            r.proc.kill()
            raise RunnerError(f'{what}: no line within the timeout: the process was killed, no verdict') from None
        if line is None:
            code = r.proc.exit_code()
            raise RunnerError(f'{what}: the mutants process ended (exit {code}) with no END: a crash, no verdict')
        self.plog.write(line + '\n')
        self.plog.flush()
        if line.startswith('FINGERPRINT-BASE '):
            r.log(line)
            return self.line(deadline, what)
        if line.startswith('STOP '):
            r.proc.exit_code()
            raise RunnerError(f'the mutants process stopped at {what}: {line[5:][:300]}')
        return line

    def canary(self, first, deadline):
        """a canary section (its BEGIN line read): it must pass, its fingerprint follow"""
        t = time.monotonic()
        deadline = max(deadline, t + self.r.replay_timeout)
        end = None
        while end is None:
            line = self.line(deadline, 'the canary')
            if line.startswith('CANARY END '):
                end = line
            elif line.startswith(('MUTANT ', 'CANARY BEGIN', 'FINGERPRINT')):
                raise RunnerError(f'a canary with no END: {line[:160]}')
        if not end.endswith(' pass'):
            raise RunnerError(f'the canary failed: {end[:200]}')
        line = self.line(deadline, 'the canary')
        if not line.startswith('FINGERPRINT CANARY '):
            raise RunnerError(f'no fingerprint after the canary: {line[:160]}')
        self.canaries += 1
        self.canary_secs += time.monotonic() - t
        self.r.timing(f'CANARY\t{first[len("CANARY BEGIN after "):]}\treplay {time.monotonic() - t:.1f}')

    def block(self, tag, n, elements):
        """the elements replayed in the process with switch n (0: none), as `replay <them> --fail-fast --no-gate`:
        (exit code as the replay's, its lines); every other outcome stops the runner"""
        r = self.r
        r.proc.send(f'RUN {tag} {n} {",".join(elements)}')
        deadline = time.monotonic() + r.replay_timeout
        begin, end_head = f'MUTANT {tag} BEGIN a={n}', f'MUTANT {tag} END '
        while True:
            line = self.line(deadline, tag)
            if line.startswith('CANARY BEGIN'):
                self.canary(line, deadline)
                deadline = time.monotonic() + r.replay_timeout
                continue
            if n and line.startswith(f'VARIANT-THREW {tag} '):
                why = line[len(f'VARIANT-THREW {tag} '):]
                line = self.line(deadline, tag)
                if not line.startswith(f'FINGERPRINT {tag} '):
                    raise RunnerError(f'{tag}: no FINGERPRINT line after its VARIANT-THREW: {line[:160]}')
                raise VariantThrew(why)
            if line != begin:
                raise RunnerError(f'{tag}: {begin} expected, not {line[:160]}')
            break
        lines, end = [], None
        while end is None:
            line = self.line(deadline, tag)
            if line.startswith(end_head):
                end = line[len(end_head):]
            elif line.startswith(('MUTANT ', 'CANARY ', 'FINGERPRINT ', 'READY', 'STATICINIT')) or line == 'BYE':
                raise RunnerError(f'{tag}: a block with no END: {line[:160]}')
            else:
                lines.append(line)
        line = self.line(deadline, tag)
        if not line.startswith(f'FINGERPRINT {tag} '):
            raise RunnerError(f'{tag}: no FINGERPRINT line after its END: {line[:160]}')
        m = SUMMARY.match(end)
        if not m or not lines or lines[-1] != end:
            raise RunnerError(f'{tag}: its END ({end[:100]}) is no summary line, or not its last line')
        # review M3: as Runner.replay's nonzero exit with no FAIL line
        if m.group(2) != '0' and not any(l.startswith('FAIL') for l in lines):
            raise RunnerError(f'{tag}: {m.group(2)} failed and no FAIL line: {" | ".join(lines[-3:])[:400]}')
        return (0 if m.group(2) == '0' else 1), lines

    def quit(self):
        """QUIT: the last canary, BYE, exit 0"""
        r = self.r
        r.proc.send('QUIT')
        deadline = time.monotonic() + r.replay_timeout
        while True:
            line = self.line(deadline, 'QUIT')
            if line.startswith('CANARY BEGIN'):
                self.canary(line, deadline)
                continue
            if line != 'BYE':
                raise RunnerError(f'QUIT: BYE expected, not {line[:160]}')
            break
        code = r.proc.exit_code()
        r.proc = None
        if code != 0:
            raise RunnerError(f'the mutants process exit {code} after BYE')

    def replay_in(self, tag, n):
        """a Runner.replay stand-in: the elements as a block of the process"""
        return lambda elements: self.block(tag, n, elements)

    # ---- the checks from the checks copy

    def run_checks(self, n):
        """Runner.run_checks from the checks copy, XI_MUTANT=<n> (None: unset): (True, exit code, [FAIL] lines)"""
        r = self.r
        env = {k: v for k, v in os.environ.items() if k != SWITCH_ENV}
        if n:
            env[SWITCH_ENV] = str(n)
        code, text = r.run(['dotnet', r.p(SCHEMA_CHECKS_BIN + '/' + CHECKS_DLL)] + list(r.checks), CHECKS_TIMEOUT, env)
        lines = text.splitlines()
        chk = [l.strip() for l in lines if '[FAIL]' in l]
        result = next((l.strip() for l in lines if CHECKS_RESULT.search(l)), None)
        if result is None:
            raise RunnerError(f'the checks printed no RESULT line (exit {code}): {" | ".join(lines[-3:])[:300]}')
        if code != 0 and not chk:
            raise RunnerError(f'the checks exit {code} with no [FAIL] line: {result[:200]}')
        return True, code, chk

    # ---- the base and the mutants

    def base(self, plan):
        """Runner.base in the process (BASE lines as there), the checks from the checks copy: True when all pass"""
        r = self.r
        scopes, runs = [], []
        for s_, g in plan:
            if s_ not in scopes:
                scopes.append(s_)
            c = r.combined(s_, g)
            if c not in runs:
                runs.append(c)
        items = [('SC' if len(scopes) == 1 else f'SC{i + 1}', s_) for i, s_ in enumerate(scopes)]
        items += [('RUN' if len(runs) == 1 else f'RUN{i + 1}', c) for i, c in enumerate(runs)]
        longest = 0
        for label, sel in items:
            t = time.monotonic()
            code, lines = self.block(f'BASE/{label}', 0, sel)
            took = time.monotonic() - t
            longest = max(longest, took)
            fails = [l for l in lines if l.startswith('FAIL')]
            last = lines[-1][:120] if lines else ''
            r.write(f'BASE {label}\t{len(sel)} keys\t{"FAIL " + fails[0][:150] if fails else "pass"}\t{last}')
            r.timing(f'BASE {label}\treplay\t{took:.1f}\tpath one-process')
            r.log(f'BASE {label}: {"FAIL" if fails else "pass"} ({took:.0f} s, {len(sel)} keys, one process)')
            if fails or code != 0:
                return False
        r.replay_timeout = max(REPLAY_TIMEOUT_MIN, 5 * longest)
        if r.checks is not None:
            t = time.monotonic()
            _, code, chk = self.run_checks(None)
            ok = code == 0 and not chk
            why = 'pass' if ok else f'FAIL exit {code} {(chk or [""])[0][:150]}'
            r.write(f'BASE CHECKS\t{" ".join(r.checks)}\t{why}')
            r.timing(f'BASE CHECKS\tchecks\t{time.monotonic() - t:.1f}\tpath one-process')
            r.log(f'BASE CHECKS: {why} ({time.monotonic() - t:.0f} s, the checks copy)')
            if not ok:
                return False
        r.log(f'the base passes in the process; a block over {r.replay_timeout:.0f} s is killed')
        return True

    def mutant(self, mid, n, scope, group):
        """Runner.mutant's verdict, its replays blocks of the process and its checks from the checks copy"""
        r = self.r
        times = {}
        t0, c0 = time.monotonic(), self.canary_secs
        threw = False

        def since(t, c):
            """the seconds since t, a canary run in them aside"""
            return time.monotonic() - t - (self.canary_secs - c)

        try:
            picked_elements = r.combined(scope, group)
            picked = resolve(r.names, picked_elements)
            scope_flows = resolve(r.names, scope)
            scope_names = set(scope_flows)

            def alone():
                t, c = time.monotonic(), self.canary_secs
                code, lines = self.block(f'{mid}/scope', n, scope)
                times['scope'] = times.get('scope', 0) + since(t, c)
                if not any(l.startswith('FAIL') for l in lines):
                    return None
                return r.place(lines, set(scope_flows), scope_flows)[1]

            t, c = time.monotonic(), self.canary_secs
            code, lines = self.block(mid, n, picked_elements)
            times['replay'] = since(t, c)
            if any(l.startswith('FAIL') for l in lines):
                w, line = r.place(lines, scope_names, picked, lambda: alone() is not None)
                r.write(f'{mid}\tKILLED\t{w}\t{line[:160]}')
                return f'KILLED {w}'
            if len(scope_flows) < len(picked):
                line = alone()
                if line is not None:
                    r.write(f'{mid}\tKILLED\tscope\t{line[:160]}')
                    return 'KILLED scope'
            if r.checks is None:
                r.write(f'{mid}\tSURVIVED flows\tno checks')
                return 'SURVIVED flows'
            t = time.monotonic()
            _, _, chk = self.run_checks(n)
            times['checks'] = time.monotonic() - t
            verdict = 'SURVIVED flows, KILLED by check' if chk else 'SURVIVED ALL'
            r.write(f'{mid}\t{verdict}\t{(chk[0][:150] if chk else "")}')
            return verdict
        except VariantThrew:
            threw = True
            raise
        finally:
            if not threw:
                times['total'] = since(t0, c0)
                r.timing(f'{mid}\t' + '\t'.join(f'{k} {v:.1f}' for k, v in times.items()) + '\tpath one-process')

    def run(self, sel, groups_of, no_base, schema_only=False):
        """the run (see the module's text): the exit code; RunnerError stops"""
        r = self.r
        ms, bad = [], []
        numbers = {}
        for k, (mid, path, old, new, sc) in enumerate(sel):
            numbers[mid] = k + 1
            with open(r.p(path), 'rb') as f:
                count = f.read().count(old.encode())
            if count != 1:
                bad.append((mid, count))
            else:
                ms.append((mid, path, old, new, k + 1))
        status = self.schema(ms) if ms else {}
        schema = [m for m in sel if status.get(m[0], ('',))[0] == 'schema']
        fallback = [(m, status[m[0]][1]) for m in sel if status.get(m[0], ('',))[0] == 'fallback']
        bad += [(m[0], status[m[0]][1]) for m in sel if status.get(m[0], ('',))[0] == 'bad']
        r.log(f'one process: {len(schema)} of {len(sel)} mutants in the schema, {len(fallback)} fall back, {len(bad)} bad')
        if schema_only:
            return 0
        for mid, count in bad:
            if isinstance(count, int):
                r.write(f'{mid}\tBAD MUTANT: {count} matches')
            else:
                r.write(f'{mid}\tBAD MUTANT: {count}')
            print(f'{mid}: BAD MUTANT', flush=True)
        if r.checks is not None:
            self.build_checks()
        done = 0
        if schema:
            dll, pdb = self.build()
            self.place_bins(dll, pdb)
            plan_pairs = []
            for m in schema:
                pg = (m[4], groups_of(m[4]))
                if pg not in plan_pairs:
                    plan_pairs.append(pg)
            runs = [r.combined(s_, g) for s_, g in plan_pairs]
            union = []
            for s_, g in plan_pairs:
                for e in list(s_) + list(r.combined(s_, g)):
                    if e not in union:
                        union.append(e)
            plan = {'union': ','.join(union), 'canary': ','.join(runs[0]),
                    'mutants': {m[0]: numbers[m[0]] for m in schema},
                    'variants': [numbers[m[0]] for m in schema if status[m[0]][1] == 'static-variant'],
                    'canary_every': CANARY_EVERY, 'heap_ceiling_mb': HEAP_CEILING_MB}
            try:
                reached = self.start(plan)
                late = [m for m in schema if numbers[m[0]] in reached]
                if late:
                    schema = [m for m in schema if numbers[m[0]] not in reached]
                    fallback += [(m, 'its switch runs while the class constructors or the setup run (STATICINIT)')
                                 for m in late]
                    r.log(f'STATICINIT: {" ".join(m[0] for m in late)} fall back')
                if no_base:
                    print('WARNING --no-base: no base run. A failure the unmutated tree has (a flow, the combined order, a '
                          'check) is then counted as a kill: only for a rerun of a few ids on a tree whose base passed', flush=True)
                    r.write('BASE\tskipped (--no-base)')
                    # the process's first blocks are unmutated: its fingerprints' base, and the switches each
                    # selection reaches unmutated (a passing mutant's block must reach its own as they did)
                    scopes = []
                    for s_, _ in plan_pairs:
                        if s_ not in scopes:
                            scopes.append(s_)
                    for i, sel_ in enumerate(scopes + [c for c in runs if c not in scopes]):
                        self.block(f'WARMUP{i + 1}', 0, sel_)
                elif not self.base(plan_pairs):
                    print('the base fails: no mutant run')
                    r.proc.kill()
                    return 1
                for m in schema:
                    mid, sc = m[0], m[4]
                    try:
                        verdict = self.mutant(mid, numbers[mid], sc, groups_of(sc))
                    except VariantThrew as e:
                        fallback.append((m, f'its static variant throws (VARIANT-THREW {e})'[:300]))
                        r.log(f'VARIANT-THREW {mid}: falls back ({e})')
                        continue
                    done += 1
                    print(f'{mid}: {verdict}', flush=True)
                self.quit()
            finally:
                if r.proc is not None:
                    r.proc.kill()
                    r.proc = None
                if getattr(self, 'plog', None):
                    self.plog.close()
            r.log(f'one process: {done} mutants, {self.canaries} canaries passed')
        if fallback:
            if not no_base:
                plan_pairs = []
                for m, _ in fallback:
                    pg = (m[4], groups_of(m[4]))
                    if pg not in plan_pairs:
                        plan_pairs.append(pg)
                if not r.base(plan_pairs, prefix='FALLBACK '):
                    print('the fallbacks\' base fails: no fallback run')
                    return 1
            for (mid, path, old, new, sc), why in fallback:
                if r.dirty:
                    r.clean(f'before {mid}')
                verdict = r.mutant(mid, path, old, new, sc, groups_of(sc), note=f'fallback {why}')
                print(f'{mid}: {verdict} (fallback: {why})', flush=True)
        return 0


MULTI_SCOPE_WARNING = ('WARNING --one-process with more than one scope: a cache filled by one scope base block and '
                       'read only by another scope is not detected by the reach check; equivalence was proven on '
                       'single-scope runs (no such cache exists today; one process per scope would close it)')


def main(wt, out, mutants, scope, group, db, checks=None, argv=(), runner=Runner):
    """the lane script's entry (see the module's text). Returns the exit code: 0, or 1 when the base failed or the
    runner stopped (RunnerError)"""
    flags = ('--base', 'BASE', '--no-base', '--one-process', '--schema-only')
    args = [a for a in argv if a not in flags]
    no_base = '--no-base' in argv
    one, schema_only = '--one-process' in argv, '--schema-only' in argv
    if no_base and ('--base' in argv or 'BASE' in argv):
        print('--base and --no-base: one or the other')
        return 1
    ms = [m if len(m) == 5 else (*m, scope) for m in mutants]
    groups_of = group if callable(group) else (lambda s: group)
    ids = [m[0] for m in ms] if 'ALLIDS' in args else args
    unknown = [i for i in ids if i not in {m[0] for m in ms}]
    if unknown:
        print(f'unknown mutant ids: {" ".join(unknown)}')
        return 1
    sel = [m for m in ms if m[0] in ids]
    if not sel and no_base:
        print('--no-base and no mutant ids: nothing to run')
        return 1
    r = runner(wt, out, db, checks)
    if one and len({(tuple(m[4]), tuple(groups_of(m[4]))) for m in sel}) > 1:
        print(MULTI_SCOPE_WARNING)
    if one or schema_only:
        return one_process(r, sel or ms if schema_only else sel, ms, groups_of, no_base, schema_only)
    try:
        r.check_marker()
        r.build_first()
        if no_base:
            print('WARNING --no-base: no base run. A failure the unmutated tree has (a flow, the combined order, a '
                  'check) is then counted as a kill: only for a rerun of a few ids on a tree whose base passed', flush=True)
            r.write('BASE\tskipped (--no-base)')
        else:
            plan = []
            for m in (sel or ms):
                pg = (m[4], groups_of(m[4]))
                if pg not in plan:
                    plan.append(pg)
            if not r.base(plan):
                print('the base fails: no mutant run')
                return 1
        stop = None
        try:
            for mid, path, old, new, sc in sel:
                if r.dirty:
                    r.clean(f'before {mid}')
                verdict = r.mutant(mid, path, old, new, sc, groups_of(sc))
                print(f'{mid}: {verdict}', flush=True)
        except BaseException as e:
            stop = e
        if sel:
            try:
                r.build_last()
            except RunnerError:
                if stop is not None:
                    print(f'runner stopped: {stop}')
                raise
        if stop is not None:
            raise stop
    except RunnerError as e:
        print(f'runner stopped: {e}')
        return 1
    return 0


def one_process(r, sel, ms, groups_of, no_base, schema_only):
    """main()'s --one-process and --schema-only (see the module's text)"""
    if not sel and not schema_only:
        # the base alone (no ids) is option 1's: the one-process base needs the schema of the mutants it guards
        print('--one-process and no mutant ids: nothing to run (the base alone: without --one-process)')
        return 1
    try:
        r.check_marker()
        r.build_first()
        stop = None
        try:
            code = OneProcess(r).run(sel, groups_of, no_base, schema_only)
        except BaseException as e:
            stop, code = e, 1
        if sel and not schema_only:
            try:
                r.build_last()
            except RunnerError:
                if stop is not None:
                    print(f'runner stopped: {stop}')
                raise
        if stop is not None:
            raise stop
        return code
    except RunnerError as e:
        print(f'runner stopped: {e}')
        return 1


# ---- the self-test --------------------------------------------------------------------------------------------------

def selftest(tmp_root=None):
    """the selector, the kill's place on made-up outputs, and the whole loop on a made-up worktree with a fake dotnet
    (FakeRunner): the build paths and the bin checks (C1), the stops (I1, M4, timeouts), the base (I2), the scope alone
    (M2), the restore and the marker (M6). No build, no database"""
    bad = []

    def check(what, want, got):
        if want != got:
            bad.append(f'{what}: want {want!r}, got {got!r}')

    def raises(what, fn, text=''):
        try:
            fn()
            bad.append(f'{what}: no RunnerError')
        except RunnerError as e:
            if text not in str(e):
                bad.append(f'{what}: RunnerError without {text!r}: {e}')

    names = ['a#x', 'a', 'b#1', 'b#2', 'c']
    r = Runner.__new__(Runner)
    r.names = names
    check('resolve: a key in name order, flow: one, each once', ['b#1', 'b#2', 'a#x', 'c'],
          resolve(names, ['b', 'flow:a#x', 'flow:b#2', 'c']))
    raises('resolve: an unknown element', lambda: resolve(names, ['zz']), 'no flow of that name')
    check('combined: the scope first, then the group\'s others in its order', ['flow:c', 'a', 'b'],
          r.combined(['flow:c', 'a'], ['a', 'b', 'flow:c']))

    picked, scope = ['a#x', 'a', 'b#1', 'b#2'], {'a#x', 'a'}
    asked = []

    def place(lines, alone=False):
        def scope_fails():
            asked.append(alone)
            return alone
        return r.place(lines, scope, picked, scope_fails)[0]

    ok_all = [f'PASS {n}: 1 steps as recorded (lua)' for n in picked]
    summary = '4 passed, 1 failed (9 s)'
    check('a scope flow stopped the run', 'scope', place(
        ['PASS a#x: 3 steps as recorded (lua)', 'FAIL a: x', 'fail-fast: stopped at a, the first failure; ran 2 of 4']))
    check('a group flow stopped the run', 'group', place(
        ['PASS a#x: 3 steps as recorded (lua)', 'PASS a: 2 steps as recorded (lua)', 'FAIL b#1: ',
         '    step 3: differs', 'fail-fast: stopped at b#1, the first failure; ran 3 of 4']))
    check('an exception in a group flow\'s replay (the first flow with no line)', 'group', place(
        ['PASS a#x: 3 steps as recorded (lua)', 'PASS a: 2 steps as recorded (lua)',
         'FAIL exception: System.InvalidOperationException: boom', '   at Program.RunFlowBody(Flow f)',
         '   at Program.Replay(Flow f, Int32 index, PacketStore store) in Program.cs:line 2116', summary]))
    check('an exception in the first flow\'s replay', 'scope', place(
        ['FAIL exception: System.Exception: boom', '   at Program.Replay(Flow f, Int32 index, PacketStore store)']))
    asked.clear()
    check('a coverage line, the scope alone fails too: scope', 'scope',
          place(ok_all + ['FAIL coverage a: 1/X/onTrigger reached by no trace'], alone=True))
    check('a coverage line, the scope alone passes: group (a group flow caused it)', 'group',
          place(ok_all + ['FAIL coverage a: waiver 1/X/onTrigger is reached by b#1'], alone=False))
    check('a coverage line asks the scope alone (each time)', [True, False], asked)
    check('a group flow\'s waiver line (FAIL <file>.flow:<n>: unknown handler) is the group\'s', 'group',
          place(ok_all + ['FAIL b#2.flow:7: unknown handler 1/X/onTrigger']))
    check('a scope flow\'s waiver line is the scope\'s', 'scope',
          place(ok_all + ['FAIL a#x.flow:4: unknown handler 1/X/onTrigger']))
    check('a coverage line of a run of the scope alone: scope, nothing asked', ('scope', 'FAIL coverage a: x'),
          r.place(['PASS a#x: 1 steps as recorded (lua)', 'PASS a: 1 steps as recorded (lua)', 'FAIL coverage a: x'],
                  scope, ['a#x', 'a']))
    for what, lines, text in [
        ('a lane lock conflict', ['FAIL char 5001 is in use by another QuestGolden run on xidb_lane7 (golden-x-5001)',
                                  '0 passed, 1 failed (1 s)'], 'no picked flow'),
        ('the party cleanup check (--no-gate)', ['FAIL gate: Cleanup drops a led party\'s rows of another char: x',
                                                 'fail-fast: stopped at the party cleanup check (--no-gate), the first '
                                                 'failure; ran 0 of 4 flows'], 'before any flow'),
        ('the startup gate', ['FAIL gate: x', 'fail-fast: stopped at the startup gate, the first failure; ran 0 of 4'],
         'before any flow'),
        ('the item stacks', ['FAIL item stacks: a.flow:3: [item 2012] x'], 'no picked flow'),
        ('a parse', ['FAIL parse: a.flow:3: x'], 'no picked flow'),
        ('an unknown line after a flow\'s', ok_all[:1] + ['FAIL something new: x'], 'no picked flow'),
        ('an exception outside a flow', ['FAIL exception: System.Exception: x', '   at Program.Setup(MySqlDatabase db)'],
         'outside any flow'),
        ('a database exception in a flow', ok_all[:2] + ['FAIL exception: MySqlConnector.MySqlException (0x80004005): '
                                                         'Connect Timeout expired', '   at Program.Replay(Flow f)'],
         'database exception'),
        ('a missing trace', ['FAIL a#x: no trace; record it'], 'missing or stale'),
        ('a stale trace', ['FAIL a#x: a#x: the flow changed since it was recorded; record it again'], 'missing or stale'),
        ('a coverage line while a flow had no line', ok_all[:3] + ['FAIL coverage a: x'], 'had no line'),
        ('the fail-fast flow is not the first FAIL line\'s', ['FAIL coverage a: x', 'fail-fast: stopped at a, the first '
                                                             'failure;'], 'first FAIL line'),
    ]:
        raises(f'place: {what} stops the runner', lambda lines=lines: place(lines), text)

    root = tempfile.mkdtemp(prefix='golden_mutants_selftest_', dir=tmp_root)
    try:
        selftest_loop(root, check, raises)
    finally:
        shutil.rmtree(root, ignore_errors=True)
    for b in bad:
        print('FAIL ' + b)
    print(f'golden_mutants selftest: {"ok" if not bad else f"{len(bad)} failed"}')
    return 1 if bad else 0


class FakeRunner(Runner):
    """Runner on a made-up worktree whose dotnet is execute(): a whole build compiles XI.Quests (B.cs) and
    XI.QuestContent (Q.cs, against XI.Quests' last output) and copies them into the harness bin; the QuestContent
    build alone compiles against XI.Quests' LAST output, stale or not, as --no-dependencies does. A replay's flows
    fail by the markers in the source and the bin (a mutated XI.Quests.dll in the bin fails b#1); the checks by
    markers in Q.cs"""
    log_lines = []

    def __init__(self, *a, **k):
        super().__init__(*a, **k, log=self.log_lines.append)
        self.seen = []          # (selection, XI.Quests.dll in the bin, Q.cs) of each replay
        self.quests_out = None  # XI.Quests' last build output
        self.check_builds = 0
        self.schema_runs, self.spawned, self.schema_checks, self.canaries_run = 0, [], [], 0

    def src(self, rel):
        with open(self.p(rel), encoding='utf-8') as f:
            return f.read()

    def put(self, rel, data):
        os.makedirs(os.path.dirname(self.p(rel)), exist_ok=True)
        with open(self.p(rel), 'w', encoding='utf-8') as f:
            f.write(data)

    def compile_qc(self):
        q = self.src('src/XI.QuestContent/Q.cs')
        if 'BROKEN' in q:
            return 1, 'Q.cs(1,1): error CS1002: ; expected\nQ.cs(2,1): error CS1513: } expected'
        self.put(QC_BIN + '/XI.QuestContent.dll', 'qc ' + q + ' against ' + self.quests_out)
        self.put(QC_BIN + '/XI.QuestContent.pdb', 'pdb ' + q)
        self.put(QC_REF, 'ref ' + ''.join(l for l in q.splitlines() if l.startswith('public')))
        self.put(QC_INFO, '[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+fake")]')
        return 0, ''

    def execute(self, cmd, timeout, env=None):
        q = self.src('src/XI.QuestContent/Q.cs')
        if cmd[:3] == ['dotnet', 'build', HARNESS]:
            self.quests_out = 'quests ' + self.src('src/XI.Quests/B.cs')
            code, text = self.compile_qc()
            if code:
                return code, text
            for f in QC_FILES:
                shutil.copy2(self.p(QC_BIN + '/' + f), self.p(HARNESS_BIN + '/' + f))
            self.put(HARNESS_BIN + '/XI.Quests.dll', self.quests_out)
            self.put(HARNESS_BIN + '/XI.Verify.QuestGolden.Live.dll', 'harness against ' + self.src(QC_REF))
            self.put(HARNESS_BIN + '/runtimes/win-x64/native/x.dll', 'native')
            return 0, 'Build succeeded.'
        if cmd[:3] == ['dotnet', 'build', QC]:
            return self.compile_qc()
        if cmd[:3] == ['dotnet', 'build', CHECKS]:
            self.check_builds += 1
            self.put(CHECKS_BIN + '/XI.Verify.Quests.dll', 'checks')
            shutil.copy2(self.p(HARNESS_BIN + '/XI.QuestContent.dll'), self.p(CHECKS_BIN + '/XI.QuestContent.dll'))
            return 0, ''
        if cmd[:len(CHECKS_RUN)] == CHECKS_RUN:
            if 'CHECKCRASH' in q:
                return 134, 'Unhandled exception. System.Exception: x'
            if 'NOMATCH' in q:
                return 1, 'RESULT: 0 passed, 0 failed  [filtered by --only "zz": 0 checks ran; no check matched]'
            if 'CHECKFAIL' in q or 'CHECKFAIL' in self.src('src/XI.Quests/B.cs'):
                return 1, '  [FAIL] c3 C3b: a check\nRESULT: 4 passed, 1 failed'
            return 0, 'RESULT: 5 passed, 0 failed'
        if cmd[2] == 'replay':
            if 'HANG' in q:
                raise subprocess.TimeoutExpired(cmd, timeout)
            quests = open(self.p(HARNESS_BIN + '/XI.Quests.dll'), encoding='utf-8').read()
            self.seen.append((cmd[3], quests, q))
            return self.fake_replay(cmd[3].split(','), q, quests)
        if len(cmd) > 2 and cmd[2] == 'schema':
            return self.fake_schema(cmd[3], cmd[5])
        if cmd[:3] == ['dotnet', 'msbuild', QC]:
            return 0, json.dumps({'Items': {'Compile': [{'Identity': 'Q.cs', 'FullPath': self.p('src/XI.QuestContent/Q.cs')}]}})
        if cmd[:2] == ['dotnet', 'build'] and cmd[2].endswith('XI.QuestContent.csproj') and self.schema_dir in cmd[2]:
            return self.fake_schema_build(cmd[2])
        if cmd[:2] == ['dotnet', self.p(SCHEMA_CHECKS_BIN + '/' + CHECKS_DLL)]:
            n = (env or {}).get(SWITCH_ENV)
            patched = self.schema_patched(SCHEMA_CHECKS_BIN, int(n)) if n else self.schema_patched(SCHEMA_CHECKS_BIN, 0)
            self.schema_checks.append(n)
            if 'CHECKFAIL' in patched:
                return 1, '  [FAIL] c3 C3b: a check\nRESULT: 4 passed, 1 failed'
            return 0, 'RESULT: 5 passed, 0 failed'
        raise AssertionError(f'fake dotnet: {cmd}')

    def fake_replay(self, elements, q, quests):
        """a replay of the elements on the source q and the bin's XI.Quests quests: (exit code, its output)"""
        flows = resolve(self.names, elements)
        if 'LOCK' in q:
            return 1, 'FAIL char 5001 is in use by another QuestGolden run on xidb_lane7\n0 passed, 1 failed (0 s)'
        if 'PARTY' in q:
            return 1, ('SKIP the startup gate (--no-gate)\nFAIL gate: Cleanup drops a led party\'s rows: x\n'
                       'fail-fast: stopped at the party cleanup check (--no-gate), the first failure; ran 0 of '
                       f'{len(flows)} flows\n0 passed, 1 failed (1 s)')
        out = ['lane xidb_lane7']
        for n in flows:
            if (n == 'b#1' and ('y=1' not in quests or 'KILL_B1' in q)) or (n == 'a' and 'KILL_A' in q):
                out += [f'FAIL {n}: ', '    step 2: differs',
                        f'fail-fast: stopped at {n}, the first failure; ran 1 of {len(flows)} flows',
                        f'0 passed, 1 failed (1 s)']
                return 1, '\n'.join(out)
            out.append(f'PASS {n}: 3 steps as recorded (lua)')
        if 'COV_MASK' in q and not any(n.startswith('b') for n in flows):
            out.append('FAIL coverage a: 1/X/onTrigger reached by no trace')
        if 'COV_GROUP' in q and any(n.startswith('b') for n in flows):
            out.append('FAIL coverage a: waiver 1/X/onTrigger is reached by b#1')
        fails = sum(l.startswith('FAIL') for l in out)
        out.append(f'{len(flows)} passed, {fails} failed (2 s)')
        return (1 if fails else 0), '\n'.join(out)

    # the schema mode: a mutant whose new text says FALLBACK falls back; the schema file is the source and each
    # schema mutant's (old, new), which the fake build carries into its dll and the fake process reads back
    def fake_schema(self, path, out_dir):
        with open(path, encoding='utf-8') as f:
            ms = json.load(f)['mutants']
        os.makedirs(os.path.join(out_dir, 'gen'), exist_ok=True)
        gen = os.path.join(out_dir, 'gen', 'Q.cs')
        keep = {m['n']: (m['old'], m['new']) for m in ms if 'FALLBACK' not in m['new']}
        with open(gen, 'w', encoding='utf-8') as f:
            json.dump({'orig': self.src('src/XI.QuestContent/Q.cs'), 'muts': keep}, f)
        doc = {'mutants': [{'id': m['id'], 'n': m['n'], 'path': m['path'],
                            'status': 'fallback' if 'FALLBACK' in m['new'] else 'schema',
                            'kind': None if 'FALLBACK' in m['new'] else
                            'static-variant' if 'VARIANT' in m['new'] else 'expression',
                            'reason': 'fake fallback' if 'FALLBACK' in m['new'] else None, 'climbs': 0} for m in ms],
               'files': [{'path': 'src/XI.QuestContent/Q.cs', 'gen': gen}] if keep else []}
        with open(os.path.join(out_dir, 'schema.json'), 'w', encoding='utf-8') as f:
            json.dump(doc, f)
        self.schema_runs += 1
        return 0, f'schema: {len(keep)} of {len(ms)} mutants in a schema'

    def fake_schema_build(self, csproj):
        text = open(csproj, encoding='utf-8').read()
        gen = re.search(r'<Compile Include="([^"]+)"', text).group(1)
        body = open(gen, encoding='utf-8').read()
        build = os.path.dirname(csproj)
        orig = json.loads(body)['orig']
        public = ''.join(l for l in orig.splitlines() if l.startswith('public'))
        self.put(os.path.relpath(os.path.join(build, 'obj/Release/net10.0/ref/XI.QuestContent.dll'), self.wt),
                 'ref ' + public + (' changed' if 'REFDIFF' in body else ''))
        self.put(os.path.relpath(os.path.join(build, 'bin/Release/net10.0/XI.QuestContent.dll'), self.wt), 'qc-schema ' + body)
        self.put(os.path.relpath(os.path.join(build, 'bin/Release/net10.0/XI.QuestContent.pdb'), self.wt), 'pdb')
        return 0, 'Build succeeded.'

    def schema_patched(self, bin_dir, n):
        """the source as mutant n patches it, read from the schema dll in bin_dir (0: the source)"""
        doc = json.loads(open(self.p(bin_dir + '/XI.QuestContent.dll'), encoding='utf-8').read()[len('qc-schema '):])
        if not n:
            return doc['orig']
        old, new = doc['muts'][str(n)]
        return doc['orig'].replace(old, new)

    def spawn(self, cmd, env):
        if SWITCH_ENV in env:
            raise AssertionError('the mutants process gets no XI_MUTANT')
        self.spawned.append(cmd)
        return FakeMutantProcess(self, cmd)


class FakeMutantProcess:
    """the mutants mode on the fake worktree: a block replays as fake_replay on the source as the switched mutant
    patches it (read from the schema copy's dll); markers in a mutant's new text: CRASH (gone after BEGIN), HANG (no
    line after BEGIN), NOEND (BYE inside the block), FPSTOP (a STOP after its fingerprint), CANARYFAIL (the canary after
    it fails), STATICINIT (reported at the start), VARIANT (a static variant: the plan must say so), VARIANTTHREW (its
    variant throws: VARIANT-THREW, no block), NOFAILLINE (a failure counted with no FAIL line)"""

    def __init__(self, r, cmd):
        import collections
        self.r, self.out, self.ended, self.code, self.since, self.count, self.last = r, collections.deque(), False, None, [], 0, 0
        with open(cmd[3], encoding='utf-8') as f:
            self.plan = json.load(f)
        self.quests = open(r.p(SCHEMA_HARNESS_BIN + '/XI.Quests.dll'), encoding='utf-8').read()
        doc = json.loads(open(r.p(SCHEMA_HARNESS_BIN + '/XI.QuestContent.dll'), encoding='utf-8').read()[len('qc-schema '):])
        reached = sorted(int(n) for n, (_, new) in doc['muts'].items() if 'STATICINIT' in new)
        self.variants = sorted(int(n) for n, (_, new) in doc['muts'].items() if 'VARIANT' in new)
        self.out.extend(['lane xidb_lane7', 'STATICINIT' + ''.join(f' {n}' for n in reached)])
        if sorted(self.plan['variants']) != self.variants:
            self.out.append(f'STOP the schema\'s static variants ({self.variants}) are not the plan\'s')
            self.end(2)
            return
        self.out.append('READY')

    def end(self, code):
        self.ended, self.code = True, code

    def run(self, n, sel):
        patched = self.r.schema_patched(SCHEMA_HARNESS_BIN, n)
        self.r.seen.append((sel, self.quests, patched))
        _, text = self.r.fake_replay(sel.split(','), patched, self.quests)
        return patched, text.splitlines()

    def canary(self, fail):
        self.out.append(f'CANARY BEGIN after {",".join(self.since)}')
        _, lines = self.run(0, self.plan['canary'])
        self.out.extend(lines)
        self.out.append(f'CANARY END {lines[-1]} {"FAIL" if fail else "pass"}')
        if fail:
            self.out.append(f'STOP the canary failed: rerun {",".join(self.since)} with option 1')
            return self.end(2)
        self.out.append('FINGERPRINT CANARY static=1 db=1 rows=0 heap=1 ws=1 (1 ms)')
        self.since = []
        self.r.canaries_run += 1

    def send(self, line):
        if self.ended:
            return
        if line == 'QUIT':
            if self.since:
                self.canary(False)
            if not self.ended:
                self.out.append('BYE')
                self.end(0)
            return
        _, tag, n, sel = line.split(' ', 3)
        n = int(n)
        # the canary before a new mutant's first block once every canary_every mutants ran (the mutants mode's rule)
        if n and n != self.last:
            if self.count and self.count % self.plan['canary_every'] == 0 and self.since:
                self.canary(False)
            self.count += 1
            self.last = n
        if n and 'VARIANTTHREW' in self.r.schema_patched(SCHEMA_HARNESS_BIN, n):
            self.out.extend([f'VARIANT-THREW {tag} Q.F: InvalidOperationException: x',
                             f'FINGERPRINT {tag} static=1 db=1 rows=0 heap=1 ws=1 (1 ms)'])
            self.since.append(tag)
            return
        self.out.append(f'MUTANT {tag} BEGIN a={n}')
        patched, lines = self.run(n, sel)
        if 'NOFAILLINE' in patched:
            lines = [l for l in lines if not l.startswith('FAIL')][:-1] + ['0 passed, 1 failed (1 s)']
        if 'CRASH' in patched:
            return self.end(134)
        if 'HANG' in patched:
            self.hung = True
            return
        self.out.extend(lines)
        if 'NOEND' in patched:
            self.out.append('BYE')
            return self.end(0)
        self.out.extend([f'MUTANT {tag} END {lines[-1]}', f'FINGERPRINT {tag} static=1 db=1 rows=0 heap=1 ws=1 (1 ms)'])
        if tag == 'BASE/RUN':
            # a static the base made lazily: the new base, said, no stop
            self.out.append(f'FINGERPRINT-BASE {tag} (unmutated) changed the static fingerprint: Q.Lazy: the new base')
        if 'FPSTOP' in patched:
            self.out.append(f'STOP {tag} changed the static fingerprint: Q.Table')
            return self.end(2)
        if n:
            self.since.append(tag)
            if 'CANARYFAIL' in patched:
                self.canary(True)

    def readline(self, timeout):
        if self.out:
            return self.out.popleft()
        if self.ended:
            return None
        raise TimeoutError

    def exit_code(self, wait=60):
        return self.code

    def kill(self):
        if not self.ended:
            self.end(-9)


def selftest_loop(root, check, raises):
    """the whole loop on made-up worktrees under root"""
    n = [0]

    def tree(qc='public A\nx=1\n', quests='y=1\n'):
        n[0] += 1
        wt = os.path.join(root, f'wt{n[0]}')
        for f in ['a#x', 'a', 'b#1', 'b#2', 'c']:
            os.makedirs(os.path.join(wt, 'tests/golden/flows'), exist_ok=True)
            open(os.path.join(wt, 'tests/golden/flows', f + '.flow'), 'w').close()
        for rel, data in [('src/XI.QuestContent/Q.cs', qc), ('src/XI.Quests/B.cs', quests)]:
            os.makedirs(os.path.dirname(os.path.join(wt, rel)), exist_ok=True)
            with open(os.path.join(wt, rel), 'w', encoding='utf-8', newline='') as f:
                f.write(data)
        os.makedirs(os.path.join(wt, HARNESS_BIN), exist_ok=True)
        return wt, os.path.join(root, f'out{n[0]}.tsv')

    def tsv(out):
        if not os.path.exists(out):
            return []
        with open(out, encoding='utf-8') as f:
            return [l.rstrip('\n') for l in f]

    made = {}

    def go(mutants, argv, wt, out, sc=('a',), grp=('a', 'b')):
        import builtins
        printed, orig_print = [], builtins.print

        class R(FakeRunner):
            def __init__(self, *a, **k):
                super().__init__(*a, **k)
                made['r'] = self
        builtins.print = lambda *a, **k: printed.append(' '.join(str(x) for x in a))
        try:
            code = main(wt, out, mutants, scope=list(sc), group=list(grp), db='xidb_lane7', checks=['--only', 'c3'],
                        argv=argv, runner=R)
        finally:
            builtins.print = orig_print
        return code, printed, made['r']

    QC_PATH, QUESTS_PATH = 'src/XI.QuestContent/Q.cs', 'src/XI.Quests/B.cs'
    clean_quests = 'quests y=1\n'

    # C1: a mutant outside XI.QuestContent, then QuestContent ones (one changes the reference assembly), a BAD and a
    # BUILD FAILED one: each replay on the base build's dlls but its own; the tree and the bin clean at the end
    wt, out = tree()
    code, printed, r = go([('X1', QUESTS_PATH, 'y=1', 'y=2'), ('Q1', QC_PATH, 'x=1', 'x=2'),
                           ('R1', QC_PATH, 'public A', 'public A2'), ('Q2', QC_PATH, 'x=1', 'x=3 KILL_A'),
                           ('BD', QC_PATH, 'nope', 'x'), ('BF', QC_PATH, 'x=1', 'BROKEN')], ['ALLIDS'], wt, out)
    rows = tsv(out)
    check('C1 mixed list: exit 0', 0, code)
    check('C1 mixed list: the verdicts', ['X1\tKILLED\tgroup', 'Q1\tSURVIVED ALL', 'R1\tSURVIVED ALL',
                                          'Q2\tKILLED\tscope', 'BD\tBAD MUTANT: 0 matches', 'BF\tBUILD FAILED'],
          [('\t'.join(l.split('\t')[:3]) if '\tKILLED\t' in l else '\t'.join(l.split('\t')[:2])) for l in rows
           if not l.startswith('BASE')])
    check('C1 mixed list: the base replays the scope alone and the exact combined run, then the checks',
          ['BASE SC\t1 keys\tpass', 'BASE RUN\t2 keys\tpass', 'BASE CHECKS\t--only c3\tpass'],
          ['\t'.join(l.split('\t')[:3]) for l in rows if l.startswith('BASE')])
    check('I2: the base\'s replays are the scope alone and the mutants\' exact selection', ['a', 'a,b'],
          [s[0] for s in r.seen[:2]])
    mutant_runs = [s for s in r.seen[2:] if 'x=2' in s[2] or 'A2' in s[2] or 'x=3' in s[2]]
    check('C1 mixed list: every QuestContent mutant\'s replay ran on the base build\'s XI.Quests.dll',
          [clean_quests] * len(mutant_runs), [s[1] for s in mutant_runs])
    check('C1 mixed list: X1 replayed on its own XI.Quests.dll', ['quests y=2\n'],
          [s[1] for s in r.seen if 'y=1' not in s[1]][:1])
    timing = tsv(out + '.timing.tsv')
    check('M5: the timing TSV names each build path',
          ['whole', 'questcontent', 'whole (reference assembly changed)', 'questcontent', 'questcontent'],
          [l.split('build-path ')[1] for l in timing if 'build-path ' in l])
    check('C1: a clean whole build after each whole-built mutant and at the end',
          ['before Q1', 'before Q2', 'after the last mutant'],
          [l.split('\t')[1] for l in timing if l.startswith('CLEAN')])
    check('C1/M6: the sources restored, no marker or temp file left', ['public A\nx=1\n', 'y=1\n', False, False],
          [open(os.path.join(wt, QC_PATH), newline='').read(), open(os.path.join(wt, QUESTS_PATH), newline='').read(),
           os.path.exists(out + '.inflight'), os.path.exists(os.path.join(wt, QC_PATH + TMP_SUFFIX))])
    check('C1: the bin is the base build\'s at the end', r.base_bin, r.bin_hashes())

    # C1: the old loop (no clean build after a whole-built mutant) is caught by the fast path's bin check
    wt, out = tree()
    r = FakeRunner(wt, out, 'xidb_lane7')
    r.build_first()
    full = os.path.join(wt, QUESTS_PATH)
    write_atomic(full, b'y=2\n')
    r.build_mutant(QUESTS_PATH)
    write_atomic(full, b'y=1\n')
    write_atomic(os.path.join(wt, QC_PATH), b'public A\nx=2\n')
    raises('C1: the old loop: a QuestContent mutant after an XI.Quests one sees a mutated XI.Quests.dll',
           lambda: r.build_mutant(QC_PATH), 'XI.Quests.dll')

    # I1, M4, timeouts: each stops the run with the source restored
    for what, mutant, text in [
        ('I1 a lane lock conflict', ('L1', QC_PATH, 'x=1', 'x=1 LOCK'), 'no picked flow'),
        ('I1 the party cleanup check', ('P1', QC_PATH, 'x=1', 'x=1 PARTY'), 'before any flow'),
        ('M4 the checks crash', ('C1', QC_PATH, 'x=1', 'x=1 CHECKCRASH'), 'no RESULT line'),
        ('M4 the checks match nothing', ('C2', QC_PATH, 'x=1', 'x=1 NOMATCH'), 'no [FAIL] line'),
        ('M7 a replay over the timeout', ('H1', QC_PATH, 'x=1', 'x=1 HANG'), 'killed'),
    ]:
        wt, out = tree()
        code, printed, r = go([mutant], ['--no-base', mutant[0]], wt, out)
        check(f'{what}: exit 1, the runner stopped', (1, True),
              (code, any(p.startswith('runner stopped:') and text in p for p in printed)))
        check(f'{what}: the source restored, no marker', ('public A\nx=1\n', False),
              (open(os.path.join(wt, QC_PATH), newline='').read(), os.path.exists(out + '.inflight')))

    # I2: --no-base warns and says so in the TSV; a base whose checks fail unmutated stops before any mutant
    wt, out = tree()
    code, printed, r = go([('Q1', QC_PATH, 'x=1', 'x=2')], ['--no-base', 'Q1'], wt, out)
    check('I2 --no-base: a WARNING, a BASE skipped line, the mutant run', (0, True, ['BASE\tskipped (--no-base)',
                                                                          'Q1\tSURVIVED ALL\t']),
          (code, any(p.startswith('WARNING --no-base') for p in printed), tsv(out)))
    wt, out = tree(quests='y=1 CHECKFAIL\n')
    code, printed, r = go([('Q1', QC_PATH, 'x=1', 'x=2')], ['Q1'], wt, out)
    check('I2 a check failing unmutated: the base fails, no mutant run', (1, True, False),
          (code, any('BASE CHECKS' in l and 'FAIL' in l for l in tsv(out)), any(l.startswith('Q1') for l in tsv(out))))
    wt, out = tree()
    code, printed, r = go([('Q1', QC_PATH, 'x=1', 'x=2')], [], wt, out)
    check('I2 no ids: only the base', (0, 3, 1), (code, len(tsv(out)), r.check_builds))

    # M2 / M3: a scope key's coverage a group flow reached passes combined, fails alone: KILLED scope; a coverage
    # line a group flow caused: the scope alone passes, KILLED group
    wt, out = tree()
    code, printed, r = go([('V1', QC_PATH, 'x=1', 'x=1 COV_MASK'), ('V2', QC_PATH, 'x=1', 'x=1 COV_GROUP')],
                          ['ALLIDS'], wt, out)
    check('M2/M3 coverage: the scope alone decides', ['V1\tKILLED\tscope\tFAIL coverage a: 1/X/onTrigger reached by no '
                                                      'trace', 'V2\tKILLED\tgroup\tFAIL coverage a: waiver 1/X/onTrigger '
                                                      'is reached by b#1'],
          [l for l in tsv(out) if not l.startswith('BASE')])

    # M6: a marker of a mutated file stops the run; one whose file is back is removed
    wt, out = tree()
    with open(out + '.inflight', 'w', encoding='utf-8') as f:
        f.write(f'{QC_PATH}\n{"0" * 64}\n')
    code, printed, r = go([('Q1', QC_PATH, 'x=1', 'x=2')], ['--no-base', 'Q1'], wt, out)
    check('M6 a marker whose file differs: the runner stops', (1, True),
          (code, any('an earlier run stopped with' in p for p in printed)))
    with open(out + '.inflight', 'w', encoding='utf-8') as f:
        f.write(f'{QC_PATH}\n{sha(os.path.join(wt, QC_PATH))}\n')
    code, printed, r = go([('Q1', QC_PATH, 'x=1', 'x=2')], ['--no-base', 'Q1'], wt, out)
    check('M6 a marker whose file is back: removed, the run goes', (0, False), (code, os.path.exists(out + '.inflight')))

    # M6: an exception mid-mutant (here the build's) restores the source and drops the marker
    wt, out = tree()
    r = FakeRunner(wt, out, 'xidb_lane7')
    r.build_first()

    def boom(path):
        raise KeyboardInterrupt
    r.build_mutant = boom
    try:
        r.mutant('K1', QC_PATH, 'x=1', 'x=2', ['a'], ['a', 'b'])
        check('M6 an interrupt mid-mutant propagates', True, False)
    except KeyboardInterrupt:
        pass
    check('M6 an interrupt mid-mutant: the source restored, no marker', ('public A\nx=1\n', False),
          (open(os.path.join(wt, QC_PATH), newline='').read(), os.path.exists(out + '.inflight')))

    selftest_one_process(tree, tsv, go, check, raises, QC_PATH)


def selftest_one_process(tree, tsv, go, check, raises, QC_PATH):
    """--one-process on the fake worktree (FakeMutantProcess): its verdicts are option 1's, the bins and the source
    untouched, the checks from the copy with the switch, the fallbacks through the loop, every stop"""
    mutants = [('Q1', QC_PATH, 'x=1', 'x=2'), ('Q2', QC_PATH, 'x=1', 'x=3 KILL_A'), ('V1', QC_PATH, 'x=1', 'x=1 COV_MASK'),
               ('V2', QC_PATH, 'x=1', 'x=1 COV_GROUP'), ('C3', QC_PATH, 'x=1', 'x=1 CHECKFAIL'), ('G1', QC_PATH, 'x=1', 'x=1 KILL_B1'),
               ('F1', QC_PATH, 'x=1', 'x=4 FALLBACK'), ('S1', QC_PATH, 'x=1', 'x=5 STATICINIT'), ('BD', QC_PATH, 'nope', 'x')]

    def verdicts(out):
        return sorted(('\t'.join(l.split('\t')[:3]) if '\tKILLED\t' in l else '\t'.join(l.split('\t')[:2])) for l in tsv(out)
                      if not l.startswith('BASE'))
    wt, out = tree()
    code1, _, r1 = go(mutants, ['ALLIDS'], wt, out)
    want = verdicts(out)
    wt, out = tree()
    code, printed, r = go(mutants, ['--one-process', 'ALLIDS'], wt, out)
    rows = tsv(out)
    check('one process: exit 0, the verdicts option 1 gives the same list', (0, 0, want), (code1, code, verdicts(out)))
    check('one process: the base in the process (scope, the combined run, the checks), the fallbacks\' own base after',
          ['BASE SC', 'BASE RUN', 'BASE CHECKS', 'BASE FALLBACK SC', 'BASE FALLBACK RUN', 'BASE FALLBACK CHECKS'],
          [l.split('\t')[0] for l in rows if l.startswith('BASE')])
    timing = tsv(out + '.timing.tsv')
    paths = {l.split('\t')[0]: l.split('\tpath ')[1] for l in timing if '\tpath ' in l and not l.startswith('BASE')}
    check('one process: the timing rows say the path (one process, or the fallback and why)',
          {'Q1': 'one-process', 'Q2': 'one-process', 'V1': 'one-process', 'V2': 'one-process', 'C3': 'one-process',
           'G1': 'one-process', 'F1': 'fallback fake fallback',
           'S1': 'fallback its switch runs while the class constructors or the setup run (STATICINIT)'}, paths)
    check('one process: one process, one schema run, the checks built once (and per fallback survivor), the copy\'s checks '
          'run unmutated once and per survivor with its switch', (1, 1, ['None', '1', '5']),
          (len(r.spawned), r.schema_runs, [str(n) for n in r.schema_checks]))
    check('one process: the worktree\'s bin is the base build\'s, the source restored, no marker',
          (r.base_bin, 'public A\nx=1\n', False),
          (r.bin_hashes(), open(os.path.join(wt, QC_PATH), newline='').read(), os.path.exists(out + '.inflight')))
    check('one process: the schema copies hold the schema dll, every other dll the base build\'s',
          (True, True), (open(r.p(SCHEMA_HARNESS_BIN + '/XI.QuestContent.dll')).read().startswith('qc-schema '),
                         sha(r.p(SCHEMA_HARNESS_BIN + '/XI.Quests.dll')) == r.base_bin['XI.Quests.dll']))
    check('one process: the process ran the mutants\' blocks on their own patched source (Q2 kills a, G1 b#1)', True,
          any('KILL_A' in s[2] for s in r.seen) and any('KILL_B1' in s[2] for s in r.seen))
    check('one process: the last canary ran at QUIT', 1, r.canaries_run)

    # the canary every N mutants (N = 2 here) and at the end
    global CANARY_EVERY
    saved = CANARY_EVERY
    CANARY_EVERY = 2
    try:
        wt, out = tree()
        code, printed, r = go([(f'Q{i}', QC_PATH, 'x=1', f'x=1{" " * i}') for i in range(1, 6)], ['--one-process', 'ALLIDS'], wt, out)
        check('one process: a canary after every 2 mutants and the last at QUIT (5 mutants: 3)', (0, 3, 3),
              (code, r.canaries_run, sum(l.startswith('CANARY') for l in tsv(out + '.timing.tsv'))))
    finally:
        CANARY_EVERY = saved

    # every stop: the runner stops (exit 1), the source and the bin as they were
    for what, new, text in [
        ('a crash (the process gone with no END)', 'x=1 CRASH', 'ended (exit 134)'),
        ('a hang (no line within the timeout: killed)', 'x=1 HANG', 'no line within the timeout'),
        ('a fingerprint stop', 'x=1 FPSTOP', 'changed the static fingerprint'),
        ('a canary stop', 'x=1 CANARYFAIL', 'the canary failed'),
        ('a block with no END', 'x=1 NOEND', 'a block with no END'),
        ('a reference assembly the schema changed', 'x=1 REFDIFF', 'reference assembly'),
        ('a block that counts a failure with no FAIL line (review M3)', 'x=1 NOFAILLINE', 'failed and no FAIL line'),
    ]:
        wt, out = tree()
        code, printed, r = go([('Q1', QC_PATH, 'x=1', 'x=2'), ('Z1', QC_PATH, 'x=1', new)], ['--one-process', 'ALLIDS'], wt, out)
        check(f'one process: {what} stops the runner', (1, True),
              (code, any(p.startswith('runner stopped:') and text in p for p in printed)))
        check(f'one process: {what}: the source, the bin and no marker as they were', ('public A\nx=1\n', r.base_bin, False),
              (open(os.path.join(wt, QC_PATH), newline='').read(), r.bin_hashes(), os.path.exists(out + '.inflight')))

    # review N1: the WARNING for a plan of more than one scope, none for one
    wt, out = tree()
    two = [('Q1', QC_PATH, 'x=1', 'x=2', ['a']), ('Q2', QC_PATH, 'x=1', 'x=3', ['c'])]
    _, printed, _ = go(two, ['--one-process', 'ALLIDS'], wt, out)
    wt, out = tree()
    _, printed1, _ = go([('Q1', QC_PATH, 'x=1', 'x=2')], ['--one-process', 'ALLIDS'], wt, out)
    check('one process: the multi-scope WARNING for two scopes, none for one', (True, False),
          (MULTI_SCOPE_WARNING in printed, MULTI_SCOPE_WARNING in printed1))

    # --no-base: a warm-up block first (the fingerprints' base), the WARNING and the TSV line; --schema-only: no process
    wt, out = tree()
    code, printed, r = go([('Q1', QC_PATH, 'x=1', 'x=2')], ['--one-process', '--no-base', 'Q1'], wt, out)
    check('one process --no-base: a WARNING, BASE skipped, a warm-up block of each selection (the scope, the '
          'combined run), the mutant', (0, True, ['BASE\tskipped (--no-base)', 'Q1\tSURVIVED ALL\t'], ['a', 'a,b']),
          (code, any(p.startswith('WARNING --no-base') for p in printed), tsv(out), [x[0] for x in r.seen[:2]]))

    # review I3: a static variant that throws when the process makes it: VARIANT-THREW, no block, the mutant through
    # the fallback loop (its verdict option 1's), no one-process timing row for it; the plan names the variants
    wt, out = tree()
    vt = [('Q1', QC_PATH, 'x=1', 'x=2'), ('VT', QC_PATH, 'x=1', 'x=1 KILL_A VARIANTTHREW'),
          ('V2', QC_PATH, 'x=1', 'x=1 VARIANT')]
    code, printed, r = go(vt, ['--one-process', 'ALLIDS'], wt, out)
    timing = tsv(out + '.timing.tsv')
    check('one process: a static variant that throws falls back (VARIANT-THREW), its verdict the loop\'s; a variant '
          'that does not stays in the process',
          (0, 'VT\tKILLED\tscope', ['fallback its static variant throws (VARIANT-THREW Q.F: InvalidOperationException: x)'],
           ['one-process'], ['one-process']),
          (code, '\t'.join(next((l for l in tsv(out) if l.startswith('VT')), '').split('\t')[:3]),
           [l.split('\tpath ')[1] for l in timing if l.startswith('VT\t')],
           [l.split('\tpath ')[1] for l in timing if l.startswith('V2\t')],
           [l.split('\tpath ')[1] for l in timing if l.startswith('Q1\t')]))
    wt, out = tree()
    code, printed, r = go(mutants, ['--one-process'], wt, out)
    check('--one-process with no ids: refused, nothing built or run', (1, [], 0),
          (code, r.spawned if hasattr(r, 'spawned') else [], getattr(r, 'schema_runs', 0)))
    wt, out = tree()
    code, printed, r = go(mutants, ['--schema-only'], wt, out)
    check('--schema-only: the schema mode run, nothing replayed, no TSV row', (0, 1, [], []), (code, r.schema_runs, r.spawned, tsv(out)))


if __name__ == '__main__':
    if sys.argv[1:2] == ['--selftest'] and len(sys.argv) <= 3:
        sys.exit(selftest(sys.argv[2] if len(sys.argv) == 3 else None))
    print('a lane script imports this module and calls main() (see its text); alone it runs --selftest [<dir>] only')
    sys.exit(1)
