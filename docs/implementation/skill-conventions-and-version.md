> **Date:** 2026-11-06
> **Requirement:** `docs/feature-packages/06-2-skill-conventions-and-version.md`
> **Branch:** feature/feature-06
> **Mode:** sub-agents (sequential)
> **Status:** complete

# 06-2: Kit version in the skill frontmatter, frontmatter visibility check, and skill conventions

## 1. Summary

What changed:
- All 17 skills under `skills/` now carry `metadata.version: 0.7.0` in their frontmatter.
- The visibility check found that Claude Code 2.1.318 does not show the frontmatter to the model. It was run with a canary skill and headless `claude -p` runs on the Skill-tool and slash-command paths, and confirmed by the docs.
- The fallback rule and the exact wording of the handshake step are recorded in 06-2.
- A "Skill conventions" section was added to `CLAUDE.md`.
- A pre-existing defect was fixed along the way: the frontmatter of `shade-kit-report` was invalid YAML.

All 3 tasks are done. The review took 3 rounds, with 0 Critical and 0 Major findings in every round. All files are left uncommitted for you: **nothing has been committed.** Review the changes with `git diff` and `git status`, and commit what you accept.

## 2. Preconditions

- Working tree at start: clean, on `feature/feature-06` (D-1).
- Runtime skill list: available. `skill-creator:skill-creator` was used for implementation, and `code-review` for the review.
- Size note: n/a. The work was not small: 17 skill files plus 8 documents.
- Report language: English, because the repository's CLAUDE.md requires English for all repository documents. This overrides the workflow default (the user's language).

## 3. Requirement and Acceptance Criteria

Every kit skill carries the same kit version in its frontmatter (`metadata.version`, SemVer). The package also checks whether Claude Code shows the frontmatter to the model when it loads a skill, and records the result with the date and the method. If the model does not see the frontmatter, every skill that calls `version --expect` also states the version in the text of its handshake step. The package also sets the conventions every skill follows.

The package's own rule says there are no automated tests of the skill texts (D-13). The "tests" below are one-off checks run in the scratchpad.

| AC | Text | Origin | Checks | Result | Verdict |
|---|---|---|---|---|---|
| AC-1 | Every skill has `metadata.version`, all with the same SemVer value (FP06-2-AC1) | explicit | One-off PyYAML check: 17 files, value is a `str`, matches `^\d+\.\d+\.\d+$`, all equal | pass (17 × `'0.7.0'`) | proven (executed: wrong value, prerelease, removed block, float `0.7`, new skill without a version → check red) |
| AC-2 | The visibility check is recorded with the date and the method (FP06-2-AC2) | explicit | Canary transcripts searched (`9.8.7` 0 hits, body marker present); record in 06-2 §Frontmatter visibility | pass | proven (executed: `9.8.7` injected into a transcript copy → grep 0 → 1); the record itself was read-checked |
| AC-3 | If the model does not see the frontmatter, every skill with `--expect` states the version in its handshake text (FP06-2-AC3) | explicit | Check that every `--expect X` equals `metadata.version` and uses the D-9 wording; rule read in 06-2 | pass, vacuously: 0 skills call `--expect` | proven (executed: handshake with `0.8.0` in a copy → red). Its substance is moved to 06-5, 06-6, and 06-9 (D-4). |
| AC-4 | The frontmatter stays valid | implicit | skill-creator `quick_validate.py` on all 17 | pass (17/17) | proven (executed: the old unquoted `shade-kit-report` description put back → red) |
| AC-5 | Status markers follow the repo convention | implicit | Status lines, AC labels, README log row read | pass | read-checked, not executed (document markers; no check to break) |

Counted separately: 4 proven (executed), 1 read-checked, 0 unproven.

## 4. Decisions

