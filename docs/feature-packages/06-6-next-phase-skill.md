# 06-6 · `shade-kit-next-phase`

| | |
|---|---|
| Status | Implemented (AC1 to AC5), 2026-11-11: `skills/shade-kit-next-phase/SKILL.md`. Draft of 2026-11-04 (split from package 06). |
| Source | [06 Skill foundation](06-skill-foundation.md) (`shade-kit-next-phase`), [phase start analysis](../../research/shade-kit-phase-start-analysis.md) |
| Kind | Skill (skeleton worked out) |
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

Shows the human where a feature stands and which skill to call next. It is advisory: it starts no phase skill, never runs a gate check, and never calls `phase start`, `phase approve`, or `phase reject`. **Decided** on 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md)), replacing "starts the phase skill the human chooses" from the skills interview on 2026-10-17. **Implemented**.

1. Version handshake: `shade-kit version --json --expect <version>` with the version stated in the text of the step, as 06-2 sets out; the frontmatter is the source, but the model does not see it (06-1, 06-2). **Decided** on 2026-11-04, the version in the text on 2026-11-06. On a mismatch, or if `shade-kit` is not found, end with the next-steps message (06-3). **Implemented** (`--expect 0.7.0`, the same wording as `shade-kit-phase-entry`).
2. `shade-kit feature current --json`. If not resolved: list the features (`feature list --json`) and ask which one to use; after the human picks one, the skill may call `feature set <id>`, which starts no phase. Otherwise name `/shade-kit-new` and end. **Implemented**.
3. `shade-kit phase next --json` (package 04). The skill does not derive the steps from the profile. **Implemented**.

| State | `phase next` returns | Shown |
|---|---|---|
| feature `done` | only the status | Report that the feature is finished; name `shade-kit feature set <id>` for another feature and `/shade-kit-new`. |
| current phase `in-progress` | `continue`, `forward` | Continue the current phase: `/shade-kit-<current>`; move on: `/shade-kit-<next>`. |
| current phase `awaiting-approval` | `pending` | Decide on the pending transition: `/shade-kit-<pending>`; discard it and continue the current phase: `/shade-kit-<current>`. |
| current phase `in-progress`, earlier phases exist | `reopen[]` | Reopen an earlier phase: `/shade-kit-<earlier>` (always needs approval). |
| any state of an active feature | | Always shown as well: `/shade-kit-discuss`, `/shade-kit-drop`. |

Only `pending` and `forward` have a gate.

4. End with these steps through `shade-kit-next-steps` (06-3), including the warnings of `phase next`. The human chooses by calling the skill. **Implemented**: the skill passes the result of `phase next` and the warnings of all its CLI calls, and `shade-kit-next-steps` writes the message from them without running `phase next` again.

### Decisions and defaults of the implementation

- **A feature named in the call while none is resolved**: the skill still lists the features and asks; the named feature is offered first. `feature set` changes the current feature, so the human confirms it. **Decided** on 2026-11-11 (implementation clarification). **Implemented**.
- **A question about another feature than the resolved one**: the skill does not switch. It shows the current feature and says how to switch: `shade-kit feature set <id>` when the override selected the current feature, or a branch whose name ends in `/<id>` (`feature new` suggests `feature/<id>`) when the branch selected it, because the branch wins over the override (03). **Decided** on 2026-11-11 (implementation clarification). **Implemented**.
- **Only active features are offered** for the pick; a finished feature has no next step. Without an active feature the skill asks nothing and ends. **Decided** on 2026-11-11 (implementation clarification). **Implemented**.
- **Proof** by scenario runs with the skeleton as the baseline, checked by a script, as in 06-4 and 06-5. **Decided** on 2026-11-11.
- **Ending without a feature** (no active feature, a new feature chosen, no pick, no answer): reason end of work; `shade-kit-next-steps` runs `phase next`, which fails with `no-current-feature`, and its refusal row names `/shade-kit-new`, `shade-kit feature list`, and `shade-kit feature set <id>`. The skill writes no message of its own. **Proposed**.
- **An answer that names no listed feature**: ask once more, then end without a pick. **Proposed**.
- **The "what happened" line** passed to `shade-kit-next-steps` is "Status of `<id>`.", plus, where it applies, that `<id>` is now the current feature (after `feature set`), the line about another feature, and that an approval or phase start asked for in the call is not recorded and the phase skill asks for it. The last one was added after iteration 1: a runner noted that a human who approved in the call may believe the transition went through. **Proposed**.
- **Warnings**: the skill keeps the warnings of every CLI call and passes them on; the closing message shows each one once. **Proposed** (as in 06-5).
- **The passed `phase next` result in `shade-kit-next-steps`** (06-3): its input section said to use a passed result, while its flow steps 1 and 2 said to run `phase next`. Runners of three scenarios noted the contradiction, though all followed the input section. Flow step 3 of `shade-kit-next-steps` now states the rule once (use a passed result, otherwise run `phase next` once), and steps 1 and 2 refer to it. **Decided** on 2026-11-11. **Implemented**; proven in iteration 3 (below).
- The skill runs only `shade-kit version --json --expect 0.7.0`, `feature current`, `feature list`, `feature set <id>` (only after the human picked that feature), and `phase next`, each with `--json`; loads only `shade-kit-next-steps`; reads or writes no file. **Implemented**.

## Acceptance criteria

