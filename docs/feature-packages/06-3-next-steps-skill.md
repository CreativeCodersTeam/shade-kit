# 06-3 · Next-steps message (`shade-kit-next-steps`)

| | |
|---|---|
| Status | Draft, 2026-11-04 (split from package 06) |
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

## Acceptance criteria

- **FP06-3-AC1** With a current feature, the message shows exactly the steps `phase next --json` returns, each with the skill to call, plus `/shade-kit-discuss` and `/shade-kit-drop`.
- **FP06-3-AC2** After a refusal, the message names the reason from the failure envelope and the options of its row in the refusal table; for a code without a row it shows the message of the failure envelope.
- **FP06-3-AC3** Every warning of `phase start` and `phase next` that the calling skill passes or the skill receives is shown.
- **FP06-3-AC4** The skill starts no skill and calls no command that changes the state (`phase start`, `phase approve`, `phase reject`, `phase complete`, `feature set`).

## Open points

- The refusal table is **Proposed**.
