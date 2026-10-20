# Verification: {{feature-title}}

<!--
Skeleton template. Package 14 (verify) owns it and writes its final structure and guidance; until then only the section headings are fixed.
How to fill this template:
- Replace every {{placeholder}} and remove every guidance comment like this one.
- Commands: every command that was run, with its result: the build, test, and lint commands recorded in review.md, plus the coverage command from the test strategy in plan.md.
- Coverage: the measured value. If config.json sets coverageThreshold, also the threshold and whether the value reaches it.
- Acceptance criteria: every acceptance criterion of spec.md with its proving tests from the coverage matrix in tasks.md and the result in this run, or "manual" for criteria that go into playbook.md.
-->

Feature: `{{feature-id}}` · Spec: [spec.md](spec.md) · Tasks: [tasks.md](tasks.md) · Review: [review.md](review.md) · Playbook: [playbook.md](playbook.md)

## Run

{{date of the run}}

## Commands

{{commands with their results}}

## Coverage

{{coverage value}}

## Acceptance criteria

{{acceptance criterion → proving tests → result}}
