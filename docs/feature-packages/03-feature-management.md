# 03 · Feature management

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-18 (`feature drop` confirmation, `status` in `feature current`, `feature list` implemented) |
| Source | Concept §3.1, §3.2, §4.1–4.3, §5.1 (`new`, `drop`), §11.4 |
| Depends on | 01, 02 |
| Enables | 04 and every phase package |

## Benefit

Several developers can create features in parallel on their own branches without id collisions. The engine always knows which feature a developer is working on, from the branch name or a local override, so neither the human nor the agent has to repeat it in every session.

## Scope

### In scope

- Feature ids.
- The feature folder and the identity part of `feature.json`.
- `feature new`, `feature list`, `feature current`, `feature set`, `feature show`, `feature drop`.
- Resolving the current feature.

### Out of scope

- Phases, phase states, approvals, and history in `feature.json` (package 04). `feature new` sets the first phase through package 04's rules once that package exists.
- Fetching issue content from a tracker. The agent does that in the `new` skill (package 09); the engine only receives the result.
- The separate spec store (later, concept §4.4).

## Functional description

### Feature ids

- No sequential numbers. **Decided.**
- With an issue: `<tracker prefix>-<number>-<slug>`, for example `gh-142-login-sso`. The prefix comes from the `trackers` mapping in `config.json` (package 02). **Decided** (format), **Proposed** (mapping).
- Without an issue: `<yyyymmdd>-<hhmm>-<slug>`, for example `20261013-1530-login-sso`. **Decided.**
- The slug is built from the title: lower case, ASCII, words joined by `-`, limited to a short length. **Proposed.**
- If the id already exists, `new` stops with an exit code. It does not add a suffix silently, because an existing id usually means the feature was already created on another branch. **Proposed.**

### Feature folder

`.shadekit/features/<feature-id>/` with:

- `feature.json`: `schemaVersion`, `id`, `title`, `profile`, `source` (kind and value), `createdAt`, `createdBy`, `status`. Package 04 adds the phase fields.
- `request.md`: created from the `request.md` template (package 09 owns its content) and filled with the original input.

Feature `status`: `active` or `done`. **Proposed.** A dropped feature has no status, because its folder is deleted.

`createdBy` and every other "who" in the kit is the git user (`user.name` and `user.email`) of the repository. **Proposed.**

### Commands

| Command | Behaviour |
|---|---|
| `feature new --profile <p> --source <text\|url\|file> [--title <t>]` | Creates the folder, `feature.json`, and `request.md`. The profile defaults to `defaultProfile` from `config.json`. A URL whose host is in `trackers` gives an issue-based id; the number comes from the URL, the slug from `--title`. Any other source gives a time-based id. For a file source, the file content is copied into `request.md`. Writes the local override so the new feature is current right away. Returns the id and paths with `--json`. **Proposed** details. |
| `feature list [--json]` | Lists features with id, title, profile, status, and (once package 04 exists) the current phase. **Implemented** with feature names only (`FeatureListCommand`); extended with the new fields. |
| `feature current [--json]` | Returns the current feature: id, profile, feature `status` (`active` or `done`), artifact paths, and (once package 04 exists) current phase and phase status. If none can be resolved, returns "not resolved" as a result, not as an error (package 01 contract). |
| `feature set <id>` | Writes `features/current-feature.json`. Stops if the id does not exist. |
| `feature show <id> [--json]` | Returns the complete state of a feature, including the approval history from package 04. Needed by the report in package 15. **Proposed.** |
| `feature drop <id> [--yes]` | Deletes the feature folder and clears the local override if it points to this feature. Possible in every phase. With `--yes` it deletes at once. Without `--yes` it first describes what it would delete; then, only in plain mode (no `--json`) with an interactive standard input, it asks for a confirmation in the console and deletes after a yes. In JSON mode or with redirected standard input it stops after the description and deletes nothing, so an agent can never hang on the question. An agent calls it with `--json`: first without `--yes` to get the description it shows the human, and after the human's yes in the conversation with `--yes`. **Decided** on 2026-10-18. |

### Resolving the current feature

Order (**Decided**):

1. The current branch name. The id is the part after the last `/` (`feature/gh-142-login-sso` → `gh-142-login-sso`). It counts only if a feature folder with that id exists. **Proposed** (matching rule).
2. The local override `features/current-feature.json`.
3. Not resolved; the agent asks the human and calls `feature set`.

If 1 and 2 both give a feature and they differ, the branch wins and the command adds a warning. **Proposed** in the concept, adopted here.

`current-feature.json` is gitignored (package 02) and has its own `schemaVersion` like every JSON file (package 01).

### Branches

The engine does not create or switch branches in v1. `feature new` returns the suggested branch name `feature/<id>` so the skill can offer to create it. **Proposed.**

## Acceptance criteria

- **FP03-AC1** `feature new` with a GitHub issue URL and a title creates `gh-<number>-<slug>`.
- **FP03-AC2** `feature new` with plain text creates a time-based id `<yyyymmdd>-<hhmm>-<slug>`.
- **FP03-AC3** `feature new` with an id that exists already stops with an exit code and changes nothing.
- **FP03-AC4** After `feature new`, `feature current` returns the new feature even on a branch whose name matches no feature.
- **FP03-AC5** On branch `feature/<id>` with an existing folder `<id>`, `feature current` returns `<id>`, also when the local override points to another feature; the output then contains a warning.
- **FP03-AC6** Without a matching branch and without an override, `feature current --json` returns "not resolved" with exit code 0.
- **FP03-AC7** `feature set` with an unknown id stops with an exit code.
- **FP03-AC8** `feature drop <id> --json` without `--yes` deletes nothing and returns what it would delete; with `--yes` it deletes the folder and clears an override that points to it.
- **FP03-AC9** `feature.json` records the git user as `createdBy`.
- **FP03-AC10** `feature drop <id>` in plain mode with an interactive standard input and without `--yes` asks for a confirmation and deletes only after a yes; with redirected standard input it deletes nothing and does not wait for input.
- **FP03-AC11** `feature current --json` returns the feature `status`, and `done` after `phase complete` (package 04).

## Open points

- Trackers other than GitHub only work if the team adds them to `trackers` in `config.json`. Whether v1 needs URL patterns beyond the host (for example Jira keys) is **Open**.
