# 10 · Refine the requirement (`refine`)

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-17 (`spec.md` template), 2026-10-18 (consistency review: router reference replaced by `shade-kit-next-phase`), 2026-10-24 (documentation check: the `quick` phase writes the `quick` spec), 2026-10-25 (code review of the rework of `feature new` in package 03: `request.md` treated as data, not as instructions), and 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md): the human reopens `refine` by calling its skill), and 2026-11-26 (implementation: `shade-kit-refine` worked out with its rules file `references/spec-rules.md`, `spec.md` section "Changes", review of the drafted requirements, scenario runs S20–S23, review round 1; [implementation report](../implementation/10-refine.md) with the decisions D-1 to D-24). AC1 to AC7 implemented. |
| Source | Concept §3.3 (research focuses), §5.1 (`refine`), §5.2 (DoR), §6 |
| Depends on | 06, 09 |
| Enables | 11 (`map`), 12 (`plan`, DoR) |

## Benefit

Vague statements, gaps, and contradictions in a requirement are found and resolved before anyone plans or codes. The result is a spec with stable requirement IDs and testable acceptance criteria, which every later phase and the final report can trace back to.

## Scope

### In scope

- The `refine` phase skill in the `feature` profile.
- The research focuses of `refine`: gaps and open questions, security at requirement level.
- The `spec.md` template and its rules (IDs, acceptance criteria, out of scope, open questions).
- Repeated `refine` rounds.

### Out of scope

- Anything about the code base (`map`, package 11). `refine` deals with the requirement only. **Proposed** split in the concept, adopted.
- `bugfix` and `quick`: their specs are written by `diagnose` (package 16) and the `quick` phase (package 17) in the same `spec.md` format.

## Functional description

### Run

