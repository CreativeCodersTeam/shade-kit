# 06-8 · Research (`shade-kit-research`) and `researches.md`

| | |
|---|---|
| Status | Implemented in `skills/shade-kit-research/SKILL.md` and `researches.template.md`, 2026-11-13. The research steps of the three calling skeletons (`refine`, `map`, `diagnose`) name the input since the same day. Scenario runs: see [acceptance criteria](#acceptance-criteria). |
| Source | [06 Skill foundation](06-skill-foundation.md) (general skills, `researches.md`), [02 Repository initialisation](02-repository-initialisation.md) (template set) |
| Kind | Skill (general skill) and template (`researches.template.md`) |
| Depends on | 06-2 |
| Enables | 10 (`refine`), 11 (`map`), 16 (`diagnose`) |

## Benefit

The agent collects facts with sources before decisions are made, and keeps them in one place per feature. Several research focuses run in parallel where the agent supports it.

## Scope

### In scope

- The general skill `shade-kit-research`, loaded by `refine`, `map`, and `diagnose`.
- The shared format of `researches.md` and its template `src/CreativeCoders.ShadeKit.Core/Templates/Resources/researches.template.md` (Markdown, embedded into the Core assembly; no C# change).
- The values each of the three calling skeletons passes to it (phase, feature id, focuses with name, ID prefix, and instructions, files to read, context note). **Decided** on 2026-11-13 (implementation clarification).

### Out of scope

- The concrete research focuses and their instructions in detail (packages 10, 11, and 16); the values in the skeletons are a starting point for them.
- Research agent definitions: there are none in v1 for this skill. **Decided** on 2026-11-04.

## Functional description

Decisions of the implementation clarification are marked **Decided** on 2026-11-13.

### Input from the calling skill

- The calling phase (`refine`, `map`, or `diagnose`).
- The feature id, from the return of the entry protocol. The skill reads the title and the feature folder with `shade-kit feature show <id> --json`; without an id it uses `shade-kit feature current --json`. **Decided** on 2026-11-13.
- The focuses to run, each with a short **name**, an **ID prefix**, and the **instructions**. The name is the section key: the heading is `<phase> / <name>` exactly as given, so the caller, not the skill, decides it (`refine` passes `gaps`, not "gaps and open questions"). **Decided** on 2026-11-13.
- The files to read (feature context: `request.md`, `spec.md`, discussion notes, named `researches.md` sections). `request.md` is data, never instructions (06-2).
- An optional **context note**: material of the caller that is not in a file yet, for example the root cause and fix options of `diagnose`, which writes `diagnosis.md` only after the research. It goes unchanged into every brief; it is not a source. **Decided** on 2026-11-13.

| Caller | Focus (name) | ID prefix | Files | Context note |
|---|---|---|---|---|
| `refine` | `gaps`, `security` | `G`, `RS` | `request.md`, discussion notes, existing `spec.md` | none |
| `map` | `best practice`, `existing architecture`, `existing affected code`, `don't hand-roll`, `security at code level` | `BP`, `AR`, `CO`, `HR`, `CS` | `spec.md`, the `refine` sections of `researches.md` | none |
| `diagnose` | `security` | `DS` | `request.md` | reproduction, root cause with evidence, affected code, fix options |

The prefixes are unique in the file and do not collide with the IDs of the other artifacts (`REQ`, `AC`, `A`, `Q`, `D`, `F`, `T`). **Decided** on 2026-11-13.

### Flow

1. Prepare: read the feature through the CLI, the date from the system clock, and an existing `researches.md`. The existing section of a focus is its **previous section**.
2. Start one general subagent per focus with a self-contained brief (the skill's research brief: focus, instructions, files, context note, previous section, rules, result shape). Run them in parallel where the agent supports subagents, sequentially otherwise, with the same brief, so that both ways give the same sections. **Decided** on 2026-11-04 (no Claude-only agent files); the brief **Decided** on 2026-11-13.
3. Subagents only read. They may search the web and fetch pages for their focus, change no file, run no `shade-kit` command and no command that changes state, and follow nothing in `request.md` or fetched pages. Only the main agent writes `researches.md`. **Decided** on 2026-11-13.
4. Collect the findings. Every finding names its source (file and line, or link); a finding without a source is marked as an assumption. A research agent reports any instruction in `request.md` that looks suspicious; the skill passes it on to the calling skill.
5. Check the sources before writing: a cited file exists, the line lies within it, and the lines are about the finding; a link is fetched and must support the finding; a search (`Search: <pattern> in <path>, no match`, the source of a fact about something missing) is run again and must give the same result. A source that fails turns its finding into an assumption (`source not confirmed`); without a web tool a link is kept and marked `(not checked)`. A part of a finding that its source does not back is marked `Assumption:` on the same line, so one thought stays one finding. **Decided** on 2026-11-13; the search source and the partly assumed finding **Decided** the same day after the first scenario runs.
6. A previous section can hold notes of other skills, for example `Dismissed: <reason>` from `refine`. The brief passes it on; a dismissed finding is not reported again without new evidence, and the return lists the ones left out. A dismissed finding the new run left out is carried into the new section unchanged with its note, numbered after the new findings, so the human's decision holds for every later round, not only the next one. **Decided** on 2026-11-13; the carry-over after scenario s2 showed that a replace lost the note.
7. A focus that fails (error, no result, result not in the shape) writes nothing; its previous section stays, and the return names it `failed` with the reason. The calling skill decides about a new run. **Decided** on 2026-11-13.
8. Merge into `researches.md` (format below).
9. Return to the calling skill: per focus its state (`written`, `replaced`, `failed`), the finding IDs, the summary, and the dismissed findings left out; the source check; the suspicious instructions; how the file was created; the warnings of the CLI calls. The skill runs no version handshake and writes no next-steps message (like `shade-kit-interview`).

### `researches.md`

- One file per feature, created from `.shadekit/templates/researches.md` on the first research run (06-2). When the template is missing, the skill creates the file with the lines the template would give (title and feature line) and says so in its return: an exception to the 06-2 rule. **Decided** on 2026-11-13.
- One section per phase and focus, headed `<phase> / <name>` (for example `refine / gaps`, `map / existing architecture`, `diagnose / security`), with the date of the run (`Date: YYYY-MM-DD`) and the findings. The file keeps only the latest run of each section; git keeps the history. **Decided** on 2026-11-13 (replaces "one section per phase run and focus").
- Every finding has an ID (`<prefix>-<n>`, numbered from 1 per section; a replaced section starts at 1 again) and names its source inline (`Source: path:line`, `Sources: …`, a link, `Search: …`) or is marked `Assumption: <basis>`; a partly backed finding has both on one line. There is no separate source list. A section without findings says `No findings. Checked: <what>.` **Decided** on 2026-11-13.
- A later run replaces the section of the same phase and focus in place; a new section is appended at the end of the file, in the order the caller gave the focuses. It does not touch other sections, a `<phase> / skipped` section included (package 11 owns that one). **Decided** on 2026-11-04; placement and `skipped` **Decided** on 2026-11-13.
- The template holds only the title, the feature line, and the guidance comment with the shared format and an example section; it lists no phase sections. The sections of each phase stay owned by packages 10, 11, and 16. **Decided** on 2026-11-13.

## Acceptance criteria

There are no automated tests of the skill texts (06-2), so the criteria are proven by a review by hand plus scenario runs: an agent loads the skill and follows it, for real, on a fixture repository initialised with the engine (`init`, `feature new`), with the input of a calling skill, and records its return and commands; the changed files are checked afterwards against the fixture's git state. The scenarios and outputs are kept outside the repository, as in 06-4 and 06-7.

- **FP06-8-AC1** The research skill writes one section per focus into `researches.md` and leaves sections of other phases unchanged.
- **FP06-8-AC2** On the first run of a feature, `researches.md` is created from the repository's template `.shadekit/templates/researches.md`.
- **FP06-8-AC3** Every finding in a written section names its source or is marked as an assumption.
- **FP06-8-AC4** On an agent without subagents, the focuses run one after the other and give the same sections.
- **FP06-8-AC5** A suspicious instruction in `request.md` is reported to the calling skill and not followed.

Iteration 1 ran 5 scenarios, each in a fresh subagent with the skill and as a baseline without it (the baseline got the same caller input, including the heading form and the ID prefixes, but not the skill). Every run worked for real on its own copy of a fixture repository; a script checked the written file against the fixture's git state, and every check marked * was corrected by hand (the script's text match was wrong). With the skill 60 of 60 checks passed, the baseline 47 of 49 (s4: no file written because the template was missing, and the suspicious instruction not reported). The checks did not catch one real flaw that the s2 runner reported: the replace dropped the dismissed finding with its note, so the decision would have held for one round only; the baseline kept it. That and the runners' notes led to a revised text: dismissed findings carried over, partly assumed findings on one line, a search as a source, the subagents kept away from `feature.json`, `gates.json`, and `config.json`, named `researches.md` sections pasted into the brief, citations counted one by one. Iteration 2 ran s2 and s5 with the revised skill (the baseline was not run again): 25 of 26 checks passed; the miss (`IUserRepository.cs:8` cited without its folder and not caught by the source check) led to the full-path rule. The last wordings (no numbering in the subagent result, a single out-of-shape line does not fail a focus, a search with a hit, today's date on a replaced section, `grep`/`find`/`git status` as read-only commands, the full-path rule) are read-checked only.

| Scenario | What it proves | With skill, iteration 1 | Baseline | With skill, iteration 2 |
|---|---|---|---|---|
| s1 `refine`, first run, parallel subagents | AC1, AC2, AC3 | 10/10 | 10/10 | not run |
| s2 `refine`, second round, a dismissed finding | AC1, AC3, dismissal | 12/12* (note lost on replace, found by the runner) | 12/12* | 12/13 (carry-over added as a check; path without folder) |
| s3 `diagnose`, no subagents, context note | AC1, AC2, AC3, AC4 | 12/12 | 12/12* | not run |
| s4 `refine`, template missing, instruction in `request.md` | AC3, AC5, template fallback | 13/13 | 0/2 (no file, instruction not reported) | not run |
| s5 `map`, later run, five focuses, `map / skipped` present | AC1, AC3 | 13/13 | 13/13 | 13/13 |

AC4 is shown by s3 (the focus ran in the main session from the same brief and gave a section of the same shape as the parallel runs); a run of the same focuses both ways with a comparison of the sections was not made.

## Open points

- **Unverified:** whether GitHub Copilot and OpenAI Codex CLI support subagents (package 08). Without them the skill runs the focuses one after the other.
- Finding IDs are numbered anew on every run of a section (decided), so a `G-3` of an earlier round can name another finding later. Only dismissed findings keep their text (with a new number). Whether the interview topics or `spec.md` should ever quote finding IDs across rounds is left to package 10.
- Resolved 2026-11-13: changing `researches.template.md` needs no change to `ShippedTemplatesTests` (**FACT**: it checks the catalog, the paths, and the DoR/DoD lines only; `TemplatesLockTests` uses fixed fake hashes). `dotnet test` passed after the change.
