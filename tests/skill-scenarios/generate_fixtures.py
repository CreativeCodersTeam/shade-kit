"""Generates the work folder for the skill scenario runs from the current engine.

Usage (from anywhere, Python 3 standard library only):

    python3 -I tests/skill-scenarios/generate_fixtures.py [--work <dir>] [--engine-dll <path>]

The script reads the CLI contract from the version handshake sentence of skills/shade-kit-phase-entry/SKILL.md,
builds the CLI (unless --engine-dll is given), creates a git repository in the work folder, and drives the real engine
through the workflow that the scenarios need. It writes into the work folder:

- fixtures/*.json   the JSON envelopes of the engine calls that the scenario catalog names, among them the two
                    refused version handshakes (env-version-engine-outdated.json, env-version-skills-outdated.json)
- ids.json          the generated feature ids and the CLI contract (read by checks/check_runs.py)
- repo/             the fixture repository; repo-s5/, repo-s6/, repo-s9/, repo-s11/, repo-s12/, repo-s15/, and
                    repo-s16/ are copies of it
- repo-empty/       an initialised repository without features; repo-s10/, repo-s13/, and repo-s14/ are copies of it
- repo-s17/         a copy of repo/ on the branch feature/<sso>, so the branch selects the sso feature
                    (S18 and S19 read repo-empty/ and repo/ without a copy: they never get past the handshake)
- repo-s20/         a copy of repo/ whose sso feature has a longer request.md and discussion notes; repo-s21/ (with
                    the spec.md and researches.md of a first refine round), repo-s22/ (without the spec template), and
                    repo-s23/ (like repo-s21/, but the spec.md has no "Changes" section) are copies of it
- out/              empty; the runners write their outputs here
- SCENARIOS.md      the catalog with every placeholder resolved (what the runners read)
- RUNNER-PROTOCOL.md the protocol with the paths resolved

Unlike the unit tests, this script starts git and the real engine. Any engine call that returns ok: false (or, for
the calls that must fail, another error code) or an unexpected outcome stops it with a non-zero exit code, so that a
workflow change shows up at once.
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
REPOSITORY = SCRIPT_DIR.parent.parent
CLI_PROJECT = REPOSITORY / "src" / "CreativeCoders.ShadeKit.Cli"
DEFAULT_DLL = CLI_PROJECT / "bin" / "Debug" / "net10.0" / "shade-kit.dll"
CONTRACT_SKILL = REPOSITORY / "skills" / "shade-kit-phase-entry" / "SKILL.md"
HANDSHAKE = re.compile(r"Version handshake: run `shade-kit version --json --expect-contract (\d+)`; (\d+) is the CLI "
                       r"contract this skill is written for\.")

SSO_SOURCE = "Add SSO login with Entra ID"
QUICK_SOURCE = "Fix typo"
TYPO_LINE = 'Fix the typo "Sigin" in the login button text.'
LARGE_CHANGE_LINE = (
    "Replace the whole authentication of all six services with SSO via Entra ID, including the migration of "
    "existing user accounts. The approach (gateway or per service) is not decided yet."
)
NOTES_FILE = "20261120-0900-login-with-sso.md"
NOTES_TEXT = """# Login with SSO

Feature: `none` · Date: 2026-11-20 · Participants: Scenario Fixture

## Context

Users ask to sign in with their company account instead of a password.

## Options

### Entra ID

Sign in with the company tenant of Entra ID.

Pros:

- Every user already has an account.

Cons:

- Guests without a company account cannot sign in.

## Conclusions

- Use Entra ID for the company tenant; keep the password login for guests.

## Open questions

None.
"""
ISSUE_URL = "https://github.com/acme/shop/issues/142"
REFINE_REQUEST = """# SSO login

Users of the shop admin portal should sign in with their company account through Entra ID instead of a password.

- Admins sign in with SSO; the login must be fast.
- Guests from partner companies also need access.
- When an employee leaves the company, they must lose access.
- Keep the audit log of sign-ins.
"""
REFINE_NOTES_FILE = "20261121-1000-sso-scope.md"
REFINE_NOTES_TEXT = """# SSO scope

Feature: `<sso>` · Request: [request.md](../request.md) · Date: 2026-11-21 · Participants: Scenario Fixture

## Context

The admin portal has a password login today.

## Options

### Sessions of 8 hours

A session lasts one working day.

Pros:

- Admins sign in once a day.

Cons:

