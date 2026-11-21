"""Checks the outputs of the skill scenario runs against the expected behaviour of the skill texts.

NEVER show this file to a scenario runner: the runners must follow the skill texts without knowing what is checked.

Usage (Python 3 standard library only):

    python3 -I tests/skill-scenarios/checks/check_runs.py <work> [outdir] [--only S10,S11]

<work> is the work folder written by generate_fixtures.py; the feature ids come from <work>/ids.json. [outdir] is the
folder of the outputs, relative to <work> (default: out). Exit code 0 when every check passes, 1 when one fails, 2 on a
usage error.

The expectations were written for the skill clarifications of 2026-11-18 (S1-S9 in SCENARIOS.md) and for the discuss
and new skills of package 09 on 2026-11-20 (S10-S17); when a skill text changes what a scenario does, change the
expectation here together with the skill.
"""
import json
import re
import subprocess
import sys
from pathlib import Path

NOTES_NAME = re.compile(r"^\d{8}-\d{4}-[a-z0-9]+(-[a-z0-9]+)*(-\d+)?\.md$")
NOTES_SECTIONS = ("## Context", "## Options", "## Conclusions", "## Open questions")
GIT_WRITE = re.compile(r"\bgit (switch|checkout|branch|commit|add|stash|reset|push|tag|merge|rebase|restore|rm|mv|"
                       r"worktree|cherry-pick|revert|clean|config (?!user\.name\b|--get\b))")
# A git command as a runner records it: in a code span or at the start of a code line, with global options such as
# `-C <dir>`, `-c <key>=<value>`, or `--no-pager` before the subcommand. Prose such as "runs no git command" is no hit.
GIT_ANY = re.compile(r"(?:`|^[ \t]*(?:\$[ \t]+)?)(?:\S*/)?git(?:\.exe)?[ \t]+"
                     r"(?:-{1,2}[\w.-]+(?:[ =][^\s`]+)?[ \t]+)*[a-z][\w-]*", re.M)
# A runner records what it did outside the skill as step 0 (protocol rule 6); the skill checks leave it out.
RUNNER_PREPARATION = re.compile(r"^0\. .*?(?=^\d+\. |\Z)", re.S | re.M)


def section(text, name):
    m = re.search(rf"^## {re.escape(name)}\s*$(.*?)(?=^## |\Z)", text, re.S | re.M)
    return m.group(1) if m else ""


def closing(text):
    return section(text, "Closing message")


def tool_blocks(text, tool):
    """Return the JSON-ish blocks that follow a 'TOOL CALL <tool>' marker."""
    blocks = []
    for m in re.finditer(rf"TOOL CALL\W*{re.escape(tool)}.*?```[a-z]*\n(.*?)```", text, re.S):
        blocks.append(m.group(1))
    return blocks


def count_questions(block):
    try:
        data = json.loads(block)
    except json.JSONDecodeError:
        return len(re.findall(r'"question"\s*:', block))
    if isinstance(data, dict) and isinstance(data.get("questions"), list):
        return len(data["questions"])
    return 1


def plain_messages(steps):
    return re.findall(r"MESSAGE TO HUMAN.*?```[a-z]*\n(.*?)```", steps, re.S)


def kit_commands(steps):
    """Return every recorded shade-kit command that was run, up to the end of its line.

    A command that was run has at least one option; a mention in a message or an option of the closing message (for
    example `shade-kit feature set <id>`) has none or a placeholder, so it is left out.
    """
    found = re.findall(r"shade-kit (?:version|feature|phase|gate|init)\b[^\n`]*", steps)
    return [c for c in found if " --" in c and "<id>" not in c]


def feature_new_command(steps):
    """Return the first recorded feature new command, also when its --source argument spans several lines.

    The match stays inside one code span or code block (no backtick), so it never reaches the --json of a later
    command; without its own --json the command is not found.
    """
    m = re.search(r"shade-kit feature new\b[^`]*?--json\b", steps)
    return m.group(0) if m else ""


def skill_steps(steps):
    """Return the steps without the runner's own preparation (step 0)."""
    return RUNNER_PREPARATION.sub("", steps)


def notes_files(folder):
    return sorted(folder.glob("*.md")) if folder.is_dir() else []


def git_status(repo):
    result = subprocess.run(["git", "status", "--porcelain"], cwd=str(repo), capture_output=True, text=True)
    return result.stdout.strip() if result.returncode == 0 else f"git status failed: {result.stderr}"


