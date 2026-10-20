# SHADE kit skills

Status: draft, 2026-10-17, updated 2026-10-18 (consistency review: entry protocol with re-entry and rejection, `quick` phase, seven research agents; findings clarification: phase and workflow skills, optional report skill, pending transition, `phase next`). Result of an interview on [shade-kit-concept.md](shade-kit-concept.md). Lists the skills SHADE kit v1 needs and what each one does roughly. Design notes only; nothing here is shipped.

## 1. Decisions

- **One skill per phase**, including the thin phases `new` and `finish`. Each phase can later be replaced on its own through the `replace` extension slot (concept section 7).
- **Phase skills and workflow skills.** Phase skills run a phase of a profile (`new`, `refine`, `map`, `diagnose`, `plan`, `implement`, `quick`, `verify`, `finish`). Workflow skills (`next-phase`, `discuss`, `drop`) belong to no profile; they run no entry protocol, only the version handshake. `discuss` and `drop` are not phases (concept §5.4, package 04).
- **No router skill.** It is replaced by `shade-kit-next-phase`, which finds the current phase, offers the possible next phases, lets the user choose, and starts the chosen phase.
- **Phase skills are profile-aware.** They read the profile from `shade-kit feature current --json` and adjust their behaviour (`bugfix`: short plan, failing test first).
- **`quick` is a phase of its own** (2026-10-18). The `quick` profile is `new` → `quick` → `finish`, and `shade-kit-quick` is the skill of the `quick` phase: a mini run of refine, plan, and implement, not an implement mode. So every phase has exactly one skill named `shade-kit-<phase>`.
- **General skills** are separate skills only when more than one phase uses them, or when they are optional. `simplify` stays inline in `shade-kit-implement`. The report is the optional general skill `shade-kit-report`, which `shade-kit-finish` loads only when a report is written (`report` in `config.json`: `ask`, `always`, `never`; package 15). Decided on 2026-10-18.
- **Every phase skill runs the entry protocol itself** as its first step, so a direct call such as `/shade-kit-plan` is gated too. `shade-kit-next-phase` only chooses and hands off. Exception: `shade-kit-new` has no entry protocol, because `shade-kit feature new` creates the feature with `new` already in progress.

Entry protocol of every phase skill (from concept section 11.4, extended on 2026-10-18):

1. Version handshake: `shade-kit version --json`. Stop on a mismatch and name what to update.
2. `shade-kit phase start <phase> --json`. If the phase is already in progress (new session, another `refine` round), the engine changes nothing and returns no gate: go to step 5. If a transition away from this phase is pending, the engine changes nothing and reports it: ask the human whether to discard it; on yes `shade-kit phase reject <pending> --note "resumed <phase>"` and go to step 5, on no hand off to `shade-kit-next-phase` and stop.
3. `shade-kit-gate-check` against the returned checklist, if the checklist is switched on.
4. If the gate needs approval: show the result and ask the human.
   - Yes: `shade-kit phase approve <phase>`.
   - No: `shade-kit phase reject <phase> --note "<gaps>"`. The previous phase is in progress again; hand off to `shade-kit-next-phase` and stop.
   - If approval is switched off, the engine has already performed the transition; a failed checklist is reported as a warning only.
5. Load the skills the CLI returned and run the phase.

## 2. Phase skills

Called by the user, directly or through `shade-kit-next-phase`. Each runs the entry protocol (exception: `shade-kit-new`).

| Skill | Purpose |
|---|---|
| `shade-kit-new` | Creates a feature with a profile (`feature.json`, `request.md`) from text, an issue URL, a file, or discussion notes. |
| `shade-kit-refine` | Clarifies the requirement through interview and research. Writes `spec.md` (requirement IDs, acceptance criteria, out of scope). Can run several times. |
| `shade-kit-map` | Maps the feature onto the code base with parallel research agents. Writes `researches.md`. |
| `shade-kit-diagnose` | `bugfix` profile: reproduces the bug, finds the root cause, and runs the code-level security research. |
| `shade-kit-plan` | Writes `plan.md` and `tasks.md` with the requirement → test mapping. Short variant for `bugfix`. |
| `shade-kit-implement` | Loops implement → simplify (inline) → review until the review has no blockers. Produces code, tests, and `review.md`. `bugfix`: failing test first. |
| `shade-kit-quick` | `quick` phase (`quick` profile): clarification, plan, and implementation in one run, one approval at the end. |
| `shade-kit-verify` | Runs the quality checks, checks that every acceptance criterion is covered by a passing test, records the results in `verification.md`, and writes `playbook.md` for manual tests. |
| `shade-kit-finish` | Sets the feature status to `done`; loads `shade-kit-report` when a report is written. |

## 2a. Workflow skills

Called by the user; outside every profile. No entry protocol, only the version handshake.

| Skill | Purpose |
|---|---|
| `shade-kit-next-phase` | Finds the current feature and phase, offers the next steps from `shade-kit phase next --json`, lets the user choose, and starts the chosen phase skill. |
| `shade-kit-discuss` | Free brainstorming on any topic. Can run as input for `new` or in any other phase. |
| `shade-kit-drop` | Deletes the feature and clears the current feature. |

## 3. General skills

Loaded by the phase skills.

| Skill | Purpose | Used by |
|---|---|---|
| `shade-kit-interview` | Structured clarification interview. | discuss, refine, diagnose, plan, quick |
| `shade-kit-gate-check` | Checks a checklist (DoR, DoD) against the feature artifacts and reports the result. | Entry protocol of every phase skill |
| `shade-kit-research` | Starts the research agents in parallel and merges their results into `researches.md`. | refine, map, diagnose |
| `shade-kit-review` | Code review with blocker classification (owned by package 13). | implement, quick |
| `shade-kit-report` | Writes `report.md`. Optional: only when a report is written. | finish |

## 4. Out of scope

- The seven research agents from concept section 3.3 (security is split into requirement level for `refine` and code level for `map` and `diagnose`). They are subagent definitions, not skills, but `shade-kit-research` depends on them.
- Extensions, memory, and the `refactor` profile (later).
- Skill content, file formats, and CLI details.

## 5. Follow-ups

1. ~~`shade-kit-next-phase` needs a CLI command that returns the allowed next phases.~~ Resolved 2026-10-18: `shade-kit phase next --json` (package 04, concept section 3.2).
2. **Unverified:** whether GitHub Copilot and OpenAI Codex CLI support subagents. This decides whether `shade-kit-research` can run its agents in parallel there (concept open question 5).
