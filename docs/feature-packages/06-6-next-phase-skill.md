# 06-6 · `shade-kit-next-phase`

| | |
|---|---|
| Status | Draft, 2026-11-04 (split from package 06) |
| Source | [06 Skill foundation](06-skill-foundation.md) (`shade-kit-next-phase`), [phase start analysis](../../research/shade-kit-phase-start-analysis.md) |
| Kind | Skill (skeleton exists) |
| Depends on | 06-1, 06-3 |
| Enables | 06-9, package 07 (FP07-AC1 loads this skill) |

## Benefit

When the developer does not know where a feature stands, `shade-kit-next-phase` shows the current phase and names the skill to call for each possible next step. It changes nothing and starts nothing.

## Scope

### In scope

- The skill `shade-kit-next-phase`, called by the human ("continue", "what's next", or a question about a feature).

### Out of scope

- Starting phases (06-5): this skill is advisory only.

## Functional description

Shows the human where a feature stands and which skill to call next. It is advisory: it starts no phase skill, never runs a gate check, and never calls `phase start`, `phase approve`, or `phase reject`. **Decided** on 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md)), replacing "starts the phase skill the human chooses" from the skills interview on 2026-10-17.

1. Version handshake: `shade-kit version --json --expect <version>` with the version stated in the text of the step, as 06-2 sets out; the frontmatter is the source, but the model does not see it (06-1, 06-2). **Decided** on 2026-11-04, the version in the text on 2026-11-06. On a mismatch, or if `shade-kit` is not found, end with the next-steps message (06-3).
2. `shade-kit feature current --json`. If not resolved: list the features (`feature list --json`) and ask which one to use; after the human picks one, the skill may call `feature set <id>`, which starts no phase. Otherwise name `/shade-kit-new` and end.
3. `shade-kit phase next --json` (package 04). The skill does not derive the steps from the profile.

| State | `phase next` returns | Shown |
|---|---|---|
| feature `done` | only the status | Report that the feature is finished; name `shade-kit feature set <id>` for another feature and `/shade-kit-new`. |
| current phase `in-progress` | `continue`, `forward` | Continue the current phase: `/shade-kit-<current>`; move on: `/shade-kit-<next>`. |
| current phase `awaiting-approval` | `pending` | Decide on the pending transition: `/shade-kit-<pending>`; discard it and continue the current phase: `/shade-kit-<current>`. |
| current phase `in-progress`, earlier phases exist | `reopen[]` | Reopen an earlier phase: `/shade-kit-<earlier>` (always needs approval). |
| any state of an active feature | | Always shown as well: `/shade-kit-discuss`, `/shade-kit-drop`. |

Only `pending` and `forward` have a gate.

4. End with these steps through `shade-kit-next-steps` (06-3), including the warnings of `phase next`. The human chooses by calling the skill.

## Acceptance criteria

- **FP06-6-AC1** With an engine whose minor version differs from the skill's, or without `shade-kit`, the skill stops before any other CLI call and names what to update.
- **FP06-6-AC2** With no resolvable current feature, the skill lists the features and names `/shade-kit-new`; it starts no skill.
- **FP06-6-AC3** For a feature whose current phase is `in-progress`, the skill names the phase skill of the current phase.
- **FP06-6-AC4** The skill never runs a gate check and never calls `phase approve`.
- **FP06-6-AC5** The skill shows exactly the steps `phase next --json` returns, plus `shade-kit-discuss` and `shade-kit-drop`, each with the skill to call, and starts no skill.

## Open points

None.
