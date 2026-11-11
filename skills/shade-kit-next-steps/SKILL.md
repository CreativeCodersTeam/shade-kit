---
name: shade-kit-next-steps
description: SHADE kit general skill that writes the closing message of a kit skill. Shows what the human can do now and which skill to call for it, after the end of the work, a "no", or a refused start, and shows the warnings of the CLI calls. Starts nothing. Loaded by every SHADE kit skill that ends; not meant to be called directly.
metadata:
  version: 0.7.0
---

# shade-kit-next-steps

## Purpose

Ends a kit skill with one message: what the human can do now, and which skill to call for each step. Every kit skill ends this way, also after a refused start or a "no", so the human never has to guess how to go on, and the message does not drift between skills.

The message is advisory. A phase starts only when the human calls its phase skill, so this skill names skills and commands but starts none of them and changes no state.

## Input from the calling skill

The calling skill has already run its version handshake; this skill does not check the version again.

- **Why the skill ends**, one of:
  - **end of work**: the calling skill finished what it does;
  - **no**: the human said no to an approval; the calling skill passes the gaps it reported;
  - **refusal**: a CLI call failed; the calling skill passes the failure envelope, `{ "ok": false, "error": { "code": "...", "message": "..." }, "warnings": [...] }`, or reports that `shade-kit` was not found.
- **Warnings**: the `warnings` (`{ code, message }`) of the CLI calls of the calling skill, for example of `phase start`.
- Optionally the result of a `shade-kit phase next --json` that the calling skill ran in this run (for example `shade-kit-next-phase`). Use it instead of running the command again.

## Flow