- A stolen session stays valid for a whole day.

## Conclusions

- Sessions last at most 8 hours.

## Open questions

- Do guests keep a password login?
"""
SPEC_ROUND_1_DATE = "2026-11-21"
SPEC_ROUND_1 = """# Spec: SSO login

Feature: `<sso>` · Request: [request.md](request.md) · Research: [researches.md](researches.md)

## Goal

Employees of the shop admin portal sign in with their company account through Entra ID, so that they keep no separate password and lose access when they leave the company.

## Glossary

| Term | Kind | Meaning |
|---|---|---|
| Company account | entity | An employee's account in the company's Entra ID tenant. Belongs to one employee. |
| Guest | term | A user from a partner company without a company account. |

## Requirements

### REQ-001 – Sign in with Entra ID

- Priority: must
- Status: active
- Rationale: Employees must not keep a separate password for the portal.

An employee with a company account signs in to the admin portal through Entra ID.

Acceptance criteria:

- **AC-001.1** Given an employee with a valid company account, when they choose "Sign in with SSO", then they are signed in and see the portal start page.
- **AC-001.2** The SSO sign-in completes within the agreed time. (open: Q-002)

### REQ-002 – Access ends when an employee leaves

- Priority: must
- Status: active
- Rationale: Former employees must not reach the portal.

An employee whose company account is disabled can no longer sign in, and their open sessions end.

Acceptance criteria:

- **AC-002.1** Given an employee whose company account was disabled, when they try to sign in, then the sign-in is refused.

### REQ-003 – Password login for guests

- Priority: should
- Status: active
- Rationale: Guests from partner companies have no company account.

Guests sign in with a user name and a password. (open: Q-001)

Acceptance criteria:

- **AC-003.1** Given a guest with a valid password, when they sign in, then they see the portal start page.

## Edge cases

- The company account is disabled while the employee is signed in → AC-002.1

## Security considerations

- Sign-in events in the audit log contain personal data → REQ-001
- Guest passwords are credentials that must be stored securely → REQ-003

## Out of scope

- Identity providers other than Entra ID.

## Assumptions

- **A-001** Sessions last at most 8 hours, as the discussion notes say. (unconfirmed)

## Open questions

- **Q-001** (REQ-003) Do guests keep a password login, or do they sign in through Entra ID too?
- **Q-002** (REQ-001) Which sign-in time counts as "fast"?

## Decisions

None.

## Changes

- 2026-11-21: created
"""
RESEARCH_ROUND_1 = """# Research: SSO login

Feature: `<sso>` · Request: [request.md](request.md) · Spec: [spec.md](spec.md)

## refine / gaps

Date: 2026-11-21

- **G-1** "The login must be fast" has no target time. Source: request.md:5
- **G-2** The request does not say whether guests keep a password login. Source: request.md:6

## refine / security

Date: 2026-11-21

