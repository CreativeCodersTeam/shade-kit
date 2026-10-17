# SHADE kit skills

Status: draft, 2026-10-17. Result of an interview on [shade-kit-concept.md](shade-kit-concept.md). Lists the skills SHADE kit v1 needs and what each one does roughly. Design notes only; nothing here is shipped.

## 1. Decisions

- **One skill per phase**, including the thin phases `new`, `drop`, and `finish`. Each phase can later be replaced on its own through the `replace` extension slot (concept section 7).
- **No router skill.** It is replaced by `shade-kit-next-phase`, which finds the current phase, offers the possible next phases, lets the user choose, and starts the chosen phase.
- **Phase skills are profile-aware.** They read the profile from `shade-kit feature current --json` and adjust their behaviour (`bugfix`: short plan, failing test first). Only the `quick` profile gets its own skill, because "all in one" is a mini run of refine, plan, and implement, not an implement mode.
- **General skills** are separate skills only when more than one phase uses them. `simplify` stays inline in `shade-kit-implement`, the report stays inline in `shade-kit-finish`.
- **Every phase skill runs the entry protocol itself** as its first step, so a direct call such as `/shade-kit-plan` is gated too. `shade-kit-next-phase` only chooses and hands off.

Entry protocol of every phase skill (from concept section 11.4):

1. `shade-kit phase start <phase> --json`
2. `shade-kit-gate-check` against the returned checklist, if the checklist is switched on
3. Ask the human for approval, then `shade-kit phase approve <phase>`
4. Run the phase

## 2. Phase skills

Called by the user, directly or through `shade-kit-next-phase`.

| Skill | Purpose |
|---|---|
| `shade-kit-next-phase` | Finds the current feature and phase, offers the allowed next phases, lets the user choose, and starts the chosen phase skill. |
| `shade-kit-discuss` | Free brainstorming on any topic. Outside every profile; can run as input for `new` or in any other phase. |
| `shade-kit-new` | Creates a feature with a profile (`feature.json`, `request.md`) from text, an issue URL, a file, or discussion notes. |
| `shade-kit-refine` | Clarifies the requirement through interview and research. Writes `spec.md` (requirement IDs, acceptance criteria, out of scope). Can run several times. |
| `shade-kit-map` | Maps the feature onto the code base with parallel research agents. Writes `researches.md`. |
| `shade-kit-diagnose` | `bugfix` profile: reproduces the bug and finds the root cause. |
| `shade-kit-plan` | Writes `plan.md` and `tasks.md` with the requirement → test mapping. Short variant for `bugfix`. |
| `shade-kit-implement` | Loops implement → simplify (inline) → review until the review has no blockers. Produces code, tests, and `review.md`. `bugfix`: failing test first. |
| `shade-kit-quick` | `quick` profile: clarification, plan, and implementation in one run, one approval at the end. |
| `shade-kit-verify` | Runs the quality checks, checks that every acceptance criterion is covered by a passing test, and writes `playbook.md` for manual tests. |
| `shade-kit-finish` | Sets the feature status to `done` and writes the report (inline). |
| `shade-kit-drop` | Deletes the feature and clears the current feature. |

## 3. General skills

Loaded by the phase skills.

| Skill | Purpose | Used by |
|---|---|---|
| `shade-kit-interview` | Structured clarification interview. | discuss, refine, diagnose, plan, quick |
| `shade-kit-gate-check` | Checks a checklist (DoR, DoD) against the feature artifacts and reports the result. | Entry protocol of every phase skill |
| `shade-kit-research` | Starts the research agents in parallel and merges their results into `researches.md`. | refine, map |
| `shade-kit-review` | Code review with blocker classification. | implement, quick |

## 4. Out of scope

- The six research agents from concept section 3.3. They are subagent definitions, not skills, but `shade-kit-research` depends on them.
- Extensions, memory, and the `refactor` profile (later).
- Skill content, file formats, and CLI details.

## 5. Follow-ups

1. `shade-kit-next-phase` needs a CLI command that returns the allowed next phases (proposal: `shade-kit phase next --json`). Concept section 3.2 has none yet. Without it, the skill would derive the next phases from the profile and duplicate engine logic.
2. **Unverified:** whether GitHub Copilot and OpenAI Codex CLI support subagents. This decides whether `shade-kit-research` can run its agents in parallel there (concept open question 5).
