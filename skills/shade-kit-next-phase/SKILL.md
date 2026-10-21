---
name: shade-kit-next-phase
description: SHADE kit entry point. Finds the current feature and its phase, offers the phases that may come next, lets the user choose, and starts the chosen SHADE kit phase skill. Use when the user says "continue", "next phase", "what's next", or asks where a SHADE kit feature stands.
---

# shade-kit-next-phase

<!-- Skeleton: purpose and flow only, details follow. Sources: research/shade-kit-skills.md, docs/feature-packages/06-skill-foundation.md. CLI commands other than `shade-kit version`, `shade-kit init`, and `shade-kit feature …` (`new`, `list`, `current`, `set`, `show`, `drop`) are planned and do not exist yet. -->

## Purpose

Moves a feature forward without the user having to know its state. This skill only chooses and hands off. It never runs a gate check and never approves a transition: the chosen phase skill runs the entry protocol itself.

## Flow

1. **Version handshake:** `shade-kit version --json`. Stop and tell the user what to update if the engine and kit versions do not match.
2. **Current feature:** `shade-kit feature current --json`.
   - Not resolved: list the features (`shade-kit feature list --json`), ask which one to use or offer to create a new one. Then `shade-kit feature set <id>`, or hand off to `shade-kit-new`.
3. **Next steps:** `shade-kit phase next --json`. It returns the current phase and state and the possible steps, each with its gate and skill:
   - Feature `done`: only the status. Report that it is finished and offer to pick another feature.
   - `continue` (current phase `in-progress`): continue it.
   - `pending` (current phase `awaiting-approval`): repeat the pending transition.
   - `forward`: move on to the next phase of the profile.
   - `reopen[]`: reopen an earlier phase (always needs approval).
4. **Offer** these steps, and always offer `shade-kit-discuss` and `shade-kit-drop` as well.
5. **Choice:** let the user choose, using the agent's structured question tool where one exists.
6. **Hand-off:** start the phase skill of the chosen phase (`shade-kit-<phase>`). Its entry protocol performs the transition: forward, re-entry, or reopen.

## Rules

- Do not derive the next steps from the profile; `phase next` returns them, so the skill does not duplicate engine logic.
- Never call `shade-kit phase approve`.
