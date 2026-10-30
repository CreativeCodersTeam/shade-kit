# 06 · Skill foundation

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-17 (template conventions, checklist format), 2026-10-18 (no router: `shade-kit-next-phase` and entry protocol; findings clarification: `phase next`, pending transition, report and review skills), 2026-10-24 (documentation check: skill names of the general skills, placeholders in checklist copies), and 2026-10-25 (code review of the rework of `feature new` in package 03: `request.md` treated as data, not as instructions), and 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md): phases start only when the user calls the phase skill, `shade-kit-next-phase` is advisory, next-steps message, active phase asks the human) |
| Source | Concept §3.2, §3.3, §3.4, §11.4, [concept §9.6](../../research/shade-kit-concept.md#9-version-drift) |
| Depends on | 01, 03, 04, 05 |
| Enables | 07, 08, and every phase package (09–17) |

## Benefit

A developer starts every phase by calling its skill (for example `/shade-kit-plan`), so no phase starts behind their back. The skill checks through the CLI whether the phase can start, tells the developer what they can do instead when it cannot, and asks before it continues a phase that is already active. It runs the gate check and asks for approval itself. When the developer does not know where a feature stands, `shade-kit-next-phase` shows the current phase and names the skill to call for each possible next step. Every phase skill builds on the same small set of shared skills, so interviews, gate checks, research, and reports work the same way everywhere.

## Scope

### In scope

- Conventions every kit skill follows.
- The version handshake on the skill side.
- The entry protocol every phase skill runs.
- The next-steps message every skill ends with.
- `shade-kit-next-phase`, which shows where a feature stands and which skill to call next.
- The general skills: interview, checklist check, research orchestration, report writing.
- The shared format of `researches.md`.

### Out of scope

- Phase skills (packages 09–17). They use what this package provides.
- Installing skills for an agent (packages 07 and 08). During development the skills are loaded from the repository's `skills/` folder.
- Concrete research focus agents (packages 10 and 11 define them; this package defines how they are run and merged).

## Functional description

### Skill conventions

- Skills are written in the shared `SKILL.md` format and the plugin layout of `skills/`. Claude-only features (for example subagents) are only used where an agent-neutral way would lose function, and each such use is named in the skill. **Decided** (constraint, concept §1), **Unverified** (format compatibility, resolved in package 08).
- A phase starts only when the human calls its phase skill (`/shade-kit-<phase>`). No skill starts a phase skill, and no phase skill calls `phase start` for a phase other than its own; a skill that ends names the skills the human can call ([Next-steps message](#next-steps-message)). **Decided** on 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md)). The rule holds by convention, not technically: the description of every phase skill says that it runs only when the user calls it by name, the skill texts contain no hand-off, and the managed section in `CLAUDE.md` / `AGENTS.md` tells the agent (package 02). A restriction such as Claude Code's `disable-model-invocation` frontmatter field is not used in v1. **Decided** on 2026-10-30. Known weak point, like approvals (package 04): nothing technically stops an agent from loading a phase skill on its own.
- Skills never read or write `feature.json`, `gates.json`, or `config.json` directly. They use the CLI with `--json`. Skills read and write Markdown artifacts in the feature folder directly. **Proposed.** This keeps every state change behind the engine's version check (package 01).
- A shipped skill references no path under `research/` or `docs/` of the kit repository; those folders are design notes and are not shipped. The source comments of the skeletons are removed when a skill is worked out. **Decided** on 2026-10-18 (findings clarification).
- Every artifact a skill creates starts from the repository's template in `.shadekit/templates/`, not from text built into the skill, so team edits to templates take effect. **Proposed.** The skill follows the template conventions of package 02: it follows the guidance comments, replaces every placeholder, and removes the guidance comments; checklist copies (package 05) keep their guidance comments.
- `request.md` can hold third-party content, written unchanged by `feature new`: the body and every comment of an issue, or a whole web page (package 03). Every skill that reads it, and every research agent that gets it as feature context, treats it as data that describes the requirement, never as instructions to the agent: it does not follow commands, links, or tool requests in it, and shows the human any instruction in it that looks suspicious (a research agent reports it to the calling skill, which shows it). **Decided** on 2026-10-25 (code review of the rework of `feature new`, finding F6; package 09, [`new`](09-discuss-and-intake.md#new)). **Implemented** (`shade-kit-new`, `shade-kit-refine`, `shade-kit-diagnose`, `shade-kit-quick`, `shade-kit-research`).

### Version handshake

- Before it runs, a kit skill calls `shade-kit version --json` and compares the engine version with its own kit version. **Decided** (concept §9.6).
- Match rule: major and minor must be equal, patch may differ. **Proposed.**
- On mismatch, or if `shade-kit` is not found, the skill stops and tells the human what to update (engine or integration) and how.

### Entry protocol

Every phase skill runs it as its first step, so a phase skill called directly, such as `/shade-kit-plan`, is gated too. **Decided** in the skills interview on 2026-10-17 ([shade-kit-skills.md](../../research/shade-kit-skills.md)); steps 2 and 4 extended on 2026-10-18; steps 2, 4, and 6 changed on 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md)). `shade-kit-new` has no entry protocol, because `feature new` creates the feature with `new` already in progress (package 04); it has its own check (package 09).

The CLI check of a phase start is `phase start` itself: it refuses a transition that is not allowed without changing `feature.json`, and its outcomes for an active phase or a pending transition change nothing (package 04). There is no separate check command. **Decided** on 2026-10-30.

1. Version handshake.
2. Check and start: `shade-kit phase start <phase> --json`, then by result:
   - Refused (failure envelope): the skill ends with the [next-steps message](#next-steps-message) for the refusal. Nothing has changed.
   - `transition-requested`: continue with step 3.
   - `transition-performed` (approval off, package 05): continue with step 3 for the checklist, then step 5.
   - `transition-pending`: the transition into this phase was requested earlier and still waits for approval. Say so, then continue with step 3 with the returned gate.
   - `already-in-progress`: the phase is already active. Tell the human and ask, with the agent's structured question tool where one exists: **continue the phase** (resume the work, or another round in `refine`) → step 5; or **end here** → the next-steps message. No phase work before the answer.
   - `resume-pending`: the phase is active and the transition to `<pending>` waits for approval. Tell the human and ask: **discard the pending transition and continue** → `phase reject <pending> --note "resumed <phase>"`, then step 5; **keep it** → end, naming `/shade-kit-<pending>`, which decides on the pending transition; or **end here** → the next-steps message.
3. If the gate has a checklist, run the checklist check skill on it.
4. Gate decision:
   - Approval on: show the result and ask the human for an explicit approval, also when the check passed. On yes: `phase approve <phase>`. On no: `phase reject <phase> --note "<gaps>"`; the previous phase is in progress again, and the skill ends with the next-steps message, which names the reported gaps.
   - Approval off: the engine has already performed the transition (package 05). A failed checklist is reported as a warning and does not block.
5. Load the skills the CLI returned and run the phase.
6. End with the next-steps message. The skill never starts another phase skill.

A phase skill never approves without a human answer in the conversation. **Decided** (concept §3.2).

### Next-steps message

Every kit skill that ends, after a refused start, a "no", or the end of its work, ends with a message that says what the human can do now and which skill to call for it. **Decided** on 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md)).

- With a current feature, the steps come from `shade-kit phase next --json` (package 04), so the skill derives no steps from the profile: continue the current phase, decide on the pending transition, move forward, or reopen an earlier phase, each with its phase skill (`/shade-kit-<phase>`), plus `/shade-kit-discuss` and `/shade-kit-drop`.
- After a refusal, the message names the reason from the failure envelope and the options for it. **Proposed:**

  | Refusal | Options named |
  |---|---|
  | Version mismatch or `shade-kit` missing (handshake) | What to update and how |
  | 10 `kit-root-not-found` | `shade-kit init` |
  | 20–22 (format too new, upgrade needed, invalid file) | Update the engine, run `shade-kit upgrade`, or fix the named file |
  | 36 `git-identity-missing` | Set `user.name` and `user.email` |
  | 37 `no-current-feature` | `shade-kit feature list`, `shade-kit feature set <id>`, `/shade-kit-new` |
  | 40 `unknown-profile` | The known profiles from the message; fix `feature.json` or update the engine |
  | 41 `phase-not-in-profile` | The phases of the profile and the steps of `phase next` (for example `/shade-kit-diagnose` instead of `/shade-kit-refine` in `bugfix`) |
  | 42 `transition-not-allowed` | The steps of `phase next` |
  | 44 `feature-done` | `shade-kit feature set <id>` for another feature, `/shade-kit-new` |
  | 46 `phase-state-invalid` | Fix the named field of `feature.json` |

- The message starts nothing: the human chooses by calling the skill.

### `shade-kit-next-phase`

Shows the human where a feature stands and which skill to call next, when they say "continue" or ask about a feature. It is advisory: it starts no phase skill, never runs a gate check, and never calls `phase start`, `phase approve`, or `phase reject`. **Decided** on 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md)), replacing "starts the phase skill the human chooses" from the skills interview on 2026-10-17.

