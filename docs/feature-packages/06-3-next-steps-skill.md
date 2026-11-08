# 06-3 · Next-steps message (`shade-kit-next-steps`)

| | |
|---|---|
| Status | Draft, 2026-11-04 (split from package 06), updated 2026-11-08 (implementation: `skills/shade-kit-next-steps/SKILL.md`; the steps of a pending transition and of a `done` feature, the refusal table keyed by error code, a missing `shade-kit`, a failing `phase next`, repeated warnings, the closing line of a refusal). AC1 to AC4 implemented; loading the skill from other skills follows in 06-5, 06-6, and 06-9. |
| Source | [06 Skill foundation](06-skill-foundation.md) (next-steps message), [phase start analysis](../../research/shade-kit-phase-start-analysis.md) |
| Kind | Skill (new general skill) |
| Depends on | 06-2 |
| Enables | 06-5, 06-6, 06-9 |

## Benefit

Every kit skill ends the same way: the human sees what they can do now and which skill to call for it, also after a refused start or a "no". One general skill writes this message, so it does not drift between skills.

## Scope

### In scope

- The general skill `shade-kit-next-steps`, loaded by every kit skill that ends.
- The steps with a current feature, the refusal table, and the display of CLI warnings.

### Out of scope

- The version handshake: the calling skill has checked the version already (06-2).
- Loading the skill from the phase skills and from `new`, `discuss`, `drop` (06-9) and from `shade-kit-next-phase` (06-6).

## Functional description

### Input from the calling skill

- Why the skill ends: end of its work, a "no" (with the reported gaps), or a refusal (the failure envelope).
- The warnings of the CLI calls of the calling skill.

### Message

Every kit skill that ends, after a refused start, a "no", or the end of its work, ends with a message that says what the human can do now and which skill to call for it. **Decided** on 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md)). The message is written by the general skill `shade-kit-next-steps`. **Decided** on 2026-11-04.

- With a current feature, the steps come from `shade-kit phase next --json` (package 04), so the skill derives no steps from the profile: continue the current phase, decide on the pending transition, move forward, or reopen an earlier phase, each with its phase skill (`/shade-kit-<phase>`), plus `/shade-kit-discuss` and `/shade-kit-drop`.
- After a "no", the message also names the gaps the calling skill reported.
- After a refusal, the message names the reason from the failure envelope and the options for it. **Proposed:**

  | Refusal | Options named |
  |---|---|
  | Version mismatch (50 `version-mismatch`, 06-1) or `shade-kit` missing (handshake) | What to update and how, from the message |
  | 10 `kit-root-not-found` | `shade-kit init` |
  | 17 `kit-file-write-failed` | Check the named file or folder in `.shadekit/` and its permissions, then call the skill again |
  | 20–22 (format too new, upgrade needed, invalid file) | Update the engine, run `shade-kit upgrade`, or fix the named file |
  | 36 `git-identity-missing` | Set `user.name` and `user.email` |
  | 37 `no-current-feature` | `shade-kit feature list`, `shade-kit feature set <id>`, `/shade-kit-new` |
  | 40 `unknown-profile` | The known profiles from the message; fix `feature.json` or update the engine |
  | 41 `phase-not-in-profile` | The phases of the profile and the steps of `phase next` (for example `/shade-kit-diagnose` instead of `/shade-kit-refine` in `bugfix`) |
  | 42 `transition-not-allowed` | The steps of `phase next` |
  | 44 `feature-done` | `shade-kit feature set <id>` for another feature, `/shade-kit-new` |
  | 46 `phase-state-invalid` | Fix the named field of `feature.json` |
  | 47 `checklist-template-not-found` | Restore the named checklist template in `.shadekit/templates/checks/`, or run `shade-kit upgrade` |
  | Any other code | The message of the failure envelope; no further options |

  Rows 17, 47, and the fallback row added on 2026-11-04. **Decided** on 2026-11-04 (rows); the table stays **Proposed** as a whole.
