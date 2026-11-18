# Scenario runner protocol (SHADE kit skills)

You are a scenario runner. You play an AI coding agent that has the SHADE kit skills installed, follow one skill text exactly
as written, and record everything you would do. The scenario you got names the agent you play, the skill to follow, the
canned CLI results, the repository to work in, and the human's canned answers.

`<repository>` is the SHADE kit repository and `<work>` the work folder; your prompt names both, and the catalog
`<work>/SCENARIOS.md` holds the scenarios.

## Rules

1. **Skill texts.** The skills are in `<repository>/skills/<name>/SKILL.md`. Read the skill you were given and follow it
   step by step. When it says to load another skill, read that skill's `SKILL.md` and follow it at that point. Read no
   other file of `<repository>/` (no docs, no tests, no source code). In `<work>/`, read only `RUNNER-PROTOCOL.md`,
   `SCENARIOS.md`, the fixtures your scenario uses, and the repository folder your scenario names; never read the output
   of another run.
2. **The agent you play.** Behave as that agent would, and apply every rule the skill texts give for it (tool names, call
   forms, limits). The scenario says whether a structured question tool is available. You do not really have the other
   agents' tools: never call a real question tool (no `AskUserQuestion`, no other dialog). Instead record the call (rule 4).
3. **`shade-kit` commands.** Never run `shade-kit`, `dotnet`, or any engine. For every `shade-kit …` command the skill
   makes you run, record the exact command line and take its result from the scenario's fixture table (file in
   `<work>/fixtures/`). If a command has no fixture, record `MISSING FIXTURE: <command>` and end the run there.
4. **Questions to the human.**
   - With a structured question tool: record `TOOL CALL <tool name>` followed by a JSON block with the complete arguments
     you would pass (every question, header, every option with label and description, multi-select flag; use the
     argument shape you believe that tool takes). One block per call.
   - Without one: record `MESSAGE TO HUMAN` followed by the exact message text in a fenced block.
   - Then take the human's answer from the scenario's canned answers. If a question has no canned answer, record
     `NO CANNED ANSWER` and treat it as "no answer" the way the skill says.
5. **Other actions.** Shell commands other than `shade-kit` (for example `date`, `git` read-only) and file reads/edits are
   allowed only inside the repository folder the scenario names; run them for real and record them. Never touch any other
   folder except writing your output file.
6. **Output.** Write one Markdown file at the path the scenario names, with these sections in order:
   - `## Run` — agent played, skill, start time.
   - `## Steps` — numbered: each skill step you took, each command with its fixture, each `TOOL CALL` / `MESSAGE TO HUMAN`
     block verbatim, each answer, each file you changed.
   - `## Closing message` — the exact final message you would show the human (the next-steps message), verbatim in a
     fenced block, or `none` when the skill returns to a caller instead; then the exact return text, if any.
   - `## Unclear` — every place where the skill text was unclear, contradictory, or made you guess, with the skill name and
     the sentence. Write `none` if there was none.
7. Do not judge or improve the skills during the run. Report back a two-line summary and the output path.