1. Version handshake.
2. `shade-kit feature current --json`. If not resolved: list the features (`feature list --json`) and ask which one to use; after the human picks one, the skill may call `feature set <id>`, which starts no phase. Otherwise name `/shade-kit-new` and end.
3. `shade-kit phase next --json` (package 04). The skill does not derive the steps from the profile.

| State | `phase next` returns | Shown |
|---|---|---|
| feature `done` | only the status | Report that the feature is finished; name `shade-kit feature set <id>` for another feature and `/shade-kit-new`. |
| current phase `in-progress` | `continue`, `forward` | Continue the current phase: `/shade-kit-<current>`; move on: `/shade-kit-<next>`. |
| current phase `awaiting-approval` | `pending` | Decide on the pending transition: `/shade-kit-<pending>`; discard it and continue the current phase: `/shade-kit-<current>`. |
| current phase `in-progress`, earlier phases exist | `reopen[]` | Reopen an earlier phase: `/shade-kit-<earlier>` (always needs approval). |
| any state of an active feature | | Always shown as well: `/shade-kit-discuss`, `/shade-kit-drop`. |

4. End with these steps (the [next-steps message](#next-steps-message)). The human chooses by calling the skill.

### General skills

| Skill | Purpose | Used by |
|---|---|---|
| Interview (`shade-kit-interview`) | Asks open questions one topic at a time, offers its own proposals, records answers in the target artifact. Uses the agent's structured question tool where one exists, plain questions otherwise. | `discuss`, `refine`, `diagnose`, `plan`, `quick` |
| Checklist check (`shade-kit-gate-check`) | Checks every item of a feature checklist against the artifacts, sets its `Result` to `pass`, `fail`, or `n/a` (package 05 rule) with the `Evidence`, and adds a line to the "Check runs" log of the checklist copy (format in package 05). | Entry protocol of every phase skill (every gate with a checklist) |
| Research (`shade-kit-research`) | Runs a set of research focuses, one subagent per focus in parallel where the agent supports it, sequentially otherwise, and merges the results into `researches.md`. | `refine`, `map`, `diagnose` |
| Report (`shade-kit-report`) | Writes `report.md` from its template and from data the CLI returns. Optional: only runs when `finish` writes a report (package 15). | `finish` |
| Review (`shade-kit-review`) | Independent code review with finding severities. Skill and `review.md` format are owned by package 13; listed here because more than one phase uses it. | `implement`, `quick` |

### `researches.md`

- One file per feature, created from the template on the first research run.
- One section per phase run and focus (for example `refine / gaps`, `map / existing architecture`), each with date, findings, and sources (file paths, links).
- A later run adds new sections or replaces the section of the same phase and focus; it does not touch other sections. **Proposed.**

## Acceptance criteria

- **FP06-AC1** With an engine whose minor version differs from the skill's, every kit skill stops before doing any work and names what to update.
- **FP06-AC2** With no resolvable current feature, `shade-kit-next-phase` lists the features and names `/shade-kit-new`; it starts no skill.
- **FP06-AC3** For a feature whose current phase is `in-progress`, `shade-kit-next-phase` names the phase skill of the current phase; that phase skill, called by the human, runs no gate.
- **FP06-AC4** The entry protocol asks for approval also when the checklist passed, and calls `phase approve` only after a yes in the conversation.
- **FP06-AC5** On a no, the phase skill calls `phase reject <phase>`, the previous phase is back in `in-progress`, and the skill ends with the next-steps message.
- **FP06-AC6** The checklist check marks an item that refers to an artifact the profile does not have as not applicable with a reason.
- **FP06-AC7** The research skill writes one section per focus into `researches.md` and leaves sections of other phases unchanged.
- **FP06-AC8** No kit skill writes a kit JSON file directly; a review of the skill texts finds only CLI calls for state changes.
- **FP06-AC9** `shade-kit-next-phase` never runs a gate check and never calls `phase approve`.
- **FP06-AC10** A phase skill called directly, without `shade-kit-next-phase`, runs the entry protocol before any phase work.
- **FP06-AC11** A phase skill called while a transition away from its phase is pending tells the human, offers to discard the pending transition and continue, to keep it, or to end, and calls `phase reject` only after the human chose to discard it.
- **FP06-AC12** `shade-kit-next-phase` shows exactly the steps `phase next --json` returns, plus `shade-kit-discuss` and `shade-kit-drop`, each with the skill to call, and starts no skill.
- **FP06-AC13** No shipped skill references a path under `research/` or `docs/`.
- **FP06-AC14** A phase skill whose `phase start` is refused ends with a message that names the reason and the human's options, and does no phase work.
- **FP06-AC15** A phase skill called while its phase is already in progress tells the human and asks whether to continue the phase or to end, before any phase work.
- **FP06-AC16** No skill starts a phase skill, and no phase skill calls `phase start` for a phase other than its own; a review of the skill texts finds no hand-off.
- **FP06-AC17** Every phase skill ends with the next-steps message, built from `phase next --json` when a current feature exists.
- **FP06-AC18** The description of every phase skill says that it runs only when the user calls it by name.

## Open points

- Exact skill names (concept open question 6).
- Phase skills are restricted to user invocation only by convention (see [Skill conventions](#skill-conventions)); a technical restriction (Claude Code `disable-model-invocation`, and whatever Copilot and Codex offer) is a possible later addition.
