---
name: shade-kit-review
description: SHADE kit general skill for an independent code review of the current change against spec.md, plan.md, and the repository's conventions, with findings classified by severity (blocker, major, minor, note) in review.md. Loaded by the SHADE kit skills implement and quick; not meant to be called directly.
---

# shade-kit-review

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/13-implement.md. Unverified: the fallback for agents without subagents (package 08). -->

## Purpose

Decides whether the implementation loop may end: only an open blocker keeps it going. The review runs in a separate context that did not write the code.

## Input from the calling skill

- The change to review (working tree or branch diff).
- `spec.md`, `plan.md` (if the profile has one), and the repository's agent instructions.
- The current loop round.

## Flow

1. **Start** the review in an independent context: a subagent where the agent supports it.
2. **Review** the change against the spec, the plan, and the conventions: correctness, missing or weak tests, design, security, lowered quality bar.
3. **Classify** each finding: ID `F-<nnn>`, severity `blocker`, `major`, `minor`, or `note`, file and line, description, status `open`. Re-check findings of earlier rounds and set them to `fixed` where they are.
4. **Write `review.md`** from the template: a "Rounds" entry (date, what changed, check results), the findings, the commands used.
5. **Return** to the calling skill: open blockers and the other findings.

## Rules

- Only the human sets a finding to `accepted`, with a reason.
- Never change code; the calling skill fixes the findings.
