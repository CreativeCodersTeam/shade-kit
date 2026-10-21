---
name: shade-kit-drop
description: SHADE kit skill to drop a feature from any phase. Deletes the feature after explicit confirmation and clears the local override only if it points to the dropped feature. Use when the user wants to drop, cancel, abandon, or delete a SHADE kit feature.
---

# shade-kit-drop

<!-- Skeleton: purpose and flow only, details follow. Sources: research/shade-kit-skills.md, docs/feature-packages/03-feature-management.md. CLI commands other than `shade-kit version`, `shade-kit init`, and `shade-kit feature …` (`new`, `list`, `current`, `set`, `show`, `drop`) are planned and do not exist yet. -->

## Purpose

Removes a feature that will not be built, from any phase. `drop` is not a phase of any profile, so there is no entry protocol, but nothing is deleted without an explicit yes.

## Flow

1. **Version handshake:** `shade-kit version --json`. Stop on a mismatch and name what to update.
2. **Feature:** `shade-kit feature current --json`, or the feature the user named. If none is resolved, list the features (`shade-kit feature list --json`) and ask.
3. **Preview:** `shade-kit feature drop <id> --json` without `--yes`. The CLI deletes nothing and returns what it would delete (`dropped: false`, `files`, and `overrideAffected`: whether the local override points to this feature and would be cleared).
4. **Confirm:** show the feature id, profile, phase, and the preview, including whether the local override would be cleared. Ask for an explicit confirmation.
5. **Drop:** only after a yes, `shade-kit feature drop <id> --yes --json`. The CLI deletes the feature folder and clears the local override if it points to this feature (`overrideCleared: true` once it was deleted).
6. **Report** what was removed.

## Rules

- Never drop a feature without a yes in the conversation.
- Do not touch git branches or commits.
