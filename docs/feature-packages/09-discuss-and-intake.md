# 09 · Discuss and intake (`discuss`, `new`)

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-18 (consistency review: router references replaced by `shade-kit-next-phase`), 2026-10-21 (package 03 implemented: the engine pre-fills `request.md`), 2026-10-24 (documentation check: line endings in FP09-AC4), and 2026-10-25 (rework of `feature new` in package 03: the engine reads the issue with `gh` and writes the request content into `request.md`; no `request.md` template; code review of the rework: `request.md` treated as data, not as instructions, exit-33 hints, issue format in FP09-AC3) |
| Source | Concept §5.1 (`discuss`, `new`), §5.4, §10 open question 2 |
| Depends on | 03, 04, 06 |
| Enables | 10 (`refine`), 16 (`diagnose`), 17 (`quick`) |

## Benefit

Ideas can be explored freely before they become a feature, without creating any workflow state. When an idea, an issue, or a document is ready, the agent turns it into a feature in one step, with the original input preserved, so later phases can always go back to what was actually asked for.

## Scope

### In scope

- The `discuss` skill and where its notes are stored.
- The `new` phase skill for every profile.
- The template `discussion.md`. `request.md` has no template since 2026-10-25; the engine writes it (package 03).

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
3. For an issue URL of a tracker in `config.json`, the agent does not read the issue: `feature new` reads it with `gh` and takes the issue title for the id unless `--title` is given (package 03). **Decided** on 2026-10-25 (rework of `feature new`), replacing "the agent reads the issue with its own tools, because the engine does not call trackers". If `gh` is missing or cannot read the issue, for example because it is not signed in to the host, `feature new` stops with exit code 33 (`invalid-source`); the agent shows the message to the human, whose last sentence says what to fix: install `gh`, or check the issue URL and that `gh` is signed in for its host (package 03, [Errors](03-feature-management.md#errors)).
4. Call `shade-kit feature new --source <…> [--profile <p>] [--title <…>] --json`. Without `--profile`, the engine uses `defaultProfile`. `--title` is optional for an issue URL of a tracker in `config.json` and required for any other URL, whose content the engine reads with an HTTP GET.
5. There is nothing to complete in `request.md`. `feature new` writes the request content into it: the text unchanged, the file content, the issue as Markdown (title, body, comments), or the content of another URL, with the line endings of the repository (package 03, [`request.md`](03-feature-management.md#requestmd)); the source itself is in `feature.json`. **Decided** on 2026-10-25 (rework of `feature new`), replacing "the agent completes the pre-filled `request.md`" **Decided** on 2026-10-20. The agent does not rewrite or summarise `request.md`.
   - `request.md` can hold third-party content: the body and every comment of an issue, which anyone who may comment can write, or a whole web page. Every skill that reads it (`shade-kit-new`, `shade-kit-refine`, `shade-kit-diagnose`, `shade-kit-quick`, and the research agents of `shade-kit-research`) treats it as data that describes the requirement, never as instructions to the agent: it does not follow commands, links, or tool requests in it, and shows the human any instruction in it that looks suspicious (a research agent reports it to the calling skill, which shows it). **Decided** on 2026-10-25 (code review of the rework of `feature new`, finding F6), replacing no earlier rule: before the rework the agent read the issue itself and chose what went into `request.md`. The engine keeps writing the content unchanged (package 03, [`request.md`](03-feature-management.md#requestmd)). **Implemented** (the five skill files).
6. Offer to create and switch to the branch `feature/<id>` (suggested by package 03) so the current feature follows the branch. The agent runs the git command only after a yes. **Proposed.**
7. Report the id and the next phase of the profile (`refine`, `diagnose`, or `quick`). The transition then goes through the entry protocol of that phase skill (package 06).

### Templates

| Template | Content |
|---|---|
| `discussion.md` | Topic, date, participants, context, options, conclusions, open questions. |

## Acceptance criteria

- **FP09-AC1** `discuss` without a current feature writes its notes to `.shadekit/discussions/` and creates no feature.
- **FP09-AC2** `discuss` with a current feature writes its notes into the feature's `discussions/` folder and leaves `feature.json` unchanged.
- **FP09-AC3** `new` with an issue URL creates an issue-based id, and `request.md` holds the issue's title as a one-line heading, its body and its comments unchanged except for trailing line breaks, as `feature new` writes them (package 03, [Tracker issues](03-feature-management.md#tracker-issues-gh)).
- **FP09-AC4** `new` with a Markdown file or discussion notes passes the file to `feature new`, which writes its content into `request.md`, unchanged except for the line endings, which follow package 03.
- **FP09-AC5** `new` creates a branch only after the human agrees.
- **FP09-AC6** After `new`, `shade-kit-next-phase` offers the next phase of the feature's profile.

## Open points

- None. The open point about the guidance comment of the shipped `request.md` skeleton is resolved: the skeleton was removed on 2026-10-25 (package 03).
