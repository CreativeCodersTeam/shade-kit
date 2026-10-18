---
name: shade-kit-discuss
description: SHADE kit free brainstorming on any topic (an idea, a design question, a problem), with or without a current feature. Writes discussion notes that can feed a new feature or a refine round. Use when the user wants to discuss, brainstorm, or think something through in a SHADE kit repository.
---

# shade-kit-discuss

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/09-discuss-and-intake.md. CLI commands other than `shade-kit version`, `shade-kit init`, and `shade-kit feature list` are planned and do not exist yet. -->

## Purpose

Open brainstorming outside every profile, possible at any time and in any phase. The result is a set of discussion notes. This skill never changes the feature state.

## Flow

1. **Version handshake:** `shade-kit version --json`. Stop and tell the user what to update on a mismatch.
2. **Context:** `shade-kit feature current --json` to find out whether a feature is current. No feature is needed.
3. **Discuss:** load `shade-kit-interview` in open mode. Ask, propose alternatives, and challenge assumptions.
4. **Notes:** at the end, or when the user asks, write the notes from the `discussion.md` template in `.shadekit/templates/`: topic, date, participants, context, options, conclusions, open questions.
   - No current feature: `.shadekit/discussions/<yyyymmdd>-<hhmm>-<slug>.md`
   - Current feature: `.shadekit/features/<id>/discussions/<yyyymmdd>-<hhmm>-<slug>.md`
5. **Next step:** offer to create a feature from the notes (`shade-kit-new`) or, with a current feature, to continue `shade-kit-refine` with the notes as input.

## Rules

- No entry protocol: `discuss` is not a phase of any profile.
- Never write `feature.json` or any other kit JSON file.