class Checker:
    def __init__(self, work, out, ids):
        self.work = work
        self.out = out
        self.sso = ids["sso"]
        self.quick = ids["quick"]
        self.kit_version = ids["kitVersion"]
        self.notes_file = ids.get("notesFile", "")
        self.csv = ids.get("csv", "")
        self.issue_url = "https://github.com/acme/shop/issues/142"
        self.results = []

    def check(self, sid, name, ok):
        self.results.append((sid, name, bool(ok)))

    def codex_forms(self, sid, text, skills):
        c = closing(text)
        for s in skills:
            self.check(sid, f"closing names $shade-kit:{s}", f"$shade-kit:{s}" in c)
        self.check(sid, "closing has no slash form /shade-kit-", re.search(r"(?<![:\w])/shade-kit-", c) is None)
        self.check(sid, "no doubled prefix shade-kit-shade-kit", "shade-kit-shade-kit" not in text)

    def slash_forms(self, sid, text, skills):
        c = closing(text)
        for s in skills:
            self.check(sid, f"closing names /{s}", f"/{s}" in c)
        self.check(sid, "closing has no $shade-kit: form", "$shade-kit:" not in c)
        self.check(sid, "no doubled prefix shade-kit-shade-kit", "shade-kit-shade-kit" not in text)

    def checklist_copy(self, repo_name):
        path = self.work / repo_name / ".shadekit" / "features" / self.quick / "checks" / "definition-of-done.md"
        return path.read_text(encoding="utf-8") if path.is_file() else ""

    def run(self):
        refine_skills = ["shade-kit-refine", "shade-kit-map", "shade-kit-discuss", "shade-kit-drop"]

        for sid in ["S1", "S2", "S3", "S4", "S5", "S6", "S7", "S8", "S9"]:
            p = self.out / f"{sid}.md"
            if not p.exists():
                self.check(sid, "output exists", False)
                continue
            t = p.read_text(encoding="utf-8")
            self.check(sid, "no MISSING FIXTURE", "MISSING FIXTURE" not in t)
            steps = section(t, "Steps")
            if sid == "S1":
                self.codex_forms(sid, t, refine_skills)
                self.check(sid, "ran phase next once", steps.count("shade-kit phase next --json") >= 1)
            if sid == "S2":
                self.slash_forms(sid, t, refine_skills)
            if sid == "S3":
                self.check(sid, "no request_user_input tool call", "TOOL CALL" not in steps)
                self.check(sid, "plain question asked", "MESSAGE TO HUMAN" in steps)
                self.codex_forms(sid, t, refine_skills)
            if sid == "S4":
                self.check(sid, "AskUserQuestion call", len(tool_blocks(steps, "AskUserQuestion")) >= 1)
                self.slash_forms(sid, t, refine_skills)
            if sid in ("S5", "S6"):
                tool, limit = ("request_user_input", 3) if sid == "S5" else ("AskUserQuestion", 4)
                blocks = tool_blocks(steps, tool)
                counts = [count_questions(b) for b in blocks]
                self.check(sid, f"{tool} used", len(blocks) >= 1)
                self.check(sid, f"every call has at most {limit} questions {counts}",
                           all(c <= limit for c in counts) and counts)
                self.check(sid, f"5 questions asked in total {counts}", sum(counts) == 5)
                copy = self.checklist_copy("repo-s5" if sid == "S5" else "repo-s6")
                self.check(sid, "TEAM-03 is fail in the copy",
                           re.search(r"\| TEAM-03 \|[^|]*\| fail \|", copy) is not None)
                self.check(sid, "run line fail (TEAM-03)",
                           re.search(r"^- \d{4}-\d\d-\d\d \d\d:\d\d [+-]\d\d:\d\d – fail \(TEAM-03\)$", copy, re.M)
                           is not None)
                self.check(sid, "return names fail", re.search(r"\bfail\b", closing(t)) is not None)
            if sid == "S7":
                blocks = tool_blocks(steps, "ask_user")
                self.check(sid, "ask_user used", len(blocks) >= 1)
                firsts_ok = True
                for b in blocks:
                    labels = re.findall(r'"(?:label|choice|text)"\s*:\s*"([^"]*)"', b)
                    if not labels:
                        m = re.search(r'"choices"\s*:\s*\[(.*?)\]', b, re.S)
                        if m:
                            labels = re.findall(r'"([^"]*)"', m.group(1))
                    if not labels:
                        labels = re.findall(r'"([^"]*\(Recommended\)[^"]*)"', b)
                    marked = [x for x in labels if x.rstrip().endswith("(Recommended)")]
                    if not labels or not labels[0].rstrip().endswith("(Recommended)") or len(marked) != 1:
                        firsts_ok = False
                self.check(sid, "first option ends with (Recommended), exactly one marked", firsts_ok and blocks)
            if sid == "S8":
                self.check(sid, "no tool call", "TOOL CALL" not in steps)
                topic_msgs = [m for m in plain_messages(steps) if re.search(r"Topic \d+/\d+", m)]
                self.check(sid, "topic messages exist", len(topic_msgs) >= 2)
                self.check(sid, "each topic message starts with progress",
                           all(re.match(r"\s*\S*\s*Topic \d+/\d+", m) for m in topic_msgs))
                self.check(sid, "each topic message has exactly one topic heading",
                           all(len(re.findall(r"^\s*Topic \d+/\d+", m, re.M)) == 1 for m in topic_msgs))
                self.check(sid, "each topic message marks a recommendation",
                           all(re.search(r"recommend", m, re.I) for m in topic_msgs))
            if sid in ("S1", "S2", "S3", "S4"):
                self.check(sid, "full feature id in closing", self.sso in closing(t))
            if sid in ("S5", "S6"):
                c = closing(t)
                self.check(sid, "return names missing artifacts",
                           all(x in c for x in ("spec.md", "review.md", "verification.md"))
                           and re.search(r"Missing artifacts", c, re.I))
            if sid == "S3":
                self.check(sid, "plain question has numbered options",
                           any(re.search(r"^\s*1[.)]", m, re.M) and re.search(r"^\s*2[.)]", m, re.M)
                               for m in plain_messages(steps)))
            if sid == "S9":
                self.check(sid, "no interview question asked", "Topic 1/" not in steps)
                self.check(sid, "report says moving on to finish does not fit",
                           re.search(r"finish", t)
                           and re.search(r"(nothing was implemented|does not fit|do not fit)", t, re.I))
                self.check(sid, "recommends $shade-kit:shade-kit-new", "$shade-kit:shade-kit-new" in t)
                c = closing(t)
                self.check(sid, "closing names $shade-kit:shade-kit-drop", "$shade-kit:shade-kit-drop" in c)
                self.check(sid, "closing has no slash form", re.search(r"(?<![:\w])/shade-kit-", c) is None)
                self.check(sid, "no doubled prefix", "shade-kit-shade-kit" not in t)
        self.run_intake()
        return self.results

    def common_intake(self, sid, text, steps):
        commands = kit_commands(steps)
        self.check(sid, f"first command is the handshake --expect {self.kit_version}",
                   commands and commands[0].startswith(f"shade-kit version --json --expect {self.kit_version}"))
        # A multi-line --source breaks the line-based match, so feature new is checked across lines.
        others = [c for c in commands if not c.startswith("shade-kit feature new")]
        new_ok = "shade-kit feature new" not in steps or feature_new_command(steps) != ""
        self.check(sid, "every shade-kit command has --json", others and all("--json" in c for c in others) and new_ok)
        self.check(sid, "ran feature current --json", "shade-kit feature current --json" in steps)
        self.check(sid, "no phase start/approve/reject/complete",
                   re.search(r"shade-kit phase (start|approve|reject|complete)", steps) is None)
        self.check(sid, "no git command that changes the repository", GIT_WRITE.search(steps) is None)
        self.check(sid, "a closing message is written", closing(text).strip() not in ("", "none"))

    def notes_checks(self, sid, path, feature_line, request_link):
        notes = path.read_text(encoding="utf-8") if path else ""
        first = notes.split("\n", 1)[0]
        self.check(sid, f"notes file name {path.name if path else None}", path and NOTES_NAME.match(path.name))
        self.check(sid, "heading is the topic alone", first.startswith("# ") and not re.match(r"# Discussion\b", first))
        self.check(sid, f"heading is a short title ({first!r})", len(first[2:].split()) <= 8 and not first.endswith("."))
        self.check(sid, f"notes line {feature_line}", feature_line in notes)
        self.check(sid, "request link as expected", ("](../request.md)" in notes) == request_link)
        self.check(sid, "no placeholder or guidance comment left", "{{" not in notes and "<!--" not in notes)
        self.check(sid, "all four sections", all(x in notes for x in NOTES_SECTIONS))
        self.check(sid, "at least one option subsection", re.search(r"^### \S", notes, re.M) is not None)
        self.check(sid, "LF line endings", "\r" not in notes)

    def fixture_id(self, name):
        data = json.loads((self.work / "fixtures" / name).read_text(encoding="utf-8"))
        return data["data"]["id"]

    def run_intake(self):
        for sid in ["S10", "S11", "S12", "S13", "S14", "S15", "S16", "S17"]:
            p = self.out / f"{sid}.md"
            if not p.exists():
                self.check(sid, "output exists", False)
                continue
            t = p.read_text(encoding="utf-8")
            steps = section(t, "Steps")
            c = closing(t)
            self.check(sid, "no MISSING FIXTURE", "MISSING FIXTURE" not in t)
            self.common_intake(sid, t, steps)

            if sid in ("S10", "S11", "S12"):
                self.check(sid, "no feature new", "shade-kit feature new" not in steps)
                self.check(sid, "interview loaded", "shade-kit-interview" in steps)
            if sid == "S10":
                repo = self.work / "repo-s10"
                files = notes_files(repo / ".shadekit" / "discussions")
                self.check(sid, f"one notes file in .shadekit/discussions ({len(files)})", len(files) == 1)
                self.notes_checks(sid, files[0] if files else None, "Feature: `none`", False)
                features = repo / ".shadekit" / "features"
                self.check(sid, "no feature folder created",
                           not features.is_dir() or not [x for x in features.iterdir() if x.is_dir()])
                self.check(sid, "closing names /shade-kit-new", "/shade-kit-new" in c)
                self.slash_forms(sid, t, [])
            if sid == "S11":
                repo = self.work / "repo-s11"
                files = notes_files(repo / ".shadekit" / "features" / self.sso / "discussions")
                self.check(sid, f"one notes file in the feature's discussions ({len(files)})", len(files) == 1)
                self.notes_checks(sid, files[0] if files else None, f"Feature: `{self.sso}`", True)
                self.check(sid, "no notes in .shadekit/discussions",
                           not notes_files(repo / ".shadekit" / "discussions"))
                base = self.work / "repo" / ".shadekit" / "features" / self.sso / "feature.json"
                own = repo / ".shadekit" / "features" / self.sso / "feature.json"
                self.check(sid, "feature.json unchanged", own.read_bytes() == base.read_bytes())
                self.slash_forms(sid, t, ["shade-kit-refine", "shade-kit-map"])
            if sid == "S12":
                repo = self.work / "repo-s12"
                files = notes_files(repo / ".shadekit" / "discussions")
                self.check(sid, f"one notes file in .shadekit/discussions ({len(files)})", len(files) == 1)
                self.notes_checks(sid, files[0] if files else None, "Feature: `none`", False)
                self.check(sid, "nothing in the finished feature's discussions",
                           not (repo / ".shadekit" / "features" / self.quick / "discussions").exists())
                self.check(sid, "says the template is missing",
                           re.search(r"discussion\.md[^\n]*(missing|not found|does not exist)|"
                                     r"(missing|not found|does not exist)[^\n]*discussion\.md", t, re.I))
                self.codex_forms(sid, t, ["shade-kit-new"])
            if sid in ("S13", "S14", "S15", "S16", "S17"):
                repo = self.work / f"repo-{sid.lower()}"
                self.check(sid, "repository unchanged (git status clean)", git_status(repo) == "")
                self.check(sid, "no git command at all in the skill steps", GIT_ANY.search(skill_steps(steps)) is None)
            command = feature_new_command(steps)
            if sid == "S13":
                notes = f".shadekit/discussions/{self.notes_file}"
                self.check(sid, "feature new with the notes as source", notes in command)
                self.check(sid, "feature new with --move-source", "--move-source" in command)
                self.check(sid, "feature new without --profile (repository default)", "--profile" not in command)
                feature = self.fixture_id("env-new-notes.json")
                self.check(sid, "names the suggested branch as information", f"feature/{feature}" in t)
                self.check(sid, "no question about a branch",
                           not any("branch" in b.lower() for b in tool_blocks(steps, "AskUserQuestion")))
                self.slash_forms(sid, t, ["shade-kit-refine"])
            if sid == "S14":
                self.check(sid, "feature new with the issue URL", self.issue_url in command)
                self.check(sid, "feature new without --title and --move-source",
                           command and "--title" not in command and "--move-source" not in command)
                self.check(sid, "the issue is not read by the agent",
                           re.search(r"\bgh (issue|api)\b|\bcurl\b|WebFetch|web_fetch", steps) is None)
                self.check(sid, "closing quotes the gh reason", "GitHub CLI gh" in c)
                # invalid-source falls under the fallback row of the refusal table: no phase next, no retry line.
                # A line that says the command was not run mentions it too.
                ran_next = [x for x in steps.splitlines()
                            if "shade-kit phase next" in x and not re.search(r"\bnot\b", x)]
                self.check(sid, "no phase next after the refusal", not ran_next)
                self.slash_forms(sid, t, [])
            if sid == "S15":
                self.check(sid, "no feature new", "shade-kit feature new" not in steps)
                self.check(sid, "no tool call", "TOOL CALL" not in steps)
                self.check(sid, "plain question names the current feature",
                           any(self.sso in m for m in plain_messages(steps)))
                self.check(sid, "says the current feature stays current", re.search(r"stays current", t) is not None)
                self.codex_forms(sid, t, ["shade-kit-refine"])
            if sid == "S16":
                blocks = tool_blocks(steps, "AskUserQuestion")
                self.check(sid, "AskUserQuestion names the current feature", any(self.sso in b for b in blocks))
                self.check(sid, "feature new with --profile bugfix", "--profile bugfix" in command)
                self.check(sid, "apostrophe quoted for a POSIX shell", "user'\\''s" in command)
                self.check(sid, "both lines in the --source argument",
                           "contains an umlaut" in command and "$LOGIN_ERROR" in command)
                self.check(sid, "feature new without --move-source", command and "--move-source" not in command)
                self.slash_forms(sid, t, ["shade-kit-diagnose"])
            if sid == "S17":
                self.check(sid, "feature new with --profile feature", "--profile feature" in command)
                self.check(sid, f"feature show for the new feature {self.csv}",
                           f"shade-kit feature show {self.csv} --json" in steps)
                # The line of what happened is the first paragraph of the closing message; the steps below it are
                # those of the branch feature and also name /shade-kit-refine.
                lead = c.strip().strip("`").strip().split("\n\n", 1)[0]
                self.check(sid, "closing names /shade-kit-refine for the new feature",
                           re.search(rf"{re.escape(self.csv)}.*/shade-kit-refine|/shade-kit-refine.*"
                                     rf"{re.escape(self.csv)}", lead, re.S) is not None)
                self.check(sid, "closing says the branch keeps selecting the other feature",
                           re.search(rf"branch.*(keeps|still|continues).*select.*{re.escape(self.sso)}", lead,
                                     re.S | re.I) is not None)
                self.check(sid, "closing names a branch switch", re.search(r"\bswitch", lead, re.I) is not None)
                self.slash_forms(sid, t, ["shade-kit-refine", "shade-kit-map"])


