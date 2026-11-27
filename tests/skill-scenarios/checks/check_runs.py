"""Checks the outputs of the skill scenario runs against the expected behaviour of the skill texts.

NEVER show this file to a scenario runner: the runners must follow the skill texts without knowing what is checked.

Usage (Python 3 standard library only):

    python3 -I tests/skill-scenarios/checks/check_runs.py <work> [outdir] [--only S10,S11]

<work> is the work folder written by generate_fixtures.py; the feature ids come from <work>/ids.json. [outdir] is the
folder of the outputs, relative to <work> (default: out). Exit code 0 when every check passes, 1 when one fails, 2 on a
usage error.

The expectations were written for the skill clarifications of 2026-11-18 (S1-S9 in SCENARIOS.md), for the discuss
and new skills of package 09 on 2026-11-20 (S10-S17), and for the refused version handshakes of the CLI contract
(S18: the engine is older than the skills, S19: the skills are older than the engine); when a skill text changes what
a scenario does, change the expectation here together with the skill. --only takes any of S1-S19.
"""
import json
import re
import subprocess
import sys
from pathlib import Path

NOTES_NAME = re.compile(r"^\d{8}-\d{4}-[a-z0-9]+(-[a-z0-9]+)*(-\d+)?\.md$")
NOTES_SECTIONS = ("## Context", "## Options", "## Conclusions", "## Open questions")
SPEC_SECTIONS = ("Goal", "Glossary", "Requirements", "Edge cases", "Security considerations", "Out of scope",
                 "Assumptions", "Open questions", "Decisions", "Changes")
GIT_WRITE = re.compile(r"\bgit (switch|checkout|branch|commit|add|stash|reset|push|tag|merge|rebase|restore|rm|mv|"
                       r"worktree|cherry-pick|revert|clean|config (?!user\.name\b|--get\b))")
# A git command as a runner records it: in a code span or at the start of a code line, with global options such as
# `-C <dir>`, `-c <key>=<value>`, or `--no-pager` before the subcommand. Prose such as "runs no git command" is no hit.
GIT_ANY = re.compile(r"(?:`|^[ \t]*(?:\$[ \t]+)?)(?:\S*/)?git(?:\.exe)?[ \t]+"
                     r"(?:-{1,2}[\w.-]+(?:[ =][^\s`]+)?[ \t]+)*[a-z][\w-]*", re.M)
# A closing message that tells to update the engine, as opposed to the kit skills (the skills remedy). A clause ends at
# ".", ":", ";", or a line break. A skills word names the skills, a plugin, the integration(s), the marketplace, or the
# kit on its own; "SHADE kit"/"shade-kit" and "kit engine" are no skills word, so "upgrade the kit engine" counts as an
# engine update (real skills remedies say "kit skills", "kit plugin", "the kit with ...", never "kit engine"). The tool
# is shade-kit or SHADE kit, unless a word character or an apostrophe directly follows, a call form (":" before a skill
# name, e.g. "$shade-kit:shade-kit-next-phase"), or a skills word follows ("/shade-kit-new", "the shade-kit plugin").
# It counts when the options have:
# - an update verb (update, upgrade, (re)install, get, download, replace, also -s/-ing) followed within 40 characters
#   of the same clause, without a skills word between, by "engine" (not "engine's") or the tool ("Updating the engine",
#   "install the latest shade-kit", "update the CLI (shade-kit)"; not "install the kit skills that match the engine",
#   "reinstall the SHADE kit integration for the engine", "Install the engine's matching skills");
# - "engine update"/"engine upgrade", or "engine is too old"/"outdated"/"out of date";
# - "newer engine" or "newer <tool>" in a clause with no skills word before it and none in the 40 characters after it
#   ("You need a newer engine"; not "Install the skills that match the newer engine");
# - `dotnet tool update`/`install`.
# Every window is bounded or ends at a clause end, so a search stays linear in the length of the options.
# Known limit: a regex over free wording. A negation ("you do not need to update the engine", "no engine update is
# needed") still counts as an engine update, and an engine update without one of the verbs above ("bring the engine
# up to date") is missed. The skill texts are reviewed by hand (tests/skill-scenarios/README.md); a FAIL of the two
# checks that use this pattern (S18, S19) is read against the closing message before it counts.
_UPDATE_VERB = (r"(?:updat(?:es?|ing)|upgrad(?:es?|ing)|(?:re)?install(?:s|ing)?|get(?:s|ting)?|download(?:s|ing)?|"
                r"replac(?:es?|ing))")