Status as of 2026-11-11. Skill texts have no automated tests, so each criterion names how it is proven: scenario runs in which a fresh subagent loaded the skill as if the human had called it, and followed it and `shade-kit-next-steps` with canned CLI envelopes and canned answers of the human. The subagent was not told the expected result and recorded every command, question, loaded skill, and its closing message, and a script checked every run. The baseline was the skeleton of the skill. Iteration 1 ran the first text, iteration 2 the text after three fixes found in iteration 1 (an approval in the call is not recorded and the message says so; the message says when `feature set` changed the current feature; the branch is named by its `/<id>` ending). Each scenario ran once per text and configuration, so the variance is not measured. The scenarios, fixtures, outputs, and the checker stay outside the repository, as in 06-4 (D-28).

| # | Scenario | Iteration 1 | Iteration 2 | Skeleton baseline (iteration 2 checks) |
|---|---|---|---|---|
| 1 | Version mismatch (`version-mismatch`) | 9 of 9 | 9 of 9 | 6 of 9: `version --json` without `--expect` cannot detect a mismatch |
| 2 | Resolved, `map` `in-progress`, `gates-defaults-used` | 12 of 12 | 12 of 12 | 8 of 12: the warning not shown |
| 3 | Not resolved, feature named in the call, the human picks it | 14 of 15 | 15 of 15 | 11 of 15: offers the done feature, `feature set` without `--json` |
| 4 | Not resolved, the human wants a new feature | 10 of 10 | 10 of 10 | 7 of 10 |
| 5 | `awaiting-approval`, the human approves in the call | 11 of 12 | 12 of 12 | 9 of 12 |
| 6 | Resolved by the branch, the human asks about another feature | 10 of 11 | 11 of 11 | 8 of 11 |
| 7 | Not resolved, only a done feature | 9 of 9 | 9 of 9 | 4 of 9: asks and sets the done feature |
| 8 | Resolved feature is `done` | 11 of 11 | 11 of 11 | 8 of 11 |
| | Total | 86 of 89 (iteration 1 checks: 86 of 86) | 89 of 89 | 61 of 89 (iteration 1 checks: 60 of 86) |

The baseline ran once, in iteration 1, and was checked again with the checks of iteration 2.

Iteration 3 ran after the change to flow step 3 of `shade-kit-next-steps` (decisions above), with the text of iteration 2 of this skill: scenarios 2, 5, and 8 (a `phase next` result passed) and 4 and 7 (none passed) again, 54 of 54, `phase next` run exactly once in each; and two new scenarios of `shade-kit-next-steps` alone, loaded by `shade-kit-refine` without a `phase next` result: the end of work in `refine` (7 of 7) and a refused `phase start implement` with `transition-not-allowed` (9 of 9), in each `phase next` run exactly once and no other command. The runners of scenarios 4 and 7 noted that `shade-kit-next-steps` runs a `phase next` there that can only fail with `no-current-feature`; that is the **Proposed** ending without a feature above.

Six checks run in every scenario: the first command is the handshake with `--expect 0.7.0`; `--json` on every command; no `phase start`, `phase approve`, `phase reject`, `phase complete`, `gate info`, `feature new`, `feature drop`, or `init`; no file read or written; `shade-kit-next-steps` loaded; neither `shade-kit-gate-check` nor a phase skill loaded. The runners of iteration 2 still noted points of wording that the text leaves to the agent (where the added sentences go in the "what happened" line, whether to suggest calling the skill again after switching the branch); none changed a decision.

- **FP06-6-AC1** With an engine whose minor version differs from the skill's, or without `shade-kit`, the skill stops before any other CLI call and names what to update. **Implemented.** Proven in scenario 1: only `shade-kit version --json --expect 0.7.0` ran, `shade-kit-next-steps` was loaded with reason refusal and the `version-mismatch` envelope, and the message names the update to 0.7. A missing `shade-kit` takes the same ending ("`shade-kit` not found"); it has no scenario run.
- **FP06-6-AC2** With no resolvable current feature, the skill lists the features and names `/shade-kit-new`; it starts no skill. **Implemented.** Proven in scenarios 3, 4, and 7: `feature list --json` after the unresolved `feature current`; in 3 and 4 one question with both active features, the new feature, and no pick (in 3 the named feature first), never the done feature; in 4 and 7 no `feature set`, no skill but `shade-kit-next-steps` loaded, and `/shade-kit-new` in the message; in 7 no question.
- **FP06-6-AC3** For a feature whose current phase is `in-progress`, the skill names the phase skill of the current phase. **Implemented.** Proven in scenarios 2 (`/shade-kit-map`), 3 (`/shade-kit-diagnose`, after `feature set gh-150-fix-crash --json`), and 6 (`/shade-kit-map` of the branch's feature).
- **FP06-6-AC4** The skill never runs a gate check and never calls `phase approve`. **Implemented.** Proven in all eight scenarios (common checks), including scenario 5, where the human approved in the call: no `phase approve`, no question, and the message says that the approval is not recorded and `/shade-kit-plan` asks for it.
- **FP06-6-AC5** The skill shows exactly the steps `phase next --json` returns, plus `shade-kit-discuss` and `shade-kit-drop`, each with the skill to call, and starts no skill. **Implemented.** Proven in scenarios 2, 3, 5, 6, and 8: the steps of the canned result and `/shade-kit-discuss` and `/shade-kit-drop`, no other phase skill named, `phase next` run once (not again by `shade-kit-next-steps`); in 5 `pending` with `/shade-kit-plan` and the discard step `/shade-kit-map`; in 8 the finished feature with `shade-kit feature set <id>` and `/shade-kit-new`.

## Open points

- The defaults of the implementation marked **Proposed** above.
- No scenario run for a missing `shade-kit`, for a failing `feature current`, `feature list`, `feature set`, or `phase next`, or for an answer that names no listed feature; the text covers each case.
