# 06-7 · Interview (`shade-kit-interview`)

| | |
|---|---|
| Status | Draft, 2026-11-04 (split from package 06) |
| Source | [06 Skill foundation](06-skill-foundation.md) (general skills) |
| Kind | Skill (general skill, skeleton exists) |
| Depends on | 06-2 |
| Enables | 09 (`discuss`), 10 (`refine`), 12 (`plan`), 16 (`diagnose`), 17 (`quick`) |

## Benefit

The agent gets decisions from the human instead of filling gaps silently. Every phase that needs answers uses the same interview, so questions look and behave the same everywhere.

## Scope

### In scope

- The general skill `shade-kit-interview`, loaded by `discuss`, `refine`, `diagnose`, `plan`, and `quick`.

### Out of scope

- What each phase asks and where it records the answers in detail (packages 09, 10, 12, 16, 17).

## Functional description

### Input from the calling skill

- Mode: `open` (discuss: explore, propose alternatives, challenge assumptions) or `targeted` (a list of findings or questions to resolve).
- The topics or findings to cover.
- The target artifact where answers are recorded (for example `spec.md`, `plan.md`, discussion notes), and how the calling skill wants them recorded.

### Flow

1. Order the topics: blocking questions first.
2. Ask one topic at a time. Give the context, the agent's own proposal, and the alternatives. Use the agent's structured question tool where one exists, plain questions otherwise.
3. Challenge an answer that contradicts earlier answers or the artifacts, once, with the reason.
4. Record every answer in the target artifact as the calling skill specifies (for example a `Q-` question moving to "Decisions" with who and when).
5. Return to the calling skill: the answered topics and the topics still open.

### Rules

- Never record an assumption as a decision without the human's answer.
- The skill does not end with the next-steps message; the calling skill does (06-3).

## Acceptance criteria

- **FP06-7-AC1** Every question covers one topic and gives the agent's own proposal and the alternatives; where the agent has a structured question tool, the skill uses it.
- **FP06-7-AC2** Blocking topics are asked before the others.
- **FP06-7-AC3** The target artifact holds a decision only for topics the human answered in the conversation; the skill returns the topics still open to the calling skill.

## Open points

None.