_SKILLS_WORD = r"(?:skill|plugin|integrations?\b|marketplace|(?<!shade[ -])\bkits?\b(?! engine\b))"
_IN_CLAUSE = rf"(?:(?!{_SKILLS_WORD})[^.:;\n])"
_TOOL = r"shade[ -]kit(?![\w'’-]|:\S| (?:skills?|plugins?|integrations?|marketplace)\b)"
_ENGINE = r"engine\b(?!['’]s\b)"
ENGINE_UPDATE = re.compile(rf"\b{_UPDATE_VERB}\b{_IN_CLAUSE}{{0,40}}?\b(?:{_ENGINE}|{_TOOL})|"
                           r"\bengine (?:update|upgrade)|\bengine (?:is|was) (?:too old|outdated|out of date)\b|"
                           rf"(?:^|[.:;]){_IN_CLAUSE}*?\bnewer (?:{_ENGINE}|{_TOOL})"
                           rf"(?![^.:;\n]{{0,40}}?{_SKILLS_WORD})|"
                           r"dotnet tool (?:update|install)\b", re.I | re.M)
# A shade-kit command as a runner records it when it runs it: at the start of a line (a code line, optionally after a
# list marker or a `$` prompt), or in a code span with at least one option. A span without an option (for example
# "run `shade-kit integrations install`" in a description of the refusal table) is a mention.
SHADE_KIT_LINE = re.compile(r"^[ \t]*(?:\d+\.[ \t]+|[-*][ \t]+)?(?:\$[ \t]+)?(shade-kit[ \t]+[a-z][\w-]*[^\n`]*)", re.M)
SHADE_KIT_SPAN = re.compile(r"`(shade-kit[ \t]+[a-z][\w-]*[^`\n]*)`")
# A sentence that says the command in the code span was not run: a negation directly before it ("No `a`, `b`", "did
# not run `a`", "Not run: `a`") or a "was not run" directly after it (also after further spans of a list).
NOT_RUN_BEFORE = re.compile(r"(?:^|\W)(?:no|not|never|without|not run:?)[ \t]*"
                            r"(?:(?:\*\*)?`[^`\n]*`(?:\*\*)?[ \t]*(?:,|or|and|nor)?[ \t]*)*(?:\*\*)?$", re.I)
NOT_RUN_AFTER = re.compile(r"^(?:\*\*)?[ \t]*(?:(?:,|or|and|nor)[ \t]*(?:\*\*)?`[^`\n]*`(?:\*\*)?[ \t]*)*"
                           r"(?:\((?:is |was )?not run|(?:is|was|were|are|gets?|got)[ \t]+(?:\*\*)?(?:not|never)\b)",
                           re.I)
SENTENCE_END = re.compile(r"[.!?;](?=\s|$)")
# A runner records what it did outside the skill as step 0 (protocol rule 6); the skill checks leave it out.
RUNNER_PREPARATION = re.compile(r"^0\. .*?(?=^\d+\. |\Z)", re.S | re.M)


def section(text, name):
    m = re.search(rf"^## {re.escape(name)}\s*$(.*?)(?=^## |\Z)", text, re.S | re.M)
    return m.group(1) if m else ""


def closing(text):
    return section(text, "Closing message")


