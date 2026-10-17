---
name: shade-kit-diagnose
description: SHADE kit phase skill for `diagnose` (bugfix profile). Reproduces the reported bug, finds and proves its root cause, and writes diagnosis.md plus a spec.md with the expected behaviour. Use when the user wants to diagnose, reproduce, or find the root cause of the bug of the current SHADE kit feature.
---

# shade-kit-diagnose

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/16-bugfix-profile.md. CLI commands other than `shade-kit version` and `shade-kit features list` are planned and do not exist yet. -->

## Purpose

Replaces `refine` and `map` in the `bugfix` profile: the bug is made reproducible and its cause is traced in the code with evidence, so the fix addresses the cause and not the symptom.

| Input | Output |
|---|---|
| `request.md`, code base | `diagnosis.md`, `spec.md` |

## Flow

1. **Entry protocol:**
   1. Version handshake: `shade-kit version --json`. Stop on a mismatch and name what to update.
   2. `shade-kit phase start diagnose --json`. Stop if the transition is not allowed and report why.
   3. If the gate has a checklist: load `shade-kit-gate-check` on it.
   4. If the gate needs approval: show the result, ask the human, and only after a yes call `shade-kit phase approve diagnose`.
   5. Load the skills the CLI returned.
2. **Read** `request.md`. Load `shade-kit-interview` for missing facts.
3. **Reproduce:** find a reliable trigger; record steps, environment, observed and expected behaviour. If it cannot be reproduced, stop and ask the human for more information instead of guessing.
4. **Root cause:** trace from the symptom to the cause with file and line evidence. Name the causes that were checked and ruled out.
5. **Write `diagnosis.md`:** reproduction, observed versus expected behaviour, root cause with evidence, ruled-out causes, affected code, fix options with a recommendation.
6. **Write `spec.md`** in the normal format: the expected behaviour as `REQ-` requirements, each with at least one acceptance criterion stating that the reproduction no longer shows the bug.
7. **Report** the root cause and the recommended fix. Continue with `shade-kit-next-phase`.
