# Report: {{feature-title}}

<!--
Skeleton template. Package 15 (finish and report) owns it and writes its final structure and guidance; until then only the section headings are fixed.
How to fill this template:
- Replace every {{placeholder}} and remove every guidance comment like this one.
- Take the data from shade-kit feature show <id> --json and from the artifacts the profile produced. Do not invent results.
- Every section stays in the report for every profile. A section whose artifact the profile does not produce says so in one line.
- Requirements and tests: every requirement of spec.md that is not removed, with its acceptance criteria, proving tests, and result from verification.md.
- Review: the number of findings per severity, and every accepted finding with its reason.
- Approvals: every transition from the feature history, with from, to, who, and when, and "approval off" where it applied.
-->

Feature: `{{feature-id}}` · Spec: [spec.md](spec.md) · Tasks: [tasks.md](tasks.md) · Review: [review.md](review.md) · Verification: [verification.md](verification.md) · Playbook: [playbook.md](playbook.md) · Definition of Done: [definition-of-done.md](checks/definition-of-done.md)

## Summary

{{goal from spec.md, profile, start date, and finish date}}

## Requirements and tests

{{requirement → acceptance criteria → proving tests → result}}

## Checks

{{build, test, lint, and coverage results from verification.md}}

## Review

{{findings per severity and accepted findings}}

## Manual checks

{{playbook items and whether each is checked off}}

## Deviations

{{deviations recorded in plan.md}}

## Approvals

{{every transition from the history}}
