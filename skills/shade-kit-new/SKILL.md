---
name: shade-kit-new
description: SHADE kit phase skill for `new`. Creates a feature with a workflow profile (feature, bugfix, quick) from free text, an issue URL, a Markdown file, or discussion notes, and writes request.md. Use when the user wants to start a new SHADE kit feature, bug fix, or quick change.
---

# shade-kit-new

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/09-discuss-and-intake.md. CLI commands other than `shade-kit version`, `shade-kit init`, and `shade-kit feature …` (`new`, `list`, `current`, `set`, `show`, `drop`) are planned and do not exist yet. -->

## Purpose

Creates the feature folder (`feature.json`, `request.md`) so the work has lasting state. `new` is the first phase of every profile; `shade-kit feature new` sets it to `in-progress` (once package 04 exists; until then it writes no phase fields), so there is no gate before it.

| Input | Output |
|---|---|
| Free text, issue URL, Markdown file, or discussion notes | `feature.json` (through the CLI), `request.md` |

## Flow

1. **Version handshake:** `shade-kit version --json`. Stop and tell the user what to update on a mismatch.
2. **Input:** collect the requirement source.
3. **Profile:** if the user did not name one, propose one from the input (defect report → `bugfix`, small and clear change → `quick`, otherwise `feature`) and let the user confirm.
4. **Issue URL:** read the issue with the agent's own tools (for example `gh`); the engine does not call trackers. Use its title for the feature id.
5. **Create:** `shade-kit feature new --source <text|url|file> [--profile <p>] [--title <t>] --json`. A URL source needs `--title`.
6. **`request.md`:** the CLI has already filled the title, id, source kind and reference, the input it received, and the creation time and creator. Complete it: add what the CLI does not have (for an issue URL, the issue title and body unchanged) and remove the guidance comment. Do not rewrite or summarise the input.
7. **Branch:** offer to create and switch to `feature/<id>`. Run the git command only after a yes.
8. **Report:** the feature id and the next phase of the profile (`refine`, `diagnose`, or `quick`). Offer `shade-kit-next-phase` to continue.
