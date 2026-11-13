---
name: shade-kit-drop
description: SHADE kit skill to drop a feature from any phase. Deletes the feature after explicit confirmation and clears the local override only if it points to the dropped feature. Use when the user wants to drop, cancel, abandon, or delete a SHADE kit feature. Runs only when the user calls it by name (`/shade-kit-drop`) or asks for it; never load it on your own or from another skill.
metadata:
  version: 0.7.0
---

# shade-kit-drop

<!-- Skeleton: purpose and flow only, details follow. Sources: research/shade-kit-skills.md, docs/feature-packages/03-feature-management.md. -->

## Purpose

Removes a feature that will not be built, from any phase. `drop` is not a phase of any profile, so there is no entry protocol, but nothing is deleted without an explicit yes.

Keep every `warnings` entry (`{ code, message }`) of every CLI call of this run. Do not show them on the way; the closing message shows each one once.

## Flow

1. Version handshake: run `shade-kit version --json --expect 0.7.0`; 0.7.0 is the kit version of this skill (`metadata.version` in its frontmatter).
   - `ok` is `true`: continue. A warning `engine-prerelease-version-mismatch` means that a prerelease engine is used although its major or minor version differs; keep it like every warning and continue.
   - `ok` is `false` (for example `version-mismatch`), or the shell reports that `shade-kit` is not found: stop before any other CLI call. End ([Ending](#ending)) with **refusal** and the failure envelope, or with "`shade-kit` not found".
2. **Feature:** the feature the user named (`shade-kit feature show <id> --json` for its profile and phase), or else `shade-kit feature current --json`. If no feature is resolved, run `shade-kit feature list --json` and ask the human which one to drop, with the agent's structured question tool (in Claude Code `AskUserQuestion`; without one, a plain question). No feature listed, no pick, or no answer: end with **end of work** and the line "No feature dropped." A failure envelope of any of these calls: end with **refusal** and the envelope.
3. **Preview:** `shade-kit feature drop <id> --json` without `--yes`. The CLI deletes nothing and returns what it would delete (`dropped: false`, `files`, and `overrideAffected`: whether the local override points to this feature and would be cleared). A failure envelope (for example `feature-not-found`): end with **refusal** and the envelope.
4. **Confirm:** show the feature id, profile, and phase (from step 2) and the preview, including whether the local override would be cleared. Ask for an explicit confirmation with the structured question tool (drop it, or keep it). On a no, or without an answer, end with **end of work** and the line "Feature `<id>` was not dropped."
5. **Drop:** only after a yes, `shade-kit feature drop <id> --yes --json`. The CLI deletes the feature folder and clears the local override if it points to this feature (`overrideCleared: true` once it was deleted). A failure envelope: end with **refusal** and the envelope.
6. **Report:** end with **end of work** and the line "Feature `<id>` dropped", with what was removed and whether the override was cleared.

## Ending

Every way of ending loads `shade-kit-next-steps` and passes it:

- why the run ends: **refusal**, with the failure envelope of the call that failed, or that `shade-kit` was not found; or **end of work**, with one line of what happened;
- every warning of the CLI calls of this run.

When the line of what happened makes a recommendation, it names the recommendation and its reason, but a skill only when the closing message does not list it: that message lists the steps of `shade-kit phase next` for the current feature, `/shade-kit-discuss`, and `/shade-kit-drop`, and without a current feature `shade-kit feature list`, `shade-kit feature set <id>`, and `/shade-kit-new`. The closing message is the end of this skill; write nothing after it.

## Rules

- Never drop a feature without a yes in the conversation.
- Do not touch git branches or commits.
- Never write `feature.json` or any other kit JSON file; `shade-kit feature drop` deletes the feature.
