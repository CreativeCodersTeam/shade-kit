---
name: shade-kit-report
description: SHADE kit general skill that writes report.md for a finished feature: requirements, acceptance criteria and proving tests, check results, review findings, manual checks, deviations, and approvals. Optional; loaded by the SHADE kit skill finish only when a report is written; not meant to be called directly.
---

# shade-kit-report

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/15-finish-and-report.md. CLI commands other than `shade-kit version` and `shade-kit feature list` are planned and do not exist yet. -->

## Purpose

Collects the evidence of a feature in one place, so reviewers of the pull request do not have to gather it from several artifacts. The report is optional: `finish` loads this skill only when the `report` setting (`ask`, `always`, `never`) leads to a report.

## Input from the calling skill

- The feature id.

## Flow

1. **Read state:** `shade-kit feature show <id> --json` (profile, phases, approval history).
2. **Read artifacts** the profile produced: `spec.md`, `tasks.md`, `review.md`, `verification.md`, `playbook.md`, `checks/definition-of-done.md`, and in `bugfix` `diagnosis.md`.
3. **Write `report.md`** from the template: summary, requirements and tests, checks, review, manual checks, deviations, approvals. A section whose artifact the profile does not have says so in one line.
4. **Return** the report summary to the calling skill.

## Rules

- Take every value from the artifacts and the CLI; never fill a section with assumptions.
- Never change the feature state; `finish` calls `shade-kit phase complete`.
