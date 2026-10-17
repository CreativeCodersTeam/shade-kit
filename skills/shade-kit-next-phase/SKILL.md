---
name: shade-kit-next-phase
description: SHADE kit entry point. Finds the current feature and its phase, offers the phases that may come next, lets the user choose, and starts the chosen SHADE kit phase skill. Use when the user says "continue", "next phase", "what's next", or asks where a SHADE kit feature stands.
---

# shade-kit-next-phase

<!-- Skeleton: purpose and flow only, details follow. Sources: research/shade-kit-skills.md, docs/feature-packages/06-skill-foundation.md. CLI commands other than `shade-kit version` and `shade-kit features list` are planned and do not exist yet. -->

## Purpose

Moves a feature forward without the user having to know its state. This skill only chooses and hands off. It never runs a gate check and never approves a transition: the chosen phase skill runs the entry protocol itself.

## Flow

1. **Version handshake:** `shade-kit version --json`. Stop and tell the user what to update if the engine and kit versions do not match.
2. **Current feature:** `shade-kit feature current --json`.
   - Not resolved: list the features (`shade-kit features list --json`), ask which one to use or offer to create a new one. Then `shade-kit feature set <id>`, or hand off to `shade-kit-new`.
3. **State:** read profile, current phase, and phase state.
   - Feature `done`: report that it is finished and offer to pick another feature.
   - Current phase `in-progress`: offer to continue it.
   - Current phase `awaiting-approval`: offer to repeat the pending transition.
4. **Next phases:** get the allowed next phases (planned: `shade-kit phase next --json`). Always offer `shade-kit-discuss` and `shade-kit-drop` as well.
5. **Choice:** let the user choose, using the agent's structured question tool where one exists.
6. **Hand-off:** start the phase skill of the chosen phase. In the `quick` profile, the `implement` phase is run by `shade-kit-quick`.

## Rules

- Do not derive the next phases from the profile in the skill once the CLI returns them; that would duplicate engine logic.
- Never call `shade-kit phase approve`.