def main(argv):
    only = set()
    if "--only" in argv:
        i = argv.index("--only")
        if i + 1 >= len(argv):
            print("usage: check_runs.py <work> [outdir] [--only S10,S11]", file=sys.stderr)
            return 2
        only = {x.strip() for x in argv[i + 1].split(",") if x.strip()}
        argv = argv[:i] + argv[i + 2:]
    if len(argv) not in (2, 3):
        print("usage: check_runs.py <work> [outdir] [--only S10,S11]", file=sys.stderr)
        return 2
    work = Path(argv[1])
    out = work / (argv[2] if len(argv) > 2 else "out")
    ids_path = work / "ids.json"
    if not ids_path.is_file():
        print(f"error: {ids_path} not found; run generate_fixtures.py first", file=sys.stderr)
        return 2
    ids = json.loads(ids_path.read_text(encoding="utf-8"))

    results = [r for r in Checker(work, out, ids).run() if not only or r[0] in only]
    unknown = only - {r[0] for r in results}
    if unknown:
        print(f"error: unknown scenario ids: {', '.join(sorted(unknown))}", file=sys.stderr)
        return 2
    failed = [r for r in results if not r[2]]
    for sid, name, ok in results:
        print(f"{'PASS' if ok else 'FAIL'} {sid}: {name}")
    print(f"\n{len(results) - len(failed)}/{len(results)} checks passed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
