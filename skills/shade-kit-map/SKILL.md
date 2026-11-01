---
name: shade-kit-map
description: SHADE kit phase skill for `map` (feature profile). Maps the current feature onto the code base with parallel research agents (best practice, existing architecture, affected code, don't hand-roll, security) and writes researches.md. Use when the user wants to map, analyse, or research the code base for the current SHADE kit feature. Runs only when the user calls it by name (`/shade-kit-map`); never load it on your own or from another skill.
---

# shade-kit-map

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/11-map.md. CLI commands other than `shade-kit version`, `shade-kit init`, and `shade-kit feature …` (`new`, `list`, `current`, `set`, `show`, `drop`) are planned and do not exist yet. -->

## Purpose

Finds out where and how the specified feature fits into the existing code, so the plan builds on facts instead of guesses.

| Input | Output |
|---|---|
| `spec.md`, `refine` sections of `researches.md`, code base | `researches.md` (sections `map / <focus>`) |

## Flow

1. **Entry protocol:**
   1. Version handshake: `shade-kit version --json`. Stop on a mismatch and name what to update.
   2. `shade-kit phase start map --json`. This is the check: it changes nothing when it refuses or when the phase is already active.
      - Refused: end with the reason and the next steps (see Ending).
      - Already in progress (no gate): tell the user that `map` is already active and ask: continue it (go to step 5) or end with the next steps.
      - A transition away from this phase is pending: tell the user and ask: discard it and continue (`shade-kit phase reject <pending> --note "resumed map"`, then go to step 5), keep it (end and name `/shade-kit-<pending>`, which decides on it), or end with the next steps.
      - The transition into `map` was requested earlier and is still pending: say so and go on with the returned gate.
   3. If the gate has a checklist: load `shade-kit-gate-check` on it.
   4. If the gate needs approval: show the result, ask the human, and only after a yes call `shade-kit phase approve map`. On a no, call `shade-kit phase reject map --note "<gaps>"` and end with the gaps and the next steps (see Ending). If approval is switched off, a failed checklist is only a warning.
   5. Load the skills the CLI returned.
2. **Read** `spec.md` and the `refine` sections of `researches.md`.
3. **Research:** load `shade-kit-research` with five focuses: best practice, existing architecture, existing affected code, don't hand-roll, security at code level. Every finding names its source (file and line, or link); a finding without a source is marked as an assumption.
4. **Summarise** for the human: affected areas, risks, and recommendations for the plan. End with the next steps (see Ending).

## Skipping the work

The human can skip the mapping on purpose (for example a change confined to one file). Then write a `map / skipped` section with the reason and the git user into `researches.md`. The phase itself is still passed, so the decision stays visible in the history.

## Ending

- End with the next steps: what the user can do now and the skill they call for each, from `shade-kit phase next --json` (continue the current phase, the pending transition, the next phase, reopen an earlier phase, each as `/shade-kit-<phase>`), plus `/shade-kit-discuss` and `/shade-kit-drop`. After a refused start, name the reason from the CLI first; without a current feature, name `shade-kit feature list`, `shade-kit feature set <id>`, and `/shade-kit-new`.
- Never start another phase skill and never call `shade-kit phase start` for another phase: a phase starts only when the user calls its skill.