def closing_message(text):
    """Return the closing message itself: the fenced block of the closing section, without the return text.

    Without a fenced block the section counts up to its "Return text" line.
    """
    c = closing(text)
    m = re.search(r"```[a-z]*\n(.*?)```", c, re.S)
    if m:
        return m.group(1)
    return re.split(r"^Return text\b", c, maxsplit=1, flags=re.M)[0].strip()


def options_part(message, reason):
    """Return the closing message after its first paragraph (the line of what happened with the quoted reason).

    The reason (error.message of the refused call) is also removed from that part, so a quoted reason never counts as
    a remedy.
    """
    parts = message.strip().strip("`").strip().split("\n\n", 1)
    rest = parts[1] if len(parts) > 1 else ""
    return rest.replace(reason, "") if reason else rest


def question_markers(steps, marker):
    """Return the recorded questions of one kind (protocol rule 4: 'TOOL CALL <tool>' or 'MESSAGE TO HUMAN').

    A marker counts at the start of a line (after a list marker or emphasis) or when its line is directly followed by
    a fenced block; a mention in prose such as "(no `TOOL CALL`, no `MESSAGE TO HUMAN`)" is no hit.
    """
    m = re.escape(marker)
    pattern = rf"^[ \t]*(?:\d+\.[ \t]+|[-*][ \t]+)?[*_`]*{m}\b|{m}\b[^\n]*\n(?:[ \t]*\n)*[ \t]*```"
    return re.findall(pattern, steps, re.M)


def run_commands(steps):
    """Return every shade-kit command the steps record as run, in order, whatever its subcommand.

    A command counts at the start of a line or in a code span with an option; a span whose sentence says that this
    command was not run is left out. Quoted envelopes do not count (their lines start with a quote or a brace, and
    they quote commands in plain quotes).
    """
    found = [(m.start(1), m.group(1).strip()) for m in SHADE_KIT_LINE.finditer(steps)]
    for m in SHADE_KIT_SPAN.finditer(steps):
        command = m.group(1).strip()
        if " -" not in command:
            continue
        line_start = steps.rfind("\n", 0, m.start()) + 1
        line_end = steps.find("\n", m.end())
        before = steps[line_start:m.start()]
        after = steps[m.end():line_end if line_end != -1 else len(steps)]
        ends = list(SENTENCE_END.finditer(before))
        before = before[ends[-1].end():] if ends else before
        end = SENTENCE_END.search(after)
        after = after[:end.start()] if end else after
        if NOT_RUN_BEFORE.search(before) or NOT_RUN_AFTER.search(after):
            continue
        found.append((m.start(1), command))
    return [command for _, command in sorted(found)]


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


# A recommendation of another round, not the neutral "whether to run another round or move on is your decision".
RECOMMEND_ROUND = re.compile(r"recommend\w*[^.\n]{0,80}?\banother (?:refine )?round", re.I)
REFINE_STEP = re.compile(r"^\d+\.\s+\*\*(?:shade-kit-)?refine step (\d+)\b", re.M | re.I)


def refine_step(steps, number):
    """The text of the refine steps with the given number (several runs of one step, e.g. a second research, count)."""
    marks = [(m.start(), int(m.group(1))) for m in REFINE_STEP.finditer(steps)]
    parts = []
    for i, (start, n) in enumerate(marks):
        if n == number:
            end = next((s for s, m in marks[i + 1:] if m != number), len(steps))
            parts.append(steps[start:end])
    return "\n".join(parts)


def question_texts(part):
    """Everything asked in a part of the steps: tool call blocks of any tool and plain messages."""
    blocks = [b for tool in ("AskUserQuestion", "ask_user", "request_user_input") for b in tool_blocks(part, tool)]
    return "\n".join(blocks + plain_messages(part))


