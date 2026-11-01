---
name: shade-kit-next-phase
description: SHADE kit status and next steps. Finds the current feature and its phase and shows the possible next steps, each with the SHADE kit phase skill the user calls for it. Starts no skill. Use when the user says "continue", "next phase", "what's next", or asks where a SHADE kit feature stands.
---

# shade-kit-next-phase

<!-- Skeleton: purpose and flow only, details follow. Sources: research/shade-kit-skills.md, docs/feature-packages/06-skill-foundation.md. CLI commands other than `shade-kit version`, `shade-kit init`, and `shade-kit feature …` (`new`, `list`, `current`, `set`, `show`, `drop`) are planned and do not exist yet. -->

## Purpose

Shows where a feature stands and what the user can do next, without the user having to know its state. This skill is advisory: a phase starts only when the user calls its phase skill (`/shade-kit-<phase>`). It never starts a phase skill, never runs a gate check, and never calls `shade-kit phase start`, `phase approve`, or `phase reject`.

## Flow

1. **Version handshake:** `shade-kit version --json`. Stop and tell the user what to update if the engine and kit versions do not match.
2. **Current feature:** `shade-kit feature current --json`.
   - Not resolved: list the features (`shade-kit feature list --json`) and ask which one to use. After the user picks one, call `shade-kit feature set <id>` (this starts no phase) and go on with step 3. If the user wants a new feature, name `/shade-kit-new` and end.
3. **Next steps:** `shade-kit phase next --json`. It returns the current phase and state and the possible steps, each with its skill; `pending` and `forward` also with their gate:
   - Feature `done`: only the status. Report that it is finished; name `shade-kit feature set <id>` for another feature and `/shade-kit-new`.
   - `continue` (current phase `in-progress`): continue it with `/shade-kit-<current>`.
   - `pending` (current phase `awaiting-approval`): decide on the pending transition with `/shade-kit-<pending>`, or discard it and continue the current phase with `/shade-kit-<current>`.
   - `forward`: move on to the next phase with `/shade-kit-<next>`.
   - `reopen[]`: reopen an earlier phase with `/shade-kit-<earlier>` (always needs approval).
4. **Show** these steps, and always `/shade-kit-discuss` and `/shade-kit-drop` as well, then end. The user chooses by calling the skill.

## Rules

- Do not derive the next steps from the profile; `phase next` returns them, so the skill does not duplicate engine logic.
- Never start a phase skill, and never call `shade-kit phase start`, `phase approve`, or `phase reject`.
