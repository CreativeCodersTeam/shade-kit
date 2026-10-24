# 09 · Discuss and intake (`discuss`, `new`)

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-18 (consistency review: router references replaced by `shade-kit-next-phase`), 2026-10-21 (package 03 implemented: the engine pre-fills `request.md`), and 2026-10-24 (documentation check: line endings in FP09-AC4) |
| Source | Concept §5.1 (`discuss`, `new`), §5.4, §10 open question 2 |
| Depends on | 03, 04, 06 |
| Enables | 10 (`refine`), 16 (`diagnose`), 17 (`quick`) |

## Benefit

Ideas can be explored freely before they become a feature, without creating any workflow state. When an idea, an issue, or a document is ready, the agent turns it into a feature in one step, with the original input preserved, so later phases can always go back to what was actually asked for.

## Scope

### In scope

- The `discuss` skill and where its notes are stored.
- The `new` phase skill for every profile.
- The templates `discussion.md` and `request.md`.

### Out of scope

- Clarifying the requirement (`refine`, package 10). `new` records the input; it does not judge it.
- Creating a feature without an agent: `shade-kit feature new` from package 03 works on its own.

## Functional description

### `discuss`

- Brainstorm any topic: an idea, a design question, a problem. Optional, outside every profile, possible at any time. **Decided** (concept §5.1, §5.4).
- Uses the interview skill (package 06) in an open mode: the agent asks, proposes alternatives, and challenges assumptions.
- At the end, or when the human asks, the agent writes the notes from the `discussion.md` template: topic, date, participants (git user), options discussed, conclusions, open questions.
- Storage (concept open question 2, **Decided** (confirmation list, 2026-10-18)):
  - No current feature: `.shadekit/discussions/<yyyymmdd>-<hhmm>-<slug>.md`.
  - Current feature: `.shadekit/features/<id>/discussions/<yyyymmdd>-<hhmm>-<slug>.md`.
- Discussion notes are committed like all kit data. `discuss` never changes `feature.json`.
- The agent offers the next step: create a feature from the notes, or (with a current feature) continue `refine` with the notes as input.

### `new`

Runs when no feature exists for the work yet. `shade-kit-next-phase` (package 06) hands off to it, or the human calls it directly.

1. Collect the input: free text, an issue URL, a Markdown file, or discussion notes.
2. Ask for the profile if the human did not name it; propose one from the input (a defect report suggests `bugfix`, a small, clear change suggests `quick`). Default from `config.json`.
3. For an issue URL, the agent reads the issue with its own tools (for example `gh`), because the engine does not call trackers (package 03). It takes the title for the id.
4. Call `shade-kit feature new --source <…> [--profile <p>] [--title <…>] --json`. Without `--profile`, the engine uses `defaultProfile`; a URL source needs `--title`.
5. Complete `request.md`. `feature new` already fills the title, the id, the source kind and reference, the original input it received (the text, the file content, or the URL), and the creation time and git user (package 03). **Decided** on 2026-10-20 (package 03 implementation clarification). The agent adds what the engine does not have, for example the issue title and body for an issue URL, and removes the guidance comment. The agent does not rewrite or summarise the input.
6. Offer to create and switch to the branch `feature/<id>` (suggested by package 03) so the current feature follows the branch. The agent runs the git command only after a yes. **Proposed.**
7. Report the id and the next phase of the profile (`refine`, `diagnose`, or `quick`). The transition then goes through the entry protocol of that phase skill (package 06).

### Templates

| Template | Content |
|---|---|
| `discussion.md` | Topic, date, participants, context, options, conclusions, open questions. |
| `request.md` | Source kind, source reference, original input, created at, created by. |

## Acceptance criteria

- **FP09-AC1** `discuss` without a current feature writes its notes to `.shadekit/discussions/` and creates no feature.
- **FP09-AC2** `discuss` with a current feature writes its notes into the feature's `discussions/` folder and leaves `feature.json` unchanged.
- **FP09-AC3** `new` with an issue URL creates an issue-based id and puts the issue's title and body unchanged into `request.md`.
- **FP09-AC4** `new` with a Markdown file or discussion notes copies their content into `request.md`, unchanged except for the line endings, which follow package 03.
- **FP09-AC5** `new` creates a branch only after the human agrees.
- **FP09-AC6** After `new`, `shade-kit-next-phase` offers the next phase of the feature's profile.

## Open points

- The guidance comment of the shipped `request.md` skeleton names the source kinds `feature new` writes (`text`, `url`, `file`) and says that the agent may refine the reference (for example the issue behind a URL, or the path of the discussion notes) when it completes the file. Aligned on 2026-10-21 in the code review rework of package 03 (finding F18). The rest of the skeleton is still replaced when this package is built. **Open.**
