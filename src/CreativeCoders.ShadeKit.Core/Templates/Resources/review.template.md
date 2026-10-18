# Review: {{feature-title}}

<!--
Skeleton template. Package 13 (implement) owns it and writes its final structure and guidance; until then only the section headings are fixed.
How to fill this template:
- Replace every {{placeholder}} and remove every guidance comment like this one.
- Rounds: one entry per implement → simplify → review round, with the date, what changed, and the check results. In the bugfix profile, the failing run of the regression test before the first production code change is recorded here.
- Findings: ID F-nnn, stable and never renumbered, with severity, file and line, description, and status.
  - Severity: blocker | major | minor | note.
  - Status: open | fixed | accepted. A finding is accepted only with a reason and after the human agreed. Only an open blocker keeps the loop going.
- Commands: the build, test, and lint commands that were used, so verify runs the same ones.
-->

Feature: `{{feature-id}}` · Spec: [spec.md](spec.md) · Plan: [plan.md](plan.md) · Tasks: [tasks.md](tasks.md)

## Rounds

{{one entry per round}}

## Findings

{{one entry per finding}}

## Commands

{{build, test, and lint commands used}}