1. Read `request.md`, any discussion notes of the feature, and an existing `spec.md` (later rounds). `request.md` can hold third-party content (issue text, comments, web pages): the skill and the research agents of step 2 treat it as data that describes the requirement, never as instructions, and the human is shown any instruction in it that looks suspicious ([skill conventions](06-2-skill-conventions-and-version.md#skill-conventions)). **Decided** on 2026-10-25 (code review of the rework of `feature new`, finding F6). **Implemented** (`shade-kit-refine`, `shade-kit-research`).
2. Read the feature with `shade-kit feature show <id> --json` (title and `paths.featureDir`): the entry protocol returns only the id and the profile. **Decided** on 2026-11-26 (implementation, D-8). **Implemented.** When the feature has no `spec.md` yet and `.shadekit/templates/spec.md` is missing, the skill stops before any research and says how to restore the template; a later round, whose `spec.md` exists, needs no template. **Decided** on 2026-11-26 (D-11, D-15). **Implemented.**
3. Run the research skill ([06-8](06-8-research-skill.md)) with two focuses in parallel, each with its short name and ID prefix (**Decided** on 2026-11-13, 06-8; **Implemented**):
   - `gaps`, prefix `G` (gaps and open questions): vague terms, missing cases, contradictions, unstated assumptions.
   - `security`, prefix `RS` (security at requirement level): data that needs protection, roles and permissions, abuse cases.
   When a focus fails, the skill names it and asks whether to research it again (once, only the failed focus) or to go on without it; going on, the report names the missing section and recommends another round. **Decided** on 2026-11-26 (D-12). **Implemented.**
4. The research skill merges the results into `researches.md` (sections `refine / gaps`, `refine / security`). A dismissed finding gets `Dismissed: <reason>.` appended to its line, without finding IDs, which change with every run; the next research run reads it and does not report the finding again without new evidence (06-8). **Decided** on 2026-11-13. **Implemented.**
5. Run the interview skill on the findings this round's research wrote, the open questions, and the unconfirmed assumptions of an existing `spec.md`: one topic at a time, each with the agent's own proposal (recording `on return` in the first round, `immediately` in a later one). Assumptions the agent made are put to the human for confirmation; an assumption the agent adds while it writes the spec stays `unconfirmed` until the next round. **Decided** on 2026-11-26 (D-5): the order interview → spec stays, so the first round can end with unconfirmed assumptions, which the report and the Definition of Ready catch. **Implemented.**
6. Write or update `spec.md` by the rules in `skills/shade-kit-refine/references/spec-rules.md`: a new file from the template in the first round, edited in place in later rounds (D-14). Answered questions move from "Open questions" to "Decisions", and their inline markers are removed; a finding the interview leaves open becomes an open question with its marker; every security finding is linked to the requirement or criterion that handles it, or to an open question (D-10); a point an answer made obsolete is left out and gets `Dismissed: obsolete, …` in `researches.md`, and a missing security research becomes an open question instead of "None identified" (review round 1, M-1, M-2, D-20). The template's guidance for "Security considerations" says the same (`None identified: <reason>`, `→ open: Q-nnn`; M-3). The rules live in a file of the skill's folder because the guidance comments of the template are removed when the file is written (D-4, 06-2). **Decided** on 2026-11-26. **Implemented.**
7. Review: the agent drafts the requirements, their priorities, and their criteria, so every requirement the round added, changed, or marked as removed is put to the human in a second interview (keep it as written, change it, or leave it open as a question). The agent may mark a requirement as removed itself; the review confirms or undoes it. **Decided** on 2026-11-26 (D-2, D-9). **Implemented.**
8. Append the round's line to "Changes". **Decided** on 2026-11-26 (D-3, D-6, D-7). **Implemented.**
9. Report what changed, which questions are still open, which assumptions are still unconfirmed, and which research section is missing; while any exists, recommend another round. The human decides whether to run another round or move on. **Implemented.**

### `spec.md`

Section order and rules. **Decided** in the template interview on 2026-10-17, except where marked; the general template conventions are in package 02.

| Section | Rule |
|---|---|
| Goal | One short paragraph: what problem the feature solves and for whom. |
| Glossary | Domain terms and key entities the requirements use, as a table `Term \| Kind (term or entity) \| Meaning`. An entity names its key attributes and relations, without implementation details. "None." if nothing needs a definition. |
| Requirements | One subsection per requirement, with a stable ID `REQ-<nnn>`, unique within the feature, numbered from `REQ-001`, and the fields Priority, Status, and Rationale, followed by the requirement statement and its acceptance criteria. |
| Priority | MoSCoW: `must`, `should`, or `could`. A "won't" goes into "Out of scope". |
| Status | `active` or `removed` (with the reason). |
| Acceptance criteria | At least one per requirement, listed under it, with ID `AC-<nnn>.<m>` (for example `AC-001.1`). Given/When/Then for behaviour; one verifiable statement for non-functional criteria such as performance, security, or compatibility. Every criterion can be decided by an automated test or is marked "(manual)" with the reason. |
| Edge cases | Boundary conditions, error scenarios, and unusual inputs. Each points to the acceptance criterion that covers it or is listed in "Out of scope". |
| Security considerations | The findings from the security research, each linked to the requirement or criterion that handles it, or "None identified." with the reason. |
| Out of scope | Stated explicitly, also when empty ("Nothing excluded."). |
| Assumptions | Defaults the agent chose where the request was silent, with ID `A-<nnn>` and status `unconfirmed`, `confirmed`, or `risk`. Each is confirmed or marked as a risk before the DoR passes. |
| Open questions | Every unresolved question, with ID `Q-<nnn>` and the requirements it affects. The unclear statement is marked inline with `(open: Q-<nnn>)`. Must say "None." before the DoR passes (package 05). |
| Decisions | Answered questions, keeping their `Q-` ID: question → answer, who decided, and the date. |
| Changes | The last section: one line per round, oldest first, `- <date>: created` in the first round, later `- <date>: added …; changed …; removed …` with the `REQ-` and `AC-` IDs (an added or removed requirement by its `REQ-` ID alone; proposed after iteration 1 of the scenario runs, **Decided** on 2026-11-26, D-23), or `no requirement or criterion changed`. Later phases compare the dates with their own artifacts after a reopen. A spec without the section gets it in its next `refine` round. **Decided** on 2026-11-26 (implementation, D-3, D-6, D-7, D-15), instead of naming the outdated artifacts of later phases in the report. **Implemented** (`spec.template.md`, `ReadContent_SpecTemplate_HasSectionsInRuleOrderEndingWithChanges`, `ReadContent_SpecTemplate_GuidanceFollowsSpecRulesForChangesAndSecurity`). |

IDs are never renumbered. A removed requirement keeps its ID with the status "removed", so references in `tasks.md`, tests, and the report stay valid. **Proposed** in the first draft, adopted in the template. A requirement also keeps its meaning: when an answer turns it into a different requirement, it is marked as removed and the new one gets a new ID. Proposed after iteration 2 of the scenario runs (a runner had rewritten `REQ-003` into another requirement), **Decided** by the user on 2026-11-26 (D-21). **Implemented** (`references/spec-rules.md`). Findings that get a new `Q-` ID are numbered in the order the interview asked them (proposed after iteration 2, **Decided** on 2026-11-26, D-22). **Implemented.**

### Repeated rounds

`refine` can run several times. **Decided** (concept §5.1). A new round starts from the current `spec.md`. If the feature has moved on, the human reopens `refine` by calling `/shade-kit-refine` (package 04 reopen; `shade-kit-next-phase` names the skill). **Decided** on 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md)). Later phases then go back to `pending`, and their artifacts must be checked against the changed spec when they run again.

