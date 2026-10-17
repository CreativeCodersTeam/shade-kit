---
name: shade-kit-refine
description: SHADE kit phase skill for `refine` (feature profile). Clarifies the requirement of the current feature through research and a structured interview and writes spec.md with requirement IDs, acceptance criteria, and out of scope. Can run several times. Use when the user wants to refine, clarify, or specify the current SHADE kit feature.
---

# shade-kit-refine

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/10-refine.md. CLI commands other than `shade-kit version` and `shade-kit features list` are planned and do not exist yet. -->

## Purpose

Turns the raw request into a testable specification: vague statements, gaps, contradictions, and unstated assumptions are found and resolved with the human.

| Input | Output |
|---|---|
| `request.md`, discussion notes of the feature, existing `spec.md` (later rounds) | `spec.md`, `researches.md` (sections `refine / gaps`, `refine / security`) |

## Flow

1. **Entry protocol:**
   1. Version handshake: `shade-kit version --json`. Stop on a mismatch and name what to update.
   2. `shade-kit phase start refine --json`. Stop if the transition is not allowed and report why.
   3. If the gate has a checklist: load `shade-kit-gate-check` on it.
   4. If the gate needs approval: show the result, ask the human, and only after a yes call `shade-kit phase approve refine`.
   5. Load the skills the CLI returned.
2. **Read** `request.md`, the feature's discussion notes, and an existing `spec.md`.
3. **Research:** load `shade-kit-research` with the focuses "gaps and open questions" and "security at requirement level".
4. **Interview:** load `shade-kit-interview` on the findings, one topic at a time, each with an own proposal. Put every assumption to the human.
5. **Write `spec.md`** from the template. Answered questions move from "Open questions" to "Decisions"; their inline markers are removed. IDs are never renumbered.
6. **Report** what changed and which questions are still open. The human decides between another round and moving on (`shade-kit-next-phase`).