- For the refusals 41, 42, and 44 the skill also calls `phase next --json`. For 37, 40, 46, and 20–22 `phase next` fails with the same code, so the options come from the refusal itself (package 04).
- The warnings of `phase start` and `phase next` (for example `gates-defaults-used`, `gate-entry-ignored`, `engine-prerelease-version-mismatch`) are always shown with their message. **Decided** on 2026-11-04.
- The message starts nothing: the human chooses by calling the skill.
- **Implemented** in `skills/shade-kit-next-steps/SKILL.md` (2026-11-08). The following decisions came out of the implementation clarification; **Decided** on 2026-11-08 by the user unless marked otherwise:
  - Pending transition (current phase `awaiting-approval`): the message names the pending step, `/shade-kit-<pending>`, and also discarding it and continuing the current phase with `/shade-kit-<current>`. The phase skill asks before it discards anything (resume-pending, 06-5). The second step is taken from `current`, not from the profile.
  - `done` feature (`phase next` returns only `current`): the message says that the feature is finished and names `shade-kit feature set <id>`, `/shade-kit-new`, `/shade-kit-discuss`, and `/shade-kit-drop`.
  - Rows 20–22 and 47 name `shade-kit upgrade` as the table says, although the command comes only with package 18 (see open points).
  - **Proposed** (defaults of the implementation, not confirmed yet):
    - The refusal table is keyed by the error code: the failure envelope carries only `error.code` and `error.message`, no exit code (01). The exit code is shown only to help the reader.
    - `shade-kit` not found (there is no envelope): install the engine (the dotnet global tool or the self-contained binary), put it on `PATH`, and call the skill again.
    - When the skill's own `phase next` fails, in any case, its envelope goes through the refusal table, and `phase next` is not run again. Example: after `shade-kit-drop` there is no current feature, so `no-current-feature` applies.
    - A warning that comes twice with the same code and message is shown once, for example `gates-defaults-used` from `phase start` and from `phase next`.
    - When the options of a refusal fix its cause, they end with calling the calling skill again. This applies to every row except 37, 41, 42, 44, and the fallback.
    - `pending` and `forward` name the checklist of their gate when it has one. `forward` and every reopen are marked as needing an approval when they need one.
    - The message order: what happened, the gaps after a "no", the current feature, phase, and state, the options, the warnings.

## Acceptance criteria

Status as of 2026-11-08. There are no automated tests of the skill texts (06-2), so each criterion names how it is proven. The proof is a review by hand plus scenario runs. In each run a subagent loads the skill, follows it with canned CLI envelopes, and records the commands it would run and the message. There are 8 scenarios: in progress with a reopen, a "no" with a pending transition and a repeated warning, `done`, refusals 37, 42, 47, and an unknown code, and the end of `drop` with a failing `phase next`. The scenarios and outputs were kept outside the repository. Skill-creator's `quick_validate.py` reports the frontmatter valid for all 18 skills.

- **FP06-3-AC1** With a current feature, the message shows exactly the steps `phase next --json` returns, each with the skill to call, plus `/shade-kit-discuss` and `/shade-kit-drop`. **Implemented.** Two additions to "exactly" were decided on 2026-11-08: the step that discards a pending transition and the options of a `done` feature (functional description). Proven in the scenarios: in progress, pending, done, and 42.
- **FP06-3-AC2** After a refusal, the message names the reason from the failure envelope and the options of its row in the refusal table; for a code without a row it shows the message of the failure envelope. **Implemented.** Proven in the scenarios: 37, 42, 47, the unknown code `git-unavailable`, and the failing `phase next` after `drop`.
- **FP06-3-AC3** Every warning of `phase start` and `phase next` that the calling skill passes or the skill receives is shown. **Implemented.** Proven in the scenarios: a warning that is both passed in and received is shown once, and a passed warning is shown when `phase next` fails.
- **FP06-3-AC4** The skill starts no skill and calls no command that changes the state (`phase start`, `phase approve`, `phase reject`, `phase complete`, `feature set`). **Implemented.** The rules of the skill allow only `shade-kit phase next --json`. In every scenario no command other than that one was run.

## Open points

- The refusal table is **Proposed**.
- Rows 20–22 and 47 name `shade-kit upgrade`, which comes only with package 18; until then the hint points to a command that does not exist yet (2026-11-08).
- The defaults of the implementation marked **Proposed** above.