- **RS-1** Sign-in events in the audit log contain personal data. Source: request.md:8
- **RS-2** Guest passwords are credentials. Source: request.md:6. Assumption: the request implies a password login for guests.
"""
CSV_SOURCE = "Export the order list as CSV."
BUGFIX_TEXT = """Login fails when the user's name contains an umlaut
Steps: sign in as "Jörg Müller"; the page shows error 500 and the log has $LOGIN_ERROR."""
TEAM_ITEMS = [
    ("TEAM-01", "The product owner accepted the wording of the corrected text."),
    ("TEAM-02", "The support team was told about the change."),
    ("TEAM-03", "The legal team agreed that the corrected text needs no new translation review."),
    ("TEAM-04", "The release manager agreed to ship the fix in the next patch release."),
    ("TEAM-05", "The UX designer confirmed that the corrected text fits the layout."),
]


class GeneratorError(Exception):
    pass


def fail(message):
    raise GeneratorError(message)


def is_inside(path, parent):
    try:
        path.resolve().relative_to(parent.resolve())
        return True
    except ValueError:
        return False


def read_contract():
    text = CONTRACT_SKILL.read_text(encoding="utf-8")
    match = HANDSHAKE.search(text)
    if not match:
        fail(f"no canonical version handshake sentence (`shade-kit version --json --expect-contract <n>`; "
             f"<n> is the CLI contract this skill is written for.) in {CONTRACT_SKILL}")
    if match.group(1) != match.group(2):
        fail(f"the handshake sentence in {CONTRACT_SKILL} names two contracts: {match.group(1)} and {match.group(2)}")
    return int(match.group(1))


def find_executable(name):
    path = shutil.which(name)
    if not path:
        fail(f"'{name}' was not found on PATH")
    return str(Path(path).resolve())


def run(args, cwd, env=None):
    result = subprocess.run(args, cwd=str(cwd), env=env, capture_output=True, text=True, encoding="utf-8")
    if result.returncode != 0:
        fail(f"command failed with exit code {result.returncode}: {' '.join(args)}\n{result.stdout}{result.stderr}")
    return result.stdout


class Engine:
    """Runs the CLI with a PATH that holds only git and dotnet, so that no installed agent changes the envelopes."""

    def __init__(self, dotnet, dll, git):
        self.dotnet = dotnet
        self.dll = dll
        env = dict(os.environ)
        env["PATH"] = os.pathsep.join(dict.fromkeys([str(Path(git).parent), str(Path(dotnet).parent)]))
        env["DOTNET_NOLOGO"] = "1"
        env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
        env.pop("SHADE_KIT_MARKETPLACE_SOURCE", None)
        self.env = env

    def call(self, repo, args, outcome=None, check=None):
        command = "shade-kit " + " ".join(args)
        result = subprocess.run([self.dotnet, self.dll] + args, cwd=str(repo), env=self.env, capture_output=True,
                                text=True, encoding="utf-8")
        try:
            envelope = json.loads(result.stdout)
        except json.JSONDecodeError:
            fail(f"{command}: no JSON envelope (exit code {result.returncode})\n{result.stdout}{result.stderr}")
        if result.returncode != 0 or envelope.get("ok") is not True:
            fail(f"{command}: exit code {result.returncode}, envelope:\n{json.dumps(envelope, indent=2)}")
        data = envelope.get("data") or {}
        if outcome is not None and data.get("outcome") != outcome:
            fail(f"{command}: outcome '{data.get('outcome')}', expected '{outcome}'")
        if check is not None:
            problem = check(data)
            if problem:
                fail(f"{command}: {problem}")
        return result.stdout, envelope

    def call_failing(self, repo, args, error_code):
        """Runs a call that must fail with the given error code and returns its stdout."""
        command = "shade-kit " + " ".join(args)
        result = subprocess.run([self.dotnet, self.dll] + args, cwd=str(repo), env=self.env, capture_output=True,
                                text=True, encoding="utf-8")
        try:
            envelope = json.loads(result.stdout)
        except json.JSONDecodeError:
            fail(f"{command}: no JSON envelope (exit code {result.returncode})\n{result.stdout}{result.stderr}")
        code = (envelope.get("error") or {}).get("code")
        if result.returncode == 0 or envelope.get("ok") is not False or code != error_code:
            fail(f"{command}: exit code {result.returncode}, error code {code}, expected {error_code}")
        return result.stdout


def expect_current(feature_id, status):
    def check(data):
        feature = data.get("feature") or {}
        actual = (feature.get("id"), feature.get("status")) if data.get("resolved") else (None, None)
        if actual != (feature_id, status):
            return f"current feature/status is {actual[0]}/{actual[1]}, expected {feature_id}/{status}"
        return None
    return check


def expect_next(feature_id, continue_phase, forward_phase):
    def check(data):
        current = (data.get("current") or {}).get("featureId")
        cont = (data.get("continue") or {}).get("phase")
        forward = (data.get("forward") or {}).get("phase")
        if (current, cont, forward) != (feature_id, continue_phase, forward_phase):
            return (f"current/continue/forward is {current}/{cont}/{forward}, "
                    f"expected {feature_id}/{continue_phase}/{forward_phase}")
        return None
    return check


def expect_gate_info(feature_id):
    def check(data):
        gate = data.get("gate") or {}
        if data.get("featureId") != feature_id or gate.get("phase") != "finish" or gate.get("checklistExists") is not True:
            return f"unexpected gate data: featureId {data.get('featureId')}, gate {gate}"
        return None
    return check


def write_text(path, text, newline):
    with open(path, "w", encoding="utf-8", newline="") as handle:
        handle.write(text.replace("\n", newline))


def read_text(path):
    with open(path, "r", encoding="utf-8", newline="") as handle:
        text = handle.read()
    newline = "\r\n" if "\r\n" in text else "\n"
    return text.replace("\r\n", "\n"), newline


def replace_checklist_items(path):
    text, newline = read_text(path)
    lines = text.split("\n")
    start = next((i for i, line in enumerate(lines) if line.startswith("| DOD-01 ")), None)
    runs = next((i for i, line in enumerate(lines) if line.strip() == "## Check runs"), None)
    if start is None or runs is None or runs < start:
        fail(f"{path}: no '| DOD-01' row before '## Check runs'; the Definition of Done template changed")
    end = max((i for i in range(start, runs) if lines[i].strip() == ""), default=None)
    if end is None:
        fail(f"{path}: no blank line between the item rows and '## Check runs'")
    rows = [f"| {item_id} | {criterion} | pending | |" for item_id, criterion in TEAM_ITEMS]
    write_text(path, "\n".join(lines[:start] + rows + lines[end:]), newline)


def append_request_line(path, line):
    text, newline = read_text(path)
    if text and not text.endswith("\n"):
        text += "\n"
    write_text(path, f"{text}\n{line}\n", newline)


def replace_request_line(path, old, new):
    text, newline = read_text(path)
    if old not in text:
        fail(f"{path}: line to replace not found")
    write_text(path, text.replace(old, new), newline)


def git_commit(git, repo, message):
    run([git, "add", "-A"], repo)
    run([git, "commit", "-q", "--allow-empty", "-m", message], repo)


def new_repository(git, path):
    path.mkdir()
    run([git, "init", "-q", "-b", "main"], path)
    for key, value in (("user.name", "Scenario Fixture"), ("user.email", "scenario@example.invalid"),
                       ("commit.gpgsign", "false"), ("tag.gpgsign", "false")):
        run([git, "config", key, value], path)
    run([git, "commit", "-q", "--allow-empty", "-m", "init"], path)


def copy_repository(work, source, name):
    target = work / name
    shutil.copytree(str(source), str(target), symlinks=True)
    return target


def resolve_placeholders(text, values):
    for key, value in values.items():
        text = text.replace(key, value)
    return text


def prepare_work(work_arg):
    if work_arg is None:
        work = Path(tempfile.mkdtemp(prefix="shade-kit-scenarios-")).resolve()
    else:
        work = Path(work_arg).expanduser().resolve()
    if is_inside(work, REPOSITORY) or is_inside(REPOSITORY, work):
        fail(f"the work folder {work} must lie outside the SHADE kit repository {REPOSITORY.resolve()}: "
             "init would write into the wrong git repository")
    if work.exists() and any(work.iterdir()):
        fail(f"the work folder {work} is not empty")
    work.mkdir(parents=True, exist_ok=True)
    return work


def build_cli(dotnet):
    print(f"Building {CLI_PROJECT.name} ...")
    run([dotnet, "build", str(CLI_PROJECT), "--nologo", "-v", "quiet"], REPOSITORY)
    if not DEFAULT_DLL.is_file():
        fail(f"the build produced no {DEFAULT_DLL}")
    return DEFAULT_DLL


def expect_contract(contract):
    def check(data):
        current = (data.get("contract") or {}).get("current")
        expected = data.get("expectedContract")
        if (current, expected) != (contract, contract):
            return f"contract.current/expectedContract is {current}/{expected}, expected {contract}/{contract}"
        return None
    return check


def generate(work, engine, git, contract):
    fixtures = work / "fixtures"
    fixtures.mkdir()
    repo = work / "repo"
    repo.mkdir()

    def save(name, stdout):
        (fixtures / name).write_text(stdout, encoding="utf-8")

    # Empty git repository with an initial commit.
    repo.rmdir()
    new_repository(git, repo)

    # 1. Version handshake, and the two refused handshakes (S18, S19): skills newer and older than the engine.
    stdout, _ = engine.call(repo, ["version", "--json", "--expect-contract", str(contract)],
                            check=expect_contract(contract))
    save("env-version.json", stdout)
    save("env-version-engine-outdated.json",
         engine.call_failing(repo, ["version", "--json", "--expect-contract", "999"], "engine-outdated"))
    save("env-version-skills-outdated.json",
         engine.call_failing(repo, ["version", "--json", "--expect-contract", "0"], "skills-outdated"))

    # 2. Kit initialisation.
    engine.call(repo, ["init", "--json"])
    git_commit(git, repo, "kit")

    # 3. Feature profile: refine started, approved, started again.
    _, envelope = engine.call(repo, ["feature", "new", "--source", SSO_SOURCE, "--title", "SSO login",
                                     "--profile", "feature", "--json"])
    sso = envelope["data"]["id"]
    stdout, _ = engine.call(repo, ["phase", "start", "refine", "--json"], outcome="transition-requested")
    save("env-start-refine-1.json", stdout)
    engine.call(repo, ["phase", "approve", "refine", "--json"])
    stdout, _ = engine.call(repo, ["phase", "start", "refine", "--json"], outcome="already-in-progress")
    save("env-start-refine-already.json", stdout)
    stdout, _ = engine.call(repo, ["phase", "next", "--json"], check=expect_next(sso, "refine", "map"))
    save("env-next-refine.json", stdout)

    # 4. Quick profile: quick started (no approval), finish requested.
    _, envelope = engine.call(repo, ["feature", "new", "--source", QUICK_SOURCE, "--title", "Quick fix",
                                     "--profile", "quick", "--json"])
    quick = envelope["data"]["id"]
    if quick == sso:
        fail(f"both features got the id {sso}")
    stdout, _ = engine.call(repo, ["phase", "start", "quick", "--json"], outcome="transition-performed")
    save("env-start-quick.json", stdout)
    stdout, _ = engine.call(repo, ["phase", "next", "--json"], check=expect_next(quick, "quick", "finish"))
    save("env-next-quick.json", stdout)
    stdout, _ = engine.call(repo, ["phase", "start", "finish", "--json"], outcome="transition-requested")
    save("env-start-finish.json", stdout)

    # 5. Team items that need a human judgement, and the typo in request.md.
    quick_dir = repo / ".shadekit" / "features" / quick
    replace_checklist_items(quick_dir / "checks" / "definition-of-done.md")
    append_request_line(quick_dir / "request.md", TYPO_LINE)

    # 6. Gate data of finish.
    stdout, _ = engine.call(repo, ["gate", "info", "finish", "--feature", quick, "--json"],
                            check=expect_gate_info(quick))
    save("env-gate-info-finish.json", stdout)
    git_commit(git, repo, "fixture")

    # 7. Copies for the scenarios that write or need other content.
    for name in ("repo-s5", "repo-s6", "repo-s9"):
        shutil.copytree(str(repo), str(work / name), symlinks=True)
    s9_request = work / "repo-s9" / ".shadekit" / "features" / quick / "request.md"
    replace_request_line(s9_request, TYPO_LINE, LARGE_CHANGE_LINE)
    git_commit(git, work / "repo-s9", "fixture s9")

    # 8. Discuss and new (S10-S17).
    intake = generate_intake(work, engine, git, repo, fixtures, sso, quick)

    # 9. Refine (S20-S22).
    generate_refine(work, engine, git, repo, fixtures, sso)

    (work / "out").mkdir()
    ids = {"sso": sso, "quick": quick, "contract": contract, "notesFile": NOTES_FILE, "csv": intake["csv"]}
    (work / "ids.json").write_text(json.dumps(ids, indent=2) + "\n", encoding="utf-8")

    values = {"<work>": str(work), "<repository>": str(REPOSITORY), "<contract>": str(contract),
              "<sso-feature>": sso, "<quick-feature>": quick, "<notes-file>": NOTES_FILE,
              "<issue-url>": ISSUE_URL, "<csv-feature>": intake["csv"]}
    for name in ("SCENARIOS.md", "RUNNER-PROTOCOL.md"):
        text = (SCRIPT_DIR / name).read_text(encoding="utf-8")
        (work / name).write_text(resolve_placeholders(text, values), encoding="utf-8")
    return ids


def generate_intake(work, engine, git, repo, fixtures, sso, quick):
    """Builds the repositories and fixtures of the discuss and new scenarios S10-S17."""

    def save(name, stdout):
        (fixtures / name).write_text(stdout, encoding="utf-8")

    # A repository without any feature: no current feature, so phase next fails.
    empty = work / "repo-empty"
    new_repository(git, empty)
    engine.call(empty, ["init", "--json"])
    git_commit(git, empty, "kit")
    stdout, _ = engine.call(empty, ["feature", "current", "--json"], check=expect_current(None, None))
    save("env-current-none.json", stdout)
    save("env-next-none.json", engine.call_failing(empty, ["phase", "next", "--json"], "no-current-feature"))
    copy_repository(work, empty, "repo-s10")
    copy_repository(work, empty, "repo-s14")

    # S13: notes written by discuss without a feature, created as a feature with --move-source. The fixtures come
    # from a throwaway copy, so the runner's repository still holds the notes.
    s13 = copy_repository(work, empty, "repo-s13")
    (s13 / ".shadekit" / "discussions").mkdir()
    write_text(s13 / ".shadekit" / "discussions" / NOTES_FILE, NOTES_TEXT, "\n")
    git_commit(git, s13, "notes")
    gen = copy_repository(work, s13, "gen-s13")
    stdout, envelope = engine.call(gen, ["feature", "new", "--source", f".shadekit/discussions/{NOTES_FILE}",
                                         "--move-source", "--json"])
    notes_feature = envelope["data"]["id"]
    if not envelope["data"]["source"]["value"].endswith(f"/{notes_feature}/discussions/{NOTES_FILE}"):
        fail(f"feature new --move-source recorded the source {envelope['data']['source']['value']}")
    save("env-new-notes.json", stdout)
    stdout, _ = engine.call(gen, ["phase", "next", "--json"], check=expect_next(notes_feature, None, "refine"))
    save("env-next-new-notes.json", stdout)
    shutil.rmtree(str(gen))

    # S14: an issue URL of the default tracker; gh is not on the PATH of the engine, so feature new refuses it.
    # gh must not share a directory with git or dotnet, or the engine would find it and call it over the network.
    if shutil.which("gh", path=engine.env["PATH"]) is not None:
        fail("gh is on the engine PATH (it shares a directory with git or dotnet); S14 needs an engine without gh")
    stdout = engine.call_failing(work / "repo-s14", ["feature", "new", "--source", ISSUE_URL, "--json"],
                                 "invalid-source")
    if "was not found" not in json.loads(stdout)["error"]["message"]:
        fail("S14: feature new did not refuse the issue URL because gh is missing")
    save("env-new-issue.json", stdout)

    # S11, S15, S16: the sso feature is current (active, refine in progress).
    s11 = copy_repository(work, repo, "repo-s11")
    engine.call(s11, ["feature", "set", sso, "--json"])
    stdout, _ = engine.call(s11, ["feature", "current", "--json"], check=expect_current(sso, "active"))
    save("env-current-sso.json", stdout)
    copy_repository(work, s11, "repo-s15")
    s16 = copy_repository(work, s11, "repo-s16")
    gen = copy_repository(work, s16, "gen-s16")
    stdout, envelope = engine.call(gen, ["feature", "new", "--source", BUGFIX_TEXT, "--profile", "bugfix", "--json"])
    bugfix_feature = envelope["data"]["id"]
    save("env-new-bugfix.json", stdout)
    stdout, _ = engine.call(gen, ["phase", "next", "--json"], check=expect_next(bugfix_feature, None, "diagnose"))
    save("env-next-new-bugfix.json", stdout)
    shutil.rmtree(str(gen))

    # S17: the branch selects the sso feature; a new feature is created anyway, and the branch keeps winning.
    s17 = copy_repository(work, repo, "repo-s17")
    (s17 / ".shadekit" / "features" / "current-feature.json").unlink()
    run([git, "switch", "-q", "-c", f"feature/{sso}"], s17)
    _, envelope = engine.call(s17, ["feature", "current", "--json"], check=expect_current(sso, "active"))
    if envelope["data"].get("resolvedFrom") != "branch":
        fail(f"S17: feature current resolved from {envelope['data'].get('resolvedFrom')}, expected branch")
    save("env-current-sso-branch.json", json.dumps(envelope, indent=2) + "\n")
    gen = copy_repository(work, s17, "gen-s17")
    stdout, envelope = engine.call(gen, ["feature", "new", "--source", CSV_SOURCE, "--profile", "feature", "--json"])
    csv_feature = envelope["data"]["id"]
    save("env-new-csv.json", stdout)
    stdout, _ = engine.call(gen, ["feature", "show", csv_feature, "--json"])
    save("env-show-csv.json", stdout)
    stdout, _ = engine.call(gen, ["phase", "next", "--json"], check=expect_next(sso, "refine", "map"))
    save("env-next-refine-branch.json", stdout)
    shutil.rmtree(str(gen))

    # S12: the quick feature is finished, and the discussion template is missing.
    s12 = copy_repository(work, repo, "repo-s12")
    engine.call(s12, ["phase", "approve", "finish", "--json"])
    engine.call(s12, ["phase", "complete", "--json"])
    # phase complete clears the override; the human selects the finished feature again.
    engine.call(s12, ["feature", "set", quick, "--json"])
    stdout, _ = engine.call(s12, ["feature", "current", "--json"], check=expect_current(quick, "done"))
    save("env-current-done.json", stdout)
    stdout, _ = engine.call(s12, ["phase", "next", "--json"])
    save("env-next-done.json", stdout)
    (s12 / ".shadekit" / "templates" / "discussion.md").unlink()
    git_commit(git, s12, "fixture s12")
    return {"csv": csv_feature}


def generate_refine(work, engine, git, repo, fixtures, sso):
    """Builds the repositories and the fixture of the refine scenarios S20-S22."""

    def save(name, stdout):
        (fixtures / name).write_text(stdout, encoding="utf-8")

    def expect_show(data):
        if data.get("id") != sso or data.get("currentPhase") != "refine":
            return f"feature show returned {data.get('id')} in {data.get('currentPhase')}, expected {sso} in refine"
        return None

    # S20: the first refine round. A longer request and notes, so that the research has something to find.
    s20 = copy_repository(work, repo, "repo-s20")
    feature_dir = s20 / ".shadekit" / "features" / sso
    write_text(feature_dir / "request.md", REFINE_REQUEST, "\n")
    (feature_dir / "discussions").mkdir()
    write_text(feature_dir / "discussions" / REFINE_NOTES_FILE, REFINE_NOTES_TEXT.replace("<sso>", sso), "\n")
    if (feature_dir / "spec.md").exists() or (feature_dir / "researches.md").exists():
        fail("S20: the sso feature already has a spec.md or researches.md")
    git_commit(git, s20, "fixture s20")
    stdout, _ = engine.call(s20, ["feature", "show", sso, "--json"], check=expect_show)
    save("env-show-sso.json", stdout)

    # S21: a later round on the spec and the research of the first round.
    s21 = copy_repository(work, s20, "repo-s21")
    feature_dir = s21 / ".shadekit" / "features" / sso
    write_text(feature_dir / "spec.md", SPEC_ROUND_1.replace("<sso>", sso), "\n")
    write_text(feature_dir / "researches.md", RESEARCH_ROUND_1.replace("<sso>", sso), "\n")
    git_commit(git, s21, "fixture s21")

    # S23: a later round on a spec written before the "Changes" section existed.
    s23 = copy_repository(work, s20, "repo-s23")
    feature_dir = s23 / ".shadekit" / "features" / sso
    old_spec = SPEC_ROUND_1.split("\n## Changes\n", 1)[0] + "\n"
    if "## Changes" in old_spec or not old_spec.rstrip().endswith("None."):
        fail("S23: the fixture spec still has a Changes section or does not end with its Decisions")
    write_text(feature_dir / "spec.md", old_spec.replace("<sso>", sso), "\n")
    write_text(feature_dir / "researches.md", RESEARCH_ROUND_1.replace("<sso>", sso), "\n")
    git_commit(git, s23, "fixture s23")

    # S22: the first round, but the repository's spec template is missing.
    s22 = copy_repository(work, s20, "repo-s22")
    (s22 / ".shadekit" / "templates" / "spec.md").unlink()
    git_commit(git, s22, "fixture s22")


def main():
    parser = argparse.ArgumentParser(description="Generates the work folder for the skill scenario runs.")
    parser.add_argument("--work", help="work folder outside the repository (default: a new temporary folder)")
    parser.add_argument("--engine-dll", help="built shade-kit.dll to use instead of building the CLI")
    args = parser.parse_args()

    try:
        dotnet = find_executable("dotnet")
        git = find_executable("git")
        contract = read_contract()
        work = prepare_work(args.work)
        print(f"Work folder: {work}")
        if args.engine_dll:
            dll = Path(args.engine_dll).expanduser().resolve()
            if not dll.is_file():
                fail(f"--engine-dll {dll} does not exist")
        else:
            dll = build_cli(dotnet)
        ids = generate(work, Engine(dotnet, str(dll), git), git, contract)
    except GeneratorError as error:
        print(f"error: {error}", file=sys.stderr)
        return 1

    print(f"CLI contract: {ids['contract']}")
    print(f"Features: sso {ids['sso']}, quick {ids['quick']}")
    print("Fixtures: " + ", ".join(sorted(p.name for p in (work / "fixtures").iterdir())))
    print(f"Scenario catalog: {work / 'SCENARIOS.md'}")
    print(f"Work folder: {work}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