| ID | Point | Decision | Origin | Round |
|---|---|---|---|---|
| D-1 | Working tree | Clean, `feature/feature-06` | n/a (clean) | — |
| D-2 | 0 / G-1 initial value | `metadata.version: 0.7.0` in all 17 skills | user's own (recommendation was 0.8.0) | 1 |
| D-3 | 0 / G-2 prerelease label | SemVer without a prerelease label, now **Decided** | recommendation accepted | 1 |
| D-4 | 0 / G-3 scope of AC-3 | 06-2 sets the rule and wording; 06-5, 06-6, and 06-9 apply it | recommendation accepted | 1 |
| D-5 | 0 / G-4 contradiction 06-2 ↔ 06-5 | Corrected in 06-2: `shade-kit-phase-entry` checks for the phase skills; `new`, `discuss`, `drop`, and `next-phase` check themselves; others do not | recommendation accepted | 1 |
| D-6 | 1 Structure / G-6 | "Skill conventions" section in `CLAUDE.md` with a link to 06-2 | recommendation accepted | 1 |
| D-7 | 2 Architecture | The frontmatter is the source; the text is a copy | pre-answered (06-2:61) | 1 |
| D-8 | 3 Naming | Key `metadata.version` | pre-answered (06-2:49–50) | 1 |
| D-9 | 4 Contracts / G-7 | Handshake text: "Version handshake: run `shade-kit version --json --expect 0.7.0`; 0.7.0 is the kit version of this skill (`metadata.version` in its frontmatter)." | recommendation accepted | 1 |
| D-10 | 4 Contracts | Unquoted `version: 0.7.0` | recommendation accepted | 1 |
| D-11 | 5 Error handling | n/a: no runtime logic; behaviour on a mismatch is in 06-5:38 and 06-6:29 | n/a | 1 |
| D-12 | 6 Tests / G-5 | Visibility method: canary + `claude -p` (Skill tool and slash command, file tools blocked) + session observation + docs search | user's own (option 3) | 1 |
| D-13 | 6 Tests | Verification by `quick_validate.py` + a one-off script; nothing goes into the repo | recommendation accepted | 1 |
| D-14 | 7 Persistence | n/a: no store involved | n/a | 1 |
| D-15 | 8 Dependencies | n/a: PyYAML only temporarily through `uv run --with pyyaml` | n/a | 1 |
| D-16 | Gate 2 skills | `skill-creator:skill-creator` only (`documentation-and-adrs` dropped) | user's own | — |
| D-17 | Gate 2 review | `code-review` skill (run in a sub-agent) | user's own | — |
| D-18 | Gate 3 mode | Sub-agents (recommendation was direct) | user's own | — |
| D-19 | Gate 3 order | Sequential | recommendation accepted | — |
| D-20 | Task #2 BLOCKED | Quote the `shade-kit-report` description, text unchanged | recommendation accepted | — |
| D-21 | Gate 5 round 1 | Fix R06-2-01 to R06-2-10 and the local path in the report; R06-2-11 and R06-2-12 deferred | user's own selection | — |
| D-22 | Gate 5 round 2 | Fix R06-2-13, R06-2-15, R06-2-17; R06-2-14, R06-2-16, R06-2-18 deferred | user's own selection | — |
| D-23 | Gate 5 round 3 | Accept R06-2-19 as is | recommendation accepted | — |

Waivers: none.

## 5. Skills

**Runtime discovery:** the runtime listed about 90 skills. The candidates were `skill-creator:skill-creator`, `anthropic-skills:skill-creator` (a duplicate), `agent-skills:documentation-and-adrs`, and the review skills and agents. `cc-ai-dotnet:*` was excluded because no C# changed. Workflow skills were excluded as nested workflows.

