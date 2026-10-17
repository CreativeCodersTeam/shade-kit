---
name: shade-kit-interview
description: SHADE kit general skill for a structured clarification interview, one topic at a time with an own proposal per question. Loaded by the SHADE kit skills discuss, refine, diagnose, plan, and quick; not meant to be called directly.
---

# shade-kit-interview

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/06-skill-foundation.md. -->

## Purpose

Gets decisions from the human instead of filling gaps silently. Every phase that needs answers uses the same interview, so questions look and behave the same everywhere.

## Input from the calling skill

- Mode: `open` (discuss: explore, propose alternatives, challenge assumptions) or `targeted` (a list of findings or questions to resolve).
- The topics or findings to cover.
- The target artifact where answers are recorded (for example `spec.md`, `plan.md`, discussion notes).

## Flow

1. **Order** the topics: blocking questions first.
2. **Ask** one topic at a time. Give context, the agent's own proposal, and the alternatives. Use the agent's structured question tool where one exists, plain questions otherwise.
3. **Challenge** answers that contradict earlier answers or the artifacts, once, with the reason.
4. **Record** every answer in the target artifact as the calling skill specifies (for example a `Q-` question moving to "Decisions" with who and when).
5. **Return** to the calling skill: answered topics, topics still open.

## Rules

- Never record an assumption as a decision without the human's answer.
