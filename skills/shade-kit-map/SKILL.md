---
name: shade-kit-map
description: SHADE kit phase skill for `map` (feature profile). Maps the current feature onto the code base with parallel research agents (best practice, existing architecture, affected code, don't hand-roll, security) and writes researches.md. Use when the user wants to map, analyse, or research the code base for the current SHADE kit feature.
---

# shade-kit-map

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/11-map.md. CLI commands other than `shade-kit version`, `shade-kit init`, and `shade-kit feature list` are planned and do not exist yet. -->

## Purpose

Finds out where and how the specified feature fits into the existing code, so the plan builds on facts instead of guesses.

| Input | Output |
|---|---|
| `spec.md`, `refine` sections of `researches.md`, code base | `researches.md` (sections `map / <focus>`) |

## Flow

1. **Entry protocol:**
   1. Version handshake: `shade-kit version --json`. Stop on a mismatch and name what to update.
   2. `shade-kit phase start map --json`. Stop if the transition is not allowed and report why. If the phase is already in progress, the CLI returns no gate: go to step 5. If a transition away from this phase is pending, the CLI reports it and changes nothing: ask the human whether to discard it. On a yes, call `shade-kit phase reject <pending> --note "resumed map"` and go to step 5; on a no, hand off to `shade-kit-next-phase` and stop.
   3. If the gate has a checklist: load `shade-kit-gate-check` on it.
   4. If the gate needs approval: show the result, ask the human, and only after a yes call `shade-kit phase approve map`. On a no, call `shade-kit phase reject map --note "<gaps>"`, hand off to `shade-kit-next-phase`, and stop. If approval is switched off, a failed checklist is only a warning.
   5. Load the skills the CLI returned.
2. **Read** `spec.md` and the `refine` sections of `researches.md`.
3. **Research:** load `shade-kit-research` with five focuses: best practice, existing architecture, existing affected code, don't hand-roll, security at code level. Every finding names its source (file and line, or link); a finding without a source is marked as an assumption.
4. **Summarise** for the human: affected areas, risks, and recommendations for the plan.

## Skipping the work

The human can skip the mapping on purpose (for example a change confined to one file). Then write a `map / skipped` section with the reason and the git user into `researches.md`. The phase itself is still passed, so the decision stays visible in the history.
