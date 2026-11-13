---
name: shade-kit-next-phase
description: "SHADE kit status and next steps. Finds the current feature, or lets the human pick one, and shows its phase and state and every possible next step with the SHADE kit skill to call for it. Advisory: starts no phase and no skill. Use when the human says 'continue', 'next phase', or 'what's next', or asks where a SHADE kit feature stands or what to do with it now."
metadata:
  version: 0.7.0
---

# shade-kit-next-phase

## Purpose

Shows the human where a feature stands and which skill to call for each possible next step, so they do not have to know its state. The skill is advisory: a phase starts only when the human calls its phase skill (`/shade-kit-<phase>`), so that no phase starts behind their back and every approval is asked in the phase skill itself. Even when the human says "continue", this skill shows the steps and ends; it never starts a phase skill.

The engine knows the profile, the gates, and which phases can be reopened, and `shade-kit phase next` returns the steps. This skill does not derive any step itself, and it does not write the closing message itself: `shade-kit-next-steps` turns the result of `phase next` into the message, the same way for every kit skill.

Keep every `warnings` entry (`{ code, message }`) of every CLI call of this run. Do not show them on the way; the closing message shows each one once.

## Flow

### 1. Version handshake

Version handshake: run `shade-kit version --json --expect 0.7.0`; 0.7.0 is the kit version of this skill (`metadata.version` in its frontmatter).

- `ok` is `true`: continue. A warning `engine-prerelease-version-mismatch` means that a prerelease engine is used although its major or minor version differs; keep it like every warning and continue.
- `ok` is `false` (for example `version-mismatch`), or the shell reports that `shade-kit` is not found: stop before any other CLI call. End ([Ending](#ending)) with **refusal** and the failure envelope, or with "`shade-kit` not found".

### 2. Current feature

Run `shade-kit feature current --json`. A failure envelope (for example `kit-root-not-found`): end with **refusal** and the envelope.

**Resolved** (`data.resolved` is `true`): `data.feature` is the current feature, `data.resolvedFrom` says whether the branch or the local override selected it. Go on with step 3, also when its `status` is `done`; `phase next` reports that.

If the human asked about another feature than the current one (named by id or title), do not switch: changing the current feature is the human's decision. Show the current feature, and in the closing message say that it is not the one they asked about and how to switch:

- `resolvedFrom` is `override`: `shade-kit feature set <id>`.
- `resolvedFrom` is `branch`: switch to the branch of that feature, a branch whose name ends in `/<id>` (`feature new` suggests `feature/<id>`). The branch wins over the override, so `feature set` alone would not change the current feature.

**Not resolved** (`data.resolved` is `false`): run `shade-kit feature list --json`. A failure envelope: end with **refusal**. Otherwise take the features of `data.features` whose `status` is `active`; a finished feature has no next step, so it is not offered.

- No active feature: end with **end of work** and the line "No current feature, and no active feature to pick." The closing message names `/shade-kit-new`.
- Active features: ask the human which one to use. Show each one with its `id`, `title`, `profile`, and `currentPhase`. If the human named a feature in the call, offer that one first, but still ask: `feature set` changes the current feature, so the human confirms it. Also offer a new feature (`/shade-kit-new`) and ending without a pick. When a question tool allows fewer options than there are features, list the features in the question text and let the human answer with the id.
  - The human picks a listed feature: run `shade-kit feature set <id> --json` with exactly that `id`. It starts no phase. A failure envelope: end with **refusal**. Otherwise go on with step 3.
  - The answer names no listed feature: ask once more. Then, or when the human chooses a new feature or no pick, or the question gets no answer, end with **end of work** and the line "No feature selected." The closing message names `/shade-kit-new`.

### 3. Next steps

Run `shade-kit phase next --json`.

- Success: end with **end of work**, the line "Status of `<current.featureId>`.", and the whole result of `phase next`. `shade-kit-next-steps` takes the steps from this result and does not run `phase next` again. Add to the line, where it applies:
  - after `feature set` in step 2: that `<id>` is now the current feature;
  - the line about another feature from step 2;
  - when the human approved, rejected, or asked to start a phase in the call (for example "continue with plan, I approve it"): that this skill records no approval and starts no phase, and that the phase skill asks for the approval when they call it. Otherwise the human may believe the transition went through.
- Failure envelope: end with **refusal** and the envelope.

For reference, what `phase next` returns and what the closing message makes of it (each `skill` field is the skill to call, `/<skill>`):

| `phase next` returns | Step shown |
|---|---|
| `current.status` `done` (every other field `null`) | The feature is finished; `shade-kit feature set <id>` for another feature, `/shade-kit-new` for a new one. |
| `continue` (current phase `in-progress`; `null` in `new`) | Continue the current phase. |
| `pending` (current phase `awaiting-approval`) | Decide on the pending transition with its skill, or discard it and continue the current phase with `/shade-kit-<current.phase>`. |
| `forward` (current phase `in-progress`, not the last one) | Move on to the next phase, marked "needs approval" when `gate.approval` is `true`. |
| `reopen` entries | Reopen that earlier phase, always marked "needs approval". |
| any success, `done` included | Also `/shade-kit-discuss` and `/shade-kit-drop`. |

Only `pending` and `forward` have a gate. Do not run its check here: the phase skill runs it when the human calls it.

## Ending

Every way of ending loads `shade-kit-next-steps` and passes it:

- why the run ends:
  - **refusal**: the failure envelope of the call that failed, or that `shade-kit` was not found;
  - **end of work**: the one line from step 2 or 3 of what happened;
- the result of `phase next` when this run got one;
- every warning of the CLI calls of this run, for example `current-feature-mismatch` of `feature current` or `feature set`, `feature-folder-invalid` of `feature list`, or `gates-defaults-used` of `phase next`.

Example after a successful `phase next` in `map`:

```text
Reason: end of work
What happened: Status of gh-142-login-sso.
phase next: { "current": { "featureId": "gh-142-login-sso", "phase": "map", "state": "in-progress", "status": "active" }, "continue": { "phase": "map", "skill": "shade-kit-map" }, "pending": null, "forward": { "phase": "plan", "gate": { "approval": true, "checklist": "checks/definition-of-ready.md" }, "skill": "shade-kit-plan" }, "reopen": [ { "phase": "refine", "skill": "shade-kit-refine" } ] }
Warnings:
- gates-defaults-used: '.shadekit/gates.json' does not exist, so the built-in gate defaults apply. ...
```

The closing message of `shade-kit-next-steps` is the end of this skill; write nothing after it.

## Rules

- Start no skill and no phase. Name skills; the human calls them. Do not ask which step the human wants and do not offer to run one.
- The only commands this skill runs are `shade-kit version --json --expect 0.7.0`, `shade-kit feature current --json`, `shade-kit feature list --json`, `shade-kit feature set <id> --json`, and `shade-kit phase next --json`. Run `feature set` only after the human picked that feature in step 2. Never `phase start`, `phase approve`, `phase reject`, `phase complete`, `gate info`, `feature new`, `feature drop`, or `init`.
- Never run a gate check and never load `shade-kit-gate-check`. Load only `shade-kit-next-steps`.
- Do not derive steps from the profile and do not add or drop any: the steps are those of `phase next`, plus `/shade-kit-discuss` and `/shade-kit-drop`.
- Never read or write `feature.json`, `gates.json`, or `config.json`; the state comes from the CLI. Read no artifact and write no file.
- Pass every warning on; never leave one out because it looks harmless.