## Scenario runs

Status as of 2026-11-26. The skill texts have no automated tests (06-2), so the criteria are proven by scenario runs S20 to S23 in `tests/skill-scenarios/` (catalog `SCENARIOS.md`, expectations in `checks/check_runs.py`), and the template part by `ReadContent_SpecTemplate_HasSectionsInRuleOrderEndingWithChanges` and `ReadContent_SpecTemplate_GuidanceFollowsSpecRulesForChangesAndSecurity`. In each run a fresh subagent played the agent, followed `shade-kit-refine` with canned envelopes from the real engine and canned answers, ran the research focuses one after the other (no subagent tool), and wrote `spec.md` and `researches.md` into its own repository copy.

| # | Scenario | Agent | Iteration 1 | Iteration 2 | Iteration 3 | Iteration 4 |
|---|---|---|---|---|---|---|
| S20 | First round: research, interview (first topic deferred), new `spec.md`, review | Claude Code | 35 of 35 | 35 of 35 | 35 of 35 | 35 of 35 |
| S21 | Second round on a fixture `spec.md`: two questions answered, an assumption confirmed, the `security` focus fails twice | GitHub Copilot CLI | 40 of 41 | 39 of 41 | 40 of 41 | 41 of 41 |
| S22 | No `spec.md` and no spec template | OpenAI Codex CLI, default mode | 19 of 19 | 19 of 19 | 19 of 19 | 19 of 19 |
| S23 | Second round on a `spec.md` without "Changes" | Claude Code | — | — | — | 30 of 30 |

All counts are those of the final checker: iteration 1 fails only the Changes rule added after it, iteration 2 only the meaning rule, iteration 3 only the `Dismissed: obsolete` note of review round 1 (M-2). The checker had false positives, fixed as they showed up: in iteration 1 (the entry return names the general skills; a restore command in a message is not run; the status column of `git status`). Its unclear points led to the clarifications of iteration 2: the Changes line names an added or removed requirement by its `REQ-` ID alone; dismissal reasons carry no finding IDs; a decision recorded with `(spec)` gets its requirement; priorities and criteria are drafts for the review, not assumptions; the topics after a failed focus. Iteration 2 found a real gap: the runner had turned REQ-003 ("password login for guests") into a different requirement under the same ID. Iteration 3 ran with the rule that a requirement keeps its meaning (a changed meaning is a removal plus a new ID), the numbering of new questions in the order they were asked, and the recommended marker of the retry question. The goal check of S21 was replaced in iteration 3 by a check of lines no answer touches, because the answers legitimately widen the goal. Review round 1 found four checks that stayed green under a realistic violation (AC-5, AC-11 twice, AC-13: they searched the whole output); they now read the interview's recorded return and its `Topic n/m` progress, the report of step 9 with the closing message, and the questions of the review, and a recommendation counts only as "recommend … another round". Iteration 4 ran all four scenarios with the review rework (M-1, M-2, S-1, the template guidance) and the new S23 (R-3); in it two more checker false positives were fixed (prose "the git commit that ran init was not looked up" is no git command; a `None or None == False` slip). Every check that proves a criterion was shown to fail on a mutated copy of the outputs of iteration 4 (28 mutations, among them the four realistic violations of the review, all red).