| Skill / agent | Category | Proposed use | Outcome |
|---|---|---|---|
| `skill-creator:skill-creator` | Skill authoring | Format + `quick_validate.py` | kept, loaded in every task |
| `anthropic-skills:skill-creator` | Duplicate | — | excluded |
| `agent-skills:documentation-and-adrs` | Documentation | Decision records | dropped by the user (D-16) |
| `cc-ai-dotnet:*` | .NET | — | excluded (no C# change) |
| Workflow skills (`cc-ai-dev:*`, `agent-skills:build/spec/plan`, `feature-dev`) | Workflow | — | stayed excluded |
| `code-review` | Review | Phase 5 | used in rounds 1 to 3 (effort `high`) |

| Task | Mapped skills | Loaded by implementer | Confirmed by verifier |
|---|---|---|---|
| #1 | skill-creator | yes, turn 1, before the first write | verifier loaded it too; the implementer's order is claimed and could not be checked from files |
| #2 | skill-creator | yes, turn 1, before the first write | same |
| #3 | skill-creator | yes, turn 1, before the first write | same |
| Rework 1 and 2 | skill-creator | yes, turn 1 of the rework agent | same |

## 6. Tasks

### Task #1: Frontmatter visibility check (Status: done)

- Delivers: AC-2. Files: scratchpad only.
- Checks: 4/4.
  - C1, Skill-tool path: frontmatter not visible.
  - C2, slash-command path: frontmatter not visible.
  - C3, docs: they agree.
  - C4, control: the body marker is present.
- Implementer: sub-agent, attempt 1. Verifier: sub-agent, PASS. It re-ran C1 independently with the same result.
- Smoke check: 3 headless `claude -p` runs, exit 0.
- Deviations: none in the repository. Two wording errors in the scratch `RESULT.md` were carried correctly into 06-2: the MCP file tools were not blocked but were unused, and the C1 API blobs predate the load.

### Task #2: `metadata.version: 0.7.0` in 17 skills (Status: done)

- Delivers: AC-1, AC-4. Files: 17 × `skills/*/SKILL.md` (16 × +2, `shade-kit-report` −1/+3).
- Checks: one-off check red before the edit, green after; `quick_validate.py` 17/17; diff touches only the frontmatter.
- Implementer: sub-agent. Attempt 1 BLOCKED on pre-existing invalid YAML in `shade-kit-report`; the retry under D-20 was DONE. Verifier: sub-agent, PASS, with its own checker and mutation tests.
- Smoke check: n/a, nothing runnable (frontmatter metadata only).
- Deviations: `shade-kit-report` description now in double quotes, text byte-identical (D-20).

### Task #3: Documentation (Status: done)

- Delivers: AC-2, AC-3, AC-5.
- Files: `06-2-skill-conventions-and-version.md`, `06-skill-foundation.md`, `README.md`, `CLAUDE.md`, plus one-line consistency edits in `06-5`, `06-6`, `06-9`.
- Implementer: sub-agent. Verifier: FAIL, then a retry, then PASS.
  - The FAIL found two remaining contradictions: `06:54` (D-5) and the out-of-scope bullet at `06-2:25` (D-4).
  - It also found two optional items: the example `0.8.0`, and two `CLAUDE.md` wordings.
- Smoke check: n/a, documentation only.
- Deviations:
  - The lines in 06-5, 06-6, and 06-9 were not in the Gate-3 file list. They were added because "with the version from its own frontmatter" became wrong after the check.
  - No **FACT** label in the repo documents, because the label is not in the repo's label set.
  - There are 12 skeletons with `version --json`, not 11.

### Rework R-1 (D-21) and R-2 (D-22) (Status: done)

- Files: `CLAUDE.md`, `06-2`, `06-9`, `06-skill-foundation`, `README`, `08-copilot-and-codex-integration.md`, and line 10 of the round-1 review report.
- Verifier: PASS in both rounds.
- Deviation, confirmed with PyYAML: the quoting rule now distinguishes the two cases. An unquoted `: ` makes the frontmatter invalid YAML; an unquoted ` #` silently cuts the description off.

## 7. Review

| Round | Report | Critical | Major | Minor | Info | Rework done | Selected | Deferred |
|---|---|---|---|---|---|---|---|---|
| 1 | `docs/reviews/2026-11-06-feature-feature-06-uncommitted.md` | 0 | 0 | 6 | 6 | — | R06-2-01 to R06-2-10, local path | R06-2-11, R06-2-12 (D-21) |
| 2 | `docs/reviews/2026-11-06-feature-feature-06-uncommitted-round-2.md` | 0 | 0 | 1 | 5 | R-1 | R06-2-13, -15, -17 | R06-2-14, -16, -18 (D-22) |
| 3 | `docs/reviews/2026-11-06-feature-feature-06-uncommitted-round-3.md` | 0 | 0 | 0 | 1 | R-2 | — | R06-2-19 (D-23) |

Part B conformance:
- Round 1: 4 proven (executed), 1 read-checked, 0 unproven; 0 departures from decisions; 0 scope findings.
- Rounds 2 and 3: no AC verdict changed. 17/17 valid; 17 × `'0.7.0'`.

Reviewer: a sub-agent with the `code-review` skill (D-17) and skill-creator.

Repeated findings:
- R06-2-13 is R06-2-06 in a new form: the log row was not updated after the rework.
- R06-2-14 is the second, unaddressed half of R06-2-07.
- In round 3, `code-review` raised R06-2-03 and R06-2-11/-18 again. They were not counted again.

## 8. Verify Before Committing

- `skills/*/SKILL.md`: value `0.7.0` (D-2, against the 0.8.0 recommendation). If the release that ships this is tagged `v0.8.0`, raise all 17 values together (R06-2-18, 06-2:94). Nothing breaks today, because no skill calls `--expect` yet.
- `skills/shade-kit-report/SKILL.md:3`: the description is now in double quotes. This fixes a real loading defect: before, Claude Code loaded the skill with empty metadata.
- `CLAUDE.md` §Skill conventions: new section (D-6). Check whether it states the rules the way you want them.
- `docs/feature-packages/06-5…`, `06-6…`, `06-9…`, `08…`: their status lines do not record the 2026-11-06 changes (R06-2-16, accepted).
- `docs/reviews/`: three new review reports (untracked). Decide whether they go into the commit; earlier reports were committed and removed later.
- The visibility result holds for Claude Code 2.1.318 and project-level skills only. The plugin and user path (package 07) was not checked (R06-2-14).

## 9. Open Items

- Deferred findings:
  - R06-2-11: no automated guard of the 17 version copies; by design (D-13), for package 19.
  - R06-2-12: how Copilot and Codex parse the unquoted value; an open point in 08 now covers it.
  - R06-2-14: re-checking the plugin path has no owner, for example as an open point in 07.
  - R06-2-16: the status lines of 06-5, 06-6, 06-9, and 08.
  - R06-2-18: whether `0.7.0` fits the next release tag.
  - R06-2-19: the README status line does not mention the code review of 06-2.
- Pre-existing, outside scope: `research/shade-kit-phase-start-analysis.md` is linked from 14 files under `docs/` but does not exist.
- Outside scope, from `code-review` on the committed 06-1 code. These were not checked; details are in the review reports:
  - the `EngineVersionProvider.cs:39` fallback `1.0.0.0` is not SemVer, which ends in exit 255;
  - a GitVersion label with a leading-zero numeric part also ends in exit 255;
  - `ExpectedVersionOptionConverter.cs:33` reports a value starting with `-` as "got none";
  - minor items in `CliErrorMapper.cs`, `EngineVersionCheckResult.cs`, `SemanticVersion.cs`, and `VersionCommand.cs`.
- Blocked tasks: none. Skills that could not be loaded: none.

## 10. Final Verification

- Build: n/a. No .NET code was changed (`git status`: only `skills/*/SKILL.md` and Markdown).
- Full suite: n/a. There are no automated tests of the skill texts (06-2 rule, D-13).
- One-off checks (main agent, final):
  - `quick_validate.py` gives 17/17 "Skill is valid!";
  - `grep "^  version:" skills/*/SKILL.md` gives 17 × `0.7.0`;
  - `--expect` appears in 0 skills.
- Smoke check: n/a, nothing runnable changed.
- Files changed: 26 entries in `git status --porcelain`.
  - 25 tracked files, +93/−23: 17 skills, 7 package documents, and `CLAUDE.md`.
  - The untracked folder `docs/reviews/`, which holds 3 reports.
  - This report, also untracked.

## 11. Skill-Invocation Log

Task #1: Frontmatter visibility check
  [x] skill-creator:skill-creator — invoked before the first write of the canary `SKILL.md` (implementer turn 1; verifier loaded it as well)

Task #2: `metadata.version` in 17 skills
  [x] skill-creator:skill-creator — invoked before the first write of `skills/*/SKILL.md` (implementer turn 1; verifier loaded it as well)

Task #3: Documentation
  [x] skill-creator:skill-creator — invoked before the first write of `06-2-skill-conventions-and-version.md` (implementer turn 1; verifier loaded it as well)

R-1: Review round 1 fixes (R06-2-01 to R06-2-10)
  [x] skill-creator:skill-creator — invoked before the first write of `CLAUDE.md` (rework implementer turn 1; verifier loaded it as well)

R-2: Review round 2 fixes (R06-2-13, -15, -17)
  [x] skill-creator:skill-creator — invoked in the same rework agent context before its first write (R-1); no further skill mapped

Review (Phase 5, rounds 1 to 3)
  [x] code-review — invoked by the review sub-agent before reading the diff (effort `high`)
  [x] skill-creator:skill-creator — invoked by the review sub-agent before reading the diff