def interview_return(part):
    """The topic IDs the recorded return of an interview lists as answered or open, and the topics that were asked.

    The return lists every topic by its ID under "Answered:" and "Open:"; the questions carry the progress "Topic n/m"
    (the question text need not name the ID), so the asked topics are the distinct n."""
    m = re.search(r"^[ \t]*Answered:", part, re.M)
    listed = []
    if m:
        rest = part[m.end():]
        end = re.search(r"^[ \t]*(Dismissed|Conflicts|New topics)\b", rest, re.M)
        listed = re.findall(r"^[ \t]*- ((?:G|RS|Q|A|T)-?\d+)\b", rest[:end.start()] if end else rest, re.M)
    asked = set(re.findall(r"Topic (\d+)/\d+", part))
    return listed, asked


def refine_report(text, steps):
    """Step 9 of refine and the closing message: the report the human reads."""
    return refine_step(steps, 9) + "\n" + closing(text)


def git_status(repo):
    result = subprocess.run(["git", "status", "--porcelain"], cwd=str(repo), capture_output=True, text=True)
    return result.stdout.strip() if result.returncode == 0 else f"git status failed: {result.stderr}"


class Checker:
    def __init__(self, work, out, ids):
        self.work = work
        self.out = out
        self.sso = ids["sso"]
        self.quick = ids["quick"]
        self.contract = ids["contract"]
        self.notes_file = ids.get("notesFile", "")
        self.csv = ids.get("csv", "")
        self.issue_url = "https://github.com/acme/shop/issues/142"
        self.results = []

    def check(self, sid, name, ok):
        self.results.append((sid, name, bool(ok)))

    def codex_forms(self, sid, text, skills, message=None):
        c = closing(text) if message is None else message
        for s in skills:
            self.check(sid, f"closing names $shade-kit:{s}", f"$shade-kit:{s}" in c)
        self.check(sid, "closing has no slash form /shade-kit-", re.search(r"(?<![:\w])/shade-kit-", c) is None)
        self.check(sid, "no doubled prefix shade-kit-shade-kit", "shade-kit-shade-kit" not in text)

    def slash_forms(self, sid, text, skills, message=None):
        c = closing(text) if message is None else message
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
        self.run_refusals()
        self.run_refine()
        return self.results

    def common_intake(self, sid, text, steps):
        commands = kit_commands(steps)
        handshake = f"shade-kit version --json --expect-contract {self.contract}"
        self.check(sid, f"first command is the handshake --expect-contract {self.contract}",
                   commands and commands[0].startswith(handshake))
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

    def run_refusals(self):
        """S18 and S19: the handshake is refused, so the skill ends at once with the refusal."""
        fixtures = {"S18": "env-version-engine-outdated.json", "S19": "env-version-skills-outdated.json"}
        for sid in ["S18", "S19"]:
            p = self.out / f"{sid}.md"
            if not p.exists():
                self.check(sid, "output exists", False)
                continue
            t = p.read_text(encoding="utf-8")
            steps = section(t, "Steps")
            # The name and remedy checks look at the closing message only, not at its return text; the remedy checks
            # look at the options after the quoted reason, which names a remedy of its own.
            c = closing_message(t)
            envelope = json.loads((self.work / "fixtures" / fixtures[sid]).read_text(encoding="utf-8"))
            options = options_part(c, envelope["error"]["message"])
            skill_part = skill_steps(steps)
            handshake = f"shade-kit version --json --expect-contract {self.contract}"
            commands = run_commands(skill_part)
            self.check(sid, "no MISSING FIXTURE", "MISSING FIXTURE" not in t)
            self.check(sid, f"exactly one shade-kit command, the handshake --expect-contract {self.contract} "
                            f"{commands}", len(commands) == 1 and commands[0].startswith(handshake))
            self.check(sid, "no tool call", not question_markers(steps, "TOOL CALL"))
            self.check(sid, "no question to the human before the closing",
                       not question_markers(steps, "MESSAGE TO HUMAN"))
            self.check(sid, "a closing message is written", c.strip() not in ("", "none"))
            self.check(sid, "no shade-kit init and no phase next",
                       not [x for x in commands if re.match(r"shade-kit (?:init|phase next)\b", x)])
            if sid == "S18":
                self.check(sid, "options say to update the engine", ENGINE_UPDATE.search(options) is not None)
                self.check(sid, "options do not tell to run shade-kit integrations install",
                           "shade-kit integrations install" not in options)
                self.check(sid, "closing names shade-kit-new only as /shade-kit-new",
                           re.search(r"(?<![/\w-])shade-kit-new\b", c) is None)
                self.slash_forms(sid, t, [], message=c)
            if sid == "S19":
                self.check(sid, "options contain shade-kit integrations install",
                           "shade-kit integrations install" in options)
                self.check(sid, "options do not tell to update the engine", ENGINE_UPDATE.search(options) is None)
                self.check(sid, "closing names shade-kit-next-phase only as $shade-kit:shade-kit-next-phase",
                           re.search(r"(?<![:\w-])shade-kit-next-phase\b", c) is None)
                self.codex_forms(sid, t, [], message=c)


    def common_refine(self, sid, text, steps, repo):
        commands = run_commands(skill_steps(steps))
        handshake = f"shade-kit version --json --expect-contract {self.contract}"
        self.check(sid, f"first command is the handshake --expect-contract {self.contract}",
                   commands and commands[0].startswith(handshake))
        self.check(sid, f"every shade-kit command has --json {commands}",
                   commands and all("--json" in c for c in commands))
        self.check(sid, "ran phase start refine --json", "shade-kit phase start refine --json" in commands)
        self.check(sid, f"ran feature show {self.sso} --json",
                   f"shade-kit feature show {self.sso} --json" in commands)
        self.check(sid, "no phase approve/reject/complete and no other phase start",
                   not [c for c in commands if re.match(r"shade-kit phase (approve|reject|complete)\b", c)
                        or (c.startswith("shade-kit phase start") and c != "shade-kit phase start refine --json")])
        # A restore command the skill names in a message to the human is not run, so fenced blocks are left out, and
        # prose ("the git commit that ran init was not looked up") is no command: only code spans and command lines
        # count, the way a runner records a command it ran.
        recorded = re.sub(r"```.*?```", "", skill_steps(steps), flags=re.S)
        candidates = re.findall(r"`([^`\n]+)`", recorded) + re.findall(r"^[ \t]*(?:\$[ \t]+)?(git[ \t].*)$", recorded,
                                                                        re.M)
        self.check(sid, "no git command that changes the repository",
                   not [c for c in candidates if GIT_WRITE.search(c)])
        self.check(sid, "a closing message is written", closing(text).strip() not in ("", "none"))
        feature = f".shadekit/features/{self.sso}/"
        # git_status strips the output, so the status column of the first line may have lost its leading space.
        changed = [re.sub(r"^\s*\S+\s+", "", line).strip('"') for line in git_status(repo).splitlines()
                   if line.strip()]
        allowed = {feature + "spec.md", feature + "researches.md"}
        self.check(sid, f"only spec.md and researches.md of the feature changed {changed}",
                   all(x in allowed for x in changed))

    def spec_checks(self, sid, spec):
        headings = re.findall(r"^## (.+)$", spec, re.M)
        self.check(sid, f"spec.md sections in rule order {headings}", headings == list(SPEC_SECTIONS))
        self.check(sid, "no placeholder or guidance comment left", "{{" not in spec and "<!--" not in spec)
        blocks = re.split(r"^### ", section(spec, "Requirements"), flags=re.M)[1:]
        reqs = [b for b in blocks if re.match(r"REQ-\d{3} ", b)]
        self.check(sid, f"every requirement subsection has a REQ- ID ({len(blocks)} subsections)",
                   blocks and len(reqs) == len(blocks))
        ok_fields, ok_criteria = True, True
        for b in reqs:
            number = b[4:7]
            if not (re.search(r"^- Priority: (must|should|could)\s*$", b, re.M)
                    and re.search(r"^- Status: (active|removed \(.+\))\s*$", b, re.M)):
                ok_fields = False
            criteria = re.findall(r"\*\*AC-(\d{3})\.\d+\*\*", b)
            if not criteria or any(c != number for c in criteria):
                ok_criteria = False
        self.check(sid, "every requirement has a priority and a status", reqs and ok_fields)
        self.check(sid, "every requirement has at least one AC-<its number>.n", reqs and ok_criteria)
        self.check(sid, "out of scope stated", section(spec, "Out of scope").strip() != "")
        open_part = section(spec, "Open questions")
        self.check(sid, "open questions section stated", open_part.strip() != "")
        open_ids = re.findall(r"\*\*(Q-\d{3})\*\*", open_part)
        decided = re.findall(r"\*\*(Q-\d{3})\*\*", section(spec, "Decisions"))
        self.check(sid, f"no Q- ID both open and decided {open_ids} {decided}",
                   not set(open_ids) & set(decided) and len(set(open_ids + decided)) == len(open_ids + decided))
        self.check(sid, "every open question has its inline marker",
                   all(f"(open: {q})" in spec for q in open_ids))
        self.check(sid, "no inline marker for a decided question",
                   not [q for q in decided if f"(open: {q})" in spec])
        self.check(sid, "decisions name who and when",
                   all(re.search(r"\(Scenario Fixture, \d{4}-\d\d-\d\d\)\s*$", line)
                       for line in section(spec, "Decisions").splitlines() if line.startswith("- **Q-")))
        edges = [x for x in section(spec, "Edge cases").splitlines() if x.startswith("- ")]
        self.check(sid, "every edge case points to an AC or out of scope",
                   all(re.search(r"→ (AC-\d{3}\.\d+|out of scope)", x) for x in edges))
        security = section(spec, "Security considerations")
        lines = [x for x in security.splitlines() if x.startswith("- ")]
        self.check(sid, "security considerations linked or None identified",
                   (lines and all(re.search(r"→ (REQ-\d{3}|AC-\d{3}\.\d+|open: Q-\d{3})", x) for x in lines))
                   or security.strip().startswith("None identified"))
        return reqs

    def run_refine(self):
        """S20-S23: refine, first round, later round with a failing focus, missing spec template, old spec."""
        for sid in ["S20", "S21", "S22", "S23"]:
            p = self.out / f"{sid}.md"
            if not p.exists():
                self.check(sid, "output exists", False)
                continue
            t = p.read_text(encoding="utf-8")
            steps = section(t, "Steps")
            c = closing(t)
            repo = self.work / f"repo-{sid.lower()}"
            feature_dir = repo / ".shadekit" / "features" / self.sso
            spec_path = feature_dir / "spec.md"
            spec = spec_path.read_text(encoding="utf-8") if spec_path.is_file() else ""
            research_path = feature_dir / "researches.md"
            research = research_path.read_text(encoding="utf-8") if research_path.is_file() else ""
            self.check(sid, "no MISSING FIXTURE", "MISSING FIXTURE" not in t)
            self.common_refine(sid, t, steps, repo)
            if sid == "S20":
                self.check(sid, "spec.md created", spec != "")
                self.spec_checks(sid, spec)
                self.check(sid, "spec.md has LF line endings like the template", "\r" not in spec)
                changes = [x for x in section(spec, "Changes").splitlines() if x.startswith("- ")]
                self.check(sid, f"one Changes line '- <date>: created' {changes}",
                           len(changes) == 1 and re.match(r"^- \d{4}-\d\d-\d\d: created\s*$", changes[0]))
                self.check(sid, "researches.md has refine / gaps and refine / security",
                           "\n## refine / gaps\n" in research and "\n## refine / security\n" in research)
                gaps = re.findall(r"^- \*\*(G-\d+)\*\*(.*)$", section(research, "refine / gaps"), re.M)
                listed, asked = interview_return(refine_step(steps, 5))
                self.check(sid, f"every gap finding was asked in the interview or dismissed {[g for g, _ in gaps]}",
                           gaps and all("Dismissed:" in rest or g in listed for g, rest in gaps))
                self.check(sid, f"the interview asked as many topics as it returned ({len(asked)} asked, "
                                f"{len(listed)} answered or open)", listed and len(asked) >= len(listed))
                review = question_texts(refine_step(steps, 7))
                req_ids = re.findall(r"^### (REQ-\d{3}) ", spec, re.M)
                self.check(sid, f"every requirement of the round is put to the review {req_ids}",
                           req_ids and all(re.search(rf"\b{r}\b", review) for r in req_ids))
                notes = re.findall(r"Dismissed:([^\n]*)", research)
                self.check(sid, f"Dismissed reasons name no finding ID ({len(notes)})",
                           all(re.search(r"\b(?:G|RS)-\d+\b", x) is None for x in notes))
                self.check(sid, "the deferred topic is an open question",
                           re.search(r"\*\*Q-\d{3}\*\*", section(spec, "Open questions")) is not None)
                self.check(sid, "report recommends another round",
                           RECOMMEND_ROUND.search(refine_report(t, steps)))
                self.slash_forms(sid, t, ["shade-kit-refine"])
            if sid == "S21":
                reqs = self.spec_checks(sid, spec)
                ids = [b[:7] for b in reqs]
                self.check(sid, f"REQ-001 to REQ-003 kept {ids}", ids[:3] == ["REQ-001", "REQ-002", "REQ-003"])
                req3 = next((b for b in reqs if b.startswith("REQ-003")), "")
                self.check(sid, "REQ-003 marked removed, not deleted",
                           re.search(r"^- Status: removed \(.+\)\s*$", req3, re.M)
                           and "**AC-003.1**" in req3)
                decided = section(spec, "Decisions")
                self.check(sid, "Q-001 and Q-002 under Decisions", "**Q-001**" in decided and "**Q-002**" in decided)
                self.check(sid, "Q-001 and Q-002 no longer open",
                           "**Q-001**" not in section(spec, "Open questions")
                           and "**Q-002**" not in section(spec, "Open questions"))
                self.check(sid, "A-001 confirmed",
                           re.search(r"\*\*A-001\*\*.*\(confirmed\)\s*$", section(spec, "Assumptions"), re.M))
                changes = [x for x in section(spec, "Changes").splitlines() if x.startswith("- ")]
                self.check(sid, f"first Changes line kept and a second appended {changes}",
                           len(changes) == 2 and changes[0] == "- 2026-11-21: created"
                           and re.match(r"^- \d{4}-\d\d-\d\d: .*removed[^;]*REQ-003", changes[1]))
                added = re.findall(r"REQ-(\d{3})", changes[1].split("added", 1)[1].split(";", 1)[0]) \
                    if len(changes) == 2 and "added" in changes[1] else []
                self.check(sid, f"an added requirement is named without its criteria {added}",
                           not [n for n in added if f"AC-{n}." in changes[1]])
                # Edited in place: lines that no canned answer touches stay byte for byte as the first round wrote them.
                kept = ["| Company account | entity | An employee's account in the company's Entra ID tenant. "
                        "Belongs to one employee. |",
                        "- **AC-001.1** Given an employee with a valid company account, when they choose \"Sign in with "
                        "SSO\", then they are signed in and see the portal start page.",
                        "- **AC-002.1** Given an employee whose company account was disabled, when they try to sign in, "
                        "then the sign-in is refused."]
                self.check(sid, "untouched lines stay unchanged (edited in place)",
                           all(line in spec.splitlines() for line in kept))
                security = section(research, "refine / security")
                self.check(sid, "the failed focus left refine / security of round 1 unchanged",
                           security.strip().startswith("Date: 2026-11-21") and "**RS-2**" in security)
                rs2 = next((x for x in security.splitlines() if x.startswith("- **RS-2**")), "")
                self.check(sid, "the obsolete guest-password point is dismissed or still in the spec",
                           "Dismissed:" in rs2 or re.search(r"password", section(spec, "Security considerations"), re.I))
                gaps = section(research, "refine / gaps")
                self.check(sid, "refine / gaps replaced with a new run",
                           re.search(r"^Date: (\d{4}-\d\d-\d\d)", gaps, re.M)
                           and not gaps.strip().startswith("Date: 2026-11-21"))
                blocks = tool_blocks(steps, "ask_user")
                retry = [b for b in blocks if re.search(r"again", b, re.I) and re.search(r"security", b, re.I)]
                self.check(sid, f"one question offers to research the failed focus again ({len(retry)})",
                           len(retry) == 1)
                self.check(sid, "research loaded a second time", steps.count("shade-kit-research") >= 2)
                report = refine_report(t, steps)
                self.check(sid, "report names the missing security research",
                           re.search(r"security[^\n]*(missing|failed|timed out)|(missing|failed)[^\n]*security",
                                     report, re.I))
                self.check(sid, "report recommends another round", RECOMMEND_ROUND.search(report))
                touched = sorted(set(re.findall(r"REQ-(\d{3})|AC-(\d{3})\.\d+", changes[1]) if len(changes) == 2
                                     else []))
                touched_reqs = sorted({f"REQ-{a or b}" for a, b in touched})
                review = question_texts(refine_step(steps, 7))
                self.check(sid, f"every requirement the round touched is put to the review {touched_reqs}",
                           touched_reqs and all(re.search(rf"\b{r}\b", review) for r in touched_reqs))
                self.slash_forms(sid, t, ["shade-kit-refine"])
            if sid == "S23":
                self.spec_checks(sid, spec)
                changes = [x for x in section(spec, "Changes").splitlines() if x.startswith("- ")]
                self.check(sid, f"Changes added at the end with one line for this round, not 'created' {changes}",
                           spec.rstrip().split("\n## ")[-1].startswith("Changes") and len(changes) == 1
                           and re.match(r"^- \d{4}-\d\d-\d\d: ", changes[0]) and "created" not in changes[0])
                self.check(sid, "the line names the changed AC-001.2",
                           changes and re.search(r"\bAC-001\.2\b", changes[0]) is not None)
                decided = section(spec, "Decisions")
                self.check(sid, "Q-001 and Q-002 under Decisions", "**Q-001**" in decided and "**Q-002**" in decided)
                self.check(sid, "REQ-003 stays active (guests keep the password login)",
                           re.search(r"### REQ-003 .*?^- Status: active", section(spec, "Requirements"), re.S | re.M))
                self.check(sid, "report recommends another round only while something is open",
                           bool(re.search(r"\*\*Q-\d{3}\*\*", section(spec, "Open questions"))
                                or re.search(r"\(unconfirmed\)", section(spec, "Assumptions")))
                           == bool(RECOMMEND_ROUND.search(refine_report(t, steps))))
                self.slash_forms(sid, t, ["shade-kit-refine"])
            if sid == "S22":
                self.check(sid, "no spec.md written", not spec_path.exists())
                self.check(sid, "no researches.md written", not research_path.exists())
                # The entry return names the general skills, so the checks look at what the research and the
                # interview would do: the research runs its own feature show, the interview asks Topic n/m questions.
                shows = [x for x in run_commands(skill_steps(steps)) if x.startswith("shade-kit feature show")]
                self.check(sid, f"no research run (one feature show, {len(shows)})", len(shows) == 1)
                self.check(sid, "no interview question", re.search(r"Topic \d+/\d+", steps) is None)
                self.check(sid, "names the missing template", ".shadekit/templates/spec.md" in t)
                self.check(sid, "repository unchanged (git status clean)", git_status(repo) == "")
                self.check(sid, "no tool call", "TOOL CALL" not in steps)
                self.codex_forms(sid, t, ["shade-kit-refine"])


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