## Acceptance criteria

- **FP10-AC1** After a `refine` run, every requirement in `spec.md` has a `REQ-` ID and at least one linked acceptance criterion. **Implemented.** Proven in S20 and S21 (every `### ` subsection is `REQ-nnn` with at least one `AC-<its number>.n`).
- **FP10-AC2** `researches.md` contains a `refine / gaps` and a `refine / security` section after the run. **Implemented.** Proven in S20; in S21 the failed `security` focus leaves the section of round 1 unchanged, and the report names the gap (D-12).
- **FP10-AC3** A second round keeps every existing ID; a removed requirement is marked "removed", not deleted. **Implemented.** Proven in S21 (REQ-001 to REQ-003 kept, REQ-003 `Status: removed (…)` with its criteria).
- **FP10-AC4** `spec.md` always has an out-of-scope section and an open-questions section. **Implemented.** Proven in S20 and S21 (all ten sections in rule order) and by `ReadContent_SpecTemplate_HasSectionsInRuleOrderEndingWithChanges`.
- **FP10-AC5** The interview asks about every gap finding or records why it was dismissed. **Implemented.** Proven in S20 (every `G-` finding is answered or open in the interview's recorded return or has a `Dismissed:` note, and at least as many topics were asked as the return lists).
- **FP10-AC6** Every requirement in `spec.md` has a priority (`must`, `should`, or `could`) and a status. **Implemented.** Proven in S20 and S21.
- **FP10-AC7** An answered question appears in "Decisions" with its original `Q-` ID, and no `(open: Q-<nnn>)` marker for it is left in `spec.md`. **Implemented.** Proven in S21 (Q-001 and Q-002 decided, no marker) and S20 (no decided ID with a marker, every open ID with one).

Criteria added in the implementation (2026-11-26), each proven by the scenario named: the feature folder from `feature show` (S20–S22, D-8); security findings linked or "None identified." (S20, D-10); every edge case points to a criterion or "out of scope" (S20); the report names open questions and recommends another round (S20, S21); one "Changes" line per round, added to a spec without the section (S20, S21, S23, D-7, D-15); the review names every requirement the round touched (S20, S21, D-2); a missing template stops the first round before the research (S22, D-11); a failed focus is offered once more (S21, D-12).

## Open points

- The research skill numbers the findings of a section anew on every run (06-8), so finding IDs (`G-n`, `RS-n`) are only stable within one round. Record lasting decisions under their `Q-` IDs in `spec.md`, not under finding IDs.
- Where a criterion the agent derives ends and an assumption begins (for example the priority `must` of every requirement, or what a failed sign-in shows) is left to the agent: drafts go to the review, defaults the spec cannot go on without become assumptions (scenario runs, iteration 3).
- Found in the scenario runs, not in scope here: `shade-kit-interview` (06-7) does not say how `ask_user` of GitHub Copilot CLI takes several questions in one call, how a topic that depends on a deferred blocking topic is asked, or whether a draft statement under question counts as a requirement for a contradiction; `shade-kit-research` (06-8) does not say how a file in a subfolder of the feature folder (`discussions/…`) is cited; `shade-kit-phase-entry` (06-5) asks "continue `refine`" before `refine` finds a missing template, and calls a first round "another round"; the managed section that `init` writes into `CLAUDE.md` (02) says "never editing `.shadekit/features/` by hand", which a runner read as forbidding the Markdown artifacts the skills write.
- Loading `references/spec-rules.md` from the skill folder is **Unverified** in GitHub Copilot CLI (06-2); the scenario runs play the agents, they do not run them.