1. **Refusal.** The reason is `error.message` of the envelope, quoted as it is. Find the options in the [refusal table](#refusal-table) by `error.code`.
   - For `phase-not-in-profile`, `transition-not-allowed`, and `feature-done`, also get the steps (step 3).
   - For every other code, do not run `phase next`. For `no-current-feature`, `unknown-profile`, `phase-state-invalid`, and the format errors it would fail with the same code, so the options come from the refusal alone.
2. **No, or end of work.** Get the steps (step 3).
3. **Steps from `phase next`.** If the calling skill passed a result of `phase next` from this run, use it and do not run the command: a second call reads the same state and only repeats the warnings. Otherwise run `shade-kit phase next --json`, once.

   Whenever this skill's own `phase next` fails, in any case, do not run it again: add the options of the refusal-table row of its `error.code` and show its `error.message` as the reason there are no steps. After `shade-kit-drop`, for example, the drop worked, there is no current feature any more, and `phase next` fails with `no-current-feature`.

   On success, take the steps from the result and do not derive any from the profile: the engine knows the profile, the gates, and which phases can be reopened, and a second copy of that logic in a skill drifts. `phase next` reads the state now; when it does not match what the calling skill expected, the result is right. Use the `skill` field of each step as the skill to call (`/<skill>`).
   - `continue`: continue the current phase with `/<continue.skill>`.
   - `pending`: a transition to `pending.phase` waits for approval. Name two steps: decide on it with `/<pending.skill>`; or discard it and continue the current phase with `/shade-kit-<current.phase>` (that phase skill asks before it discards anything).
   - `forward`: move on to the next phase with `/<forward.skill>`, marked "needs approval" when `gate.approval` is `true`.
   - `reopen`: for each entry, reopen that earlier phase with `/<skill>`, marked "needs approval" (a reopen always does). `[]` and `null` add nothing.
   - For `pending` and `forward`, name the checklist of the gate (`gate.checklist`) when it is not `null`; otherwise say nothing about a checklist.
   - `current.status` is `done` (every other field is `null`): say that the feature is finished, and name `shade-kit feature set <id>` to work on another feature and `/shade-kit-new` for a new one.
   - Whenever `phase next` succeeded, `done` included, also name `/shade-kit-discuss` and `/shade-kit-drop`.
4. **Warnings.** Collect the warnings the calling skill passed and those of this skill's own `phase next` call. Show each one with its code and message. When the same code and message come twice (for example `gates-defaults-used` from both `phase start` and `phase next`), show it once.
5. **Write the message** and end. Keep this order, and leave out parts that are empty:
   1. What happened, in one line, from the calling skill's point of view: its work is done, the human said no (name the transition), or its call was refused, with `error.message` as the reason.
   2. After a "no": the gaps the calling skill reported.
   3. The current feature, phase, and state from `current`, when `phase next` succeeded; add the status only when it is `done`. When `phase next` failed, its `error.message` instead.
   4. The options: the steps from step 3 and the options of the refusal row, each with the skill or command to call. When the options of a refusal fix its cause (every row except `no-current-feature`, `phase-not-in-profile`, `transition-not-allowed`, `feature-done`, and the fallback), end them with calling the calling skill again.
   5. The warnings.

   Do not ask which step the human wants and do not offer to run one. The human chooses by calling the skill.

Example after the end of work in `refine`: the phase is in progress, `forward` is `map` with an approval and no checklist, `reopen` is `[]`, and `phase next` returned one warning.

```text
Refine is done for now.

Feature: sso-login · phase refine (in-progress)

What you can do now:
- Continue refine: /shade-kit-refine
- Move on to map (needs approval): /shade-kit-map
- Talk it through: /shade-kit-discuss
- Drop the feature: /shade-kit-drop

Warnings:
- gates-defaults-used: '.shadekit/gates.json' does not exist, so the built-in gate defaults apply. ...
```

Example after a refused `phase start plan` with `checklist-template-not-found` (no `phase next` for this code):

```text
Plan could not start: The checklist template '.shadekit/templates/checks/definition-of-ready.md' of the gate into 'plan' does not exist.

What you can do now:
- Restore the checklist template '.shadekit/templates/checks/definition-of-ready.md'
- Or run: shade-kit upgrade
- Then call /shade-kit-plan again
```

## Refusal table

The table is keyed by `error.code`; the envelope has no exit code, the numbers are only there for the reader. Where a row says "from the message", the options are named in `error.message` (for example the known profiles, or the phases of the profile).

| Error code (exit code) | Options to name |
|---|---|
| `version-mismatch` (50), or `shade-kit` not found | What to update and how, from the message: update the engine or the kit skills so that their major and minor versions match. If `shade-kit` was not found: install the engine (the `shade-kit` dotnet global tool or the self-contained binary), make sure it is on `PATH`, then call the skill again. |
| `kit-root-not-found` (10) | `shade-kit init` |
| `kit-file-write-failed` (17) | Check the named file or folder in `.shadekit/` and its permissions, then call the skill again. |
| `format-too-new` (20), `upgrade-needed` (21), `invalid-file` (22) | Update the engine, run `shade-kit upgrade`, or fix the named file. |
| `git-identity-missing` (36) | Set `user.name` and `user.email` (`git config user.name "..."`, `git config user.email "..."`). |
| `no-current-feature` (37) | `shade-kit feature list`, `shade-kit feature set <id>`, `/shade-kit-new` |
| `unknown-profile` (40) | The known profiles from the message; fix `profile` in `feature.json` or update the engine. |
| `phase-not-in-profile` (41) | The phases of the profile from the message, and the steps of `phase next` (for example `/shade-kit-diagnose` instead of `/shade-kit-refine` in `bugfix`). |
| `transition-not-allowed` (42) | The steps of `phase next`. |
| `feature-done` (44) | `shade-kit feature set <id>` for another feature, `/shade-kit-new` (and the steps of `phase next`). |
| `phase-state-invalid` (46) | Fix the field of `feature.json` that the message names. |
| `checklist-template-not-found` (47) | Restore the named checklist template in `.shadekit/templates/checks/`, or run `shade-kit upgrade`. |
| Any other code | The message of the failure envelope; no further options. |

## Rules

- Start no skill. Name skills; the human calls them.
- Run no command that changes the state: never `phase start`, `phase approve`, `phase reject`, `phase complete`, `feature set`, `feature new`, `feature drop`, or `init`. The only command this skill runs is `shade-kit phase next --json`; a command in the options is named, not run.
- Never read `feature.json`, `gates.json`, or `config.json`; the state comes from `phase next`.
- Show every warning; never leave one out because it looks harmless.
