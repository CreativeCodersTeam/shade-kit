"""Generates the work folder for the skill scenario runs from the current engine.

Usage (from anywhere, Python 3 standard library only):

    python3 -I tests/skill-scenarios/generate_fixtures.py [--work <dir>] [--engine-dll <path>]

The script builds the CLI (unless --engine-dll is given), creates a git repository in the work folder, and drives the
real engine through the workflow that the scenarios need. It writes into the work folder:

- fixtures/*.json   the JSON envelopes of the engine calls that the scenario catalog names
- ids.json          the generated feature ids and the kit version (read by checks/check_runs.py)
- repo/             the fixture repository; repo-s5/, repo-s6/, repo-s9/ are copies of it
- out/              empty; the runners write their outputs here
- SCENARIOS.md      the catalog with every placeholder resolved (what the runners read)
- RUNNER-PROTOCOL.md the protocol with the paths resolved

Unlike the unit tests, this script starts git and the real engine. Any engine call that returns ok: false or an
unexpected outcome stops it with a non-zero exit code, so that a workflow change shows up at once.
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
VERSION_SKILL = REPOSITORY / "skills" / "shade-kit-next-steps" / "SKILL.md"

SSO_SOURCE = "Add SSO login with Entra ID"
QUICK_SOURCE = "Fix typo"
TYPO_LINE = 'Fix the typo "Sigin" in the login button text.'
LARGE_CHANGE_LINE = (
    "Replace the whole authentication of all six services with SSO via Entra ID, including the migration of "
    "existing user accounts. The approach (gateway or per service) is not decided yet."
)
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


def read_kit_version():
    text = VERSION_SKILL.read_text(encoding="utf-8")
    match = re.search(r"^---\s*$(.*?)^---\s*$", text, re.S | re.M)
    if not match:
        fail(f"no frontmatter in {VERSION_SKILL}")
    metadata = re.search(r"^metadata:[ \t]*\n((?:[ \t]+.*\n)*)", match.group(1), re.M)
    version = re.search(r"^[ \t]+version:[ \t]*(\S+)[ \t]*$", metadata.group(1), re.M) if metadata else None
    if not version or not re.fullmatch(r"\d+\.\d+\.\d+", version.group(1)):
        fail(f"no plain x.y.z metadata.version in {VERSION_SKILL}")
    return version.group(1)


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


def generate(work, engine, git, kit_version):
    fixtures = work / "fixtures"
    fixtures.mkdir()
    repo = work / "repo"
    repo.mkdir()

    def save(name, stdout):
        (fixtures / name).write_text(stdout, encoding="utf-8")

    # Empty git repository with an initial commit.
    run([git, "init", "-q", "-b", "main"], repo)
    for key, value in (("user.name", "Scenario Fixture"), ("user.email", "scenario@example.invalid"),
                       ("commit.gpgsign", "false"), ("tag.gpgsign", "false")):
        run([git, "config", key, value], repo)
    run([git, "commit", "-q", "--allow-empty", "-m", "init"], repo)

    # 1. Version handshake.
    stdout, _ = engine.call(repo, ["version", "--json", "--expect", kit_version])
    save("env-version.json", stdout)

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

    (work / "out").mkdir()
    ids = {"sso": sso, "quick": quick, "kitVersion": kit_version}
    (work / "ids.json").write_text(json.dumps(ids, indent=2) + "\n", encoding="utf-8")

    values = {"<work>": str(work), "<repository>": str(REPOSITORY), "<kit-version>": kit_version,
              "<sso-feature>": sso, "<quick-feature>": quick}
    for name in ("SCENARIOS.md", "RUNNER-PROTOCOL.md"):
        text = (SCRIPT_DIR / name).read_text(encoding="utf-8")
        (work / name).write_text(resolve_placeholders(text, values), encoding="utf-8")
    return ids


def main():
    parser = argparse.ArgumentParser(description="Generates the work folder for the skill scenario runs.")
    parser.add_argument("--work", help="work folder outside the repository (default: a new temporary folder)")
    parser.add_argument("--engine-dll", help="built shade-kit.dll to use instead of building the CLI")
    args = parser.parse_args()

    try:
        dotnet = find_executable("dotnet")
        git = find_executable("git")
        kit_version = read_kit_version()
        work = prepare_work(args.work)
        print(f"Work folder: {work}")
        if args.engine_dll:
            dll = Path(args.engine_dll).expanduser().resolve()
            if not dll.is_file():
                fail(f"--engine-dll {dll} does not exist")
        else:
            dll = build_cli(dotnet)
        ids = generate(work, Engine(dotnet, str(dll), git), git, kit_version)
    except GeneratorError as error:
        print(f"error: {error}", file=sys.stderr)
        return 1

    print(f"Kit version: {ids['kitVersion']}")
    print(f"Features: sso {ids['sso']}, quick {ids['quick']}")
    print("Fixtures: " + ", ".join(sorted(p.name for p in (work / "fixtures").iterdir())))
    print(f"Scenario catalog: {work / 'SCENARIOS.md'}")
    print(f"Work folder: {work}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
