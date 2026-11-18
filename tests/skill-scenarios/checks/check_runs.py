"""Checks the outputs of the skill scenario runs against the expected behaviour of the skill texts.

NEVER show this file to a scenario runner: the runners must follow the skill texts without knowing what is checked.

Usage (Python 3 standard library only):

    python3 -I tests/skill-scenarios/checks/check_runs.py <work> [outdir]

<work> is the work folder written by generate_fixtures.py; the feature ids come from <work>/ids.json. [outdir] is the
folder of the outputs, relative to <work> (default: out). Exit code 0 when every check passes, 1 when one fails, 2 on a
usage error.

The expectations were written for the skill clarifications of 2026-11-18 (S1-S9 in SCENARIOS.md); when a skill text
changes what a scenario does, change the expectation here together with the skill.
"""
import json
import re
import sys
from pathlib import Path


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


class Checker:
    def __init__(self, work, out, ids):
        self.work = work
        self.out = out
        self.sso = ids["sso"]
        self.quick = ids["quick"]
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
        return self.results


def main(argv):
    if len(argv) not in (2, 3):
        print("usage: check_runs.py <work> [outdir]", file=sys.stderr)
        return 2
    work = Path(argv[1])
    out = work / (argv[2] if len(argv) > 2 else "out")
    ids_path = work / "ids.json"
    if not ids_path.is_file():
        print(f"error: {ids_path} not found; run generate_fixtures.py first", file=sys.stderr)
        return 2
    ids = json.loads(ids_path.read_text(encoding="utf-8"))

    results = Checker(work, out, ids).run()
    failed = [r for r in results if not r[2]]
    for sid, name, ok in results:
        print(f"{'PASS' if ok else 'FAIL'} {sid}: {name}")
    print(f"\n{len(results) - len(failed)}/{len(results)} checks passed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
