> **Date:** 2026-11-08
> **Requirement:** `docs/feature-packages/06-4-gate-check-skill.md` (package 06-4, checklist check `shade-kit-gate-check` and `shade-kit gate info`)
> **Branch:** feature/feature-06-04
> **Mode:** sub-agents (sequential; scenario runners in parallel)
> **Status:** complete — rework loop ended by the user after review round 4 (D-42); open items in section 9

# Checklist check skill and `shade-kit gate info` — implementation and four review rounds

## 1. Summary

Package 06-4 was implemented on another machine (tasks #1–#3, committed by the user as `3df0abd` together with the
round-1 review report and its hand-over section). This session continued at Gate 5 of round 1. It carried out five
rework tasks (RW-1 to RW-5) and three further review rounds (rounds 2 to 4). It added 9 scenario runs in fresh
sub-agents (6 on the rework-1 skill text, 3 re-runs on the rework-2 text) and corrected the documentation of the
scenario evidence. The C# part stays small: `gate info` lost its undecided `-f` alias, a duplicate record was
removed, and test traits were split. Most of the rework went into the skill text, the DoD template and the feature
package documents. All 8 tasks are done (3 implementation, 5 rework). Round 4 has 0 Critical, 0 Major and 0 Minor
findings. **Nothing of this session is committed.** Review the working tree with `git diff` and commit what you
accept.

## 2. Preconditions

- Working tree at the start of this session: clean, branch `feature/feature-06-04` = `origin/feature/feature-06-04`
  at `3df0abd` (the implementation and the round-1 report were committed by the user to move the work between
  machines). The original run started on `main` with a clean tree (round-1 D-1).
- Scenario evidence of the original run (s1–s8, R-A/R-B/R-C) existed only in the other machine's temporary folder;
  it was not available here (**FACT**: the folder does not exist on this machine). The new scenarios were rebuilt
  from the hand-over recipe.
- Runtime skill list: available. `agent-skills:code-review-and-quality` (review set D-31) is not installed on this
  machine, so the user chose a replacement (D-38).
- Baseline on this machine before any change (**FACT**): `dotnet build` 0 warnings; `dotnet test` 4721 total, 4720
  passed, 0 failed, 1 skipped (`PathExecutableSearchTests.HasUnixExecuteBit_OnWindows_ReturnsTrue`, platform skip).
- Size note: n/a — the work was not small (three tasks, new CLI command, more than five files).

## 3. Requirement and Acceptance Criteria

Package 06-4 adds the general skill `shade-kit-gate-check`, which checks the feature's copy of a gate checklist
(Definition of Ready, Definition of Done, team checklists) against the feature artifacts, read-only git and the
facts of the new read-only CLI command `shade-kit gate info <phase> [--feature <id>] --json`. It writes Result and
Evidence of every item and one "Check runs" line, and returns pass, fail or error to the calling skill. It never
approves.

The AC list is the round-1 list (AC-1 to AC-18; mapping to FP06-4-AC1…AC7 in the round-1 report, B1). Verdicts as
of round 4 (rounds 2–4 updated AC-14, AC-15 and AC-17, and only read-checked the rest):

| AC | Text (short) | Origin | Tests / scenarios | Result | Verdict |
|---|---|---|---|---|---|
| AC-1 (FP06-4-AC1) | n/a with reason for an artifact the profile lacks | explicit | s2, R-C | pass | proven (executed: n/a rule deleted → R-C red) |
| AC-2 (FP06-4-AC2) | every item pass/fail/n/a + Evidence, one more run line | explicit | s1–s4, s7, s8, s9, s10a, s10b, s9r, s10ar; check_run.py | pass | read-checked — not executed |
| AC-3 (FP06-4-AC3) | no other file changed, no `phase` command | explicit | all scenarios | pass | read-checked — not executed |
| AC-4 | return content (D-22) | implicit | s2, s7, s8, R-A, s9, s9r | pass | read-checked — not executed |
| AC-5 | guidance comments kept | implicit | all writing scenarios | pass | read-checked — not executed |
| AC-6 | missing artifact in a profile that writes it → fail | implicit | s4, s3 | pass | read-checked — not executed |
| AC-7 | team items checked | implicit | s7 | pass | read-checked — not executed |
| AC-8 | overall pass iff no fail | implicit | s2, s4, s8, R-A, R-C, s9–s10b | pass | read-checked — not executed |
| AC-9 | no docs/research path, frontmatter 0.7.0, no handshake | explicit (06-2) | grep, quick_validate 18/18 | pass | read-checked — not executed |
| AC-10 | no build/test/lint/coverage run | explicit | all logs | pass | read-checked — not executed |
| AC-11 (FP06-4-AC4) | `gate info` content | explicit | GateInfoReaderTests, GateInfoCommandTests, GateCommandsEndToEndTests | pass | proven (executed: round-1 mutations MA, MB, MC) |
| AC-12 (FP06-4-AC4) | `gate info` changes no file | explicit | `ReadAsync_AnyFeature_ChangesNoFile`, `GateInfo_Json_ChangesNoFileInKitStore` | pass | proven (executed: MD) |
| AC-13 (FP06-4-AC7) | DOD-02 from `gate info`, never `config.json` | explicit | s8, R-A, R-B | pass | proven (executed: R-B) |
| AC-14 (FP06-4-AC5) | `--feature` or current; 31/32/37 | explicit | `ExecuteAsync_JsonFeatureResolutionFailure_WritesErrorEnvelopeWithMappedCode` (3 rows), reader + E2E tests | pass | proven (executed: ME; traits split in RW-1) |
| AC-15 (FP06-4-AC6) | first phase → `gate: null`; not in profile → 41; done answered | explicit | `ExecuteAsync_JsonPhaseNotInProfile_WritesErrorEnvelopeWithPhaseNotInProfile`, null-gate tests, E2E | pass | proven (executed: MF, MG, MH) |
| AC-16 (FP06-4-AC7) | undecidable items asked after all items | explicit | s7, s10b | pass | read-checked — not executed |
| AC-17 (FP06-4-AC7) | `gate info` failure / unusable copy → `error`, no file changed | explicit | e1, e2, e3, e3r (fresh sub-agents); s5, s6 (implementing agent, iteration 2) | pass | read-checked — not executed (was unproven in round 1) |
| AC-18 | run line with local time + UTC offset; template examples | explicit | all writing runs | pass | read-checked — not executed |

**Count:** 6 proven (executed) / 12 read-checked — not executed / 0 unproven. Separately: the D-37 rule is proven
(executed) in round 2 (rule mutated → s10a went red in a fresh sub-agent). The D-39/D-40/D-41 wordings are
read-checked only: three of them (m2, n1, D-40) are not discriminated by the fixtures, and the three D-41
wordings have no scenario run.

## 4. Decisions

D-1 to D-33 are the decisions of the original run, recorded in full in the hand-over section of
`docs/reviews/2026-11-08-feature-feature-06-04-uncommitted.md` ("Decision table"). They are summarised here so
that this file stands alone.

| ID | Point | Decision | Origin | Round |
|---|---|---|---|---|
| D-1 | working tree | clean at start, branch `main` (original run) | n/a | — |
| D-2/D-6 | mode of the run | first ordinary request, then the full `implement-direct` workflow | user | — |
| D-3 | DOD-02 threshold vs. "skills never read config.json" | a CLI command that delivers what the gate check needs | user | 1 |
| D-4 | change set for DOD-06/08 | read-only git; base = merge-base with the default branch plus uncommitted; no base → fail | recommended | 1 |
| D-5/D-20 | skill input | featureId, target phase, checklist path from `phase start --json` | recommended | 1 |
| D-7 | command | `shade-kit gate info <phase>`, new group `gate` | recommended | 1 |
| D-8/D-10 | content | feature id, profile, folder, phases with state, gate, coverageThreshold | user | 1 |
| D-9 | feature selection | `--feature <id>`, otherwise the current feature | user | 1 |
| D-11 | data, not instructions | only `request.md` | user | 1 |
| D-12/D-26 | undecidable items | ask the human after all items; no answer → fail | user + recommended | 1 |
| D-13 | core location | service in `Core/Gates/` | recommended | 1 |
| D-14 | documentation | 06-4, reference in 05, CLAUDE.md | recommended | 1 |
| D-15 | skill files | single `SKILL.md` | recommended | 1 |
| D-16 | loading | `IFeatureStore` | recommended | 1 |
| D-17 | gate semantics | configured forward gate, independent of the phase state | recommended | 1 |
| D-18 | names | `IGateInfoReader`/`GateInfoReader`/`GateInfoResult`; `GateCommandGroup`, `GateInfoCommand…` | recommended + convention | 1 |
| D-19 | JSON | `{ featureId, profile, featureDir, phases:[{name,state}], gate:{…}\|null, coverageThreshold }` | user | 1 |
| D-21/D-29 | run line | `- YYYY-MM-DD HH:MM ±HH:MM – pass` / `– fail (IDs)`; template examples | user + recommended | 1 |
| D-22 | return | result, failed items, n/a items, run line, warnings | recommended | 1 |
| D-23 | first phase | `gate: null` | recommended | 1 |
| D-24 | done feature | answered | recommended | 1 |
| D-25 | unusable copy / `gate info` failure | no file changed, `error` | recommended | 1 |
| D-27 | C# tests | Core + command + end to end, `[Trait("Requirement", …)]` | recommended + convention | 1 |
| D-28 | skill proof | scenario runs outside the repository | recommended | 1 |
| D-30 | implementation skills | skill-creator, dotnet-fundamentals, dotnet-tester, dotnet-xmldocs | user | 1 |
| D-31 | review set | dotnet-reviewer + agent-skills:code-review-and-quality | recommended | 1 |
| D-32 | implementation mode | sub-agents, sequential | recommended | 1 |
| D-33 | Gate 5 (round 1, other machine) | stop; record the hand-over | user | — |
| P7 / P8 | persistence / dependencies | read-only, no format change / no new package | pre-answered / n/a | 1 |
| D-34 | R-1 (Major) | remove `-f`; pin "no short name" | recommended accepted | Gate 5 r1 |
| D-35 | R-2 | only the 3 new `error` scenarios, no re-run of s5/s6 | user's own | Gate 5 r1 |
| D-36 | round-1 ≤ Minor | fix M1–M6, S1–S3, N1; defer N2 | user's own | Gate 5 r1 |
| D-37 | S3 rule (raised by the RW-2 verifier) | `git status --porcelain --branch`; with no own commits, ask only on the default branch or detached HEAD | recommended accepted | rework 1 |
| D-38 | review set on this machine | dotnet-reviewer + skill-creator as yardstick (code-review-and-quality not installed) | recommended accepted | Gate 5 r2 |
| D-39 | round-2 findings | fix m1–m4, s2, n1, n2, n3; defer s1 | user's own | Gate 5 r2 |
| D-40 | git form (part of m1) | `git status --porcelain --branch --untracked-files=all` | user's own (via m1) | Gate 5 r2 |
| D-41 | round-3 findings | fix all six (r3-m1, r3-s1, r3-s2, r3-n1, r3-n2, r3-n3) | user's own | Gate 5 r3 |
| D-42 | round 4 | accept; r4-s1 and r4-n1–n3 deferred, named as open points here | recommended accepted | Gate 5 r4 |
| D-43 | simplify review (2026-11-10) | apply A–L of the four-angle cleanup review: `gate info` moves into `IPhaseWorkflow` with `IPhaseEngine.HasGate` and `TransitionGate.ForPhase`; phases carry their artifacts (`PhaseArtifact`, `onDemand`) from `BuiltInWorkflowProfiles`; one `phases` array for `feature show` and `gate info`; `gate info` keeps the configured approval while a transition is pending; shared E2E harness and test helpers; skill: n/a rule on the artifacts, `git log --oneline`, multi-path diff, one edit per row, every rule once | user's own (each finding chosen) | simplify |

Waivers: none.

## 5. Skills

**Runtime discovery (this session):** the skill list included the cc-ai-dotnet set (dotnet-fundamentals,
dotnet-tester, dotnet-xmldocs, dotnet-reviewer, …) and skill-creator. `agent-skills:code-review-and-quality` was
not in the list.

| Skill / agent | Category | Proposed use | Outcome |
|---|---|---|---|
| skill-creator:skill-creator | knowledge (skill text) | implementation, review yardstick | kept (D-30, D-38) |
| cc-ai-dotnet:dotnet-fundamentals | baseline knowledge | C# tasks | kept (D-30) |
| cc-ai-dotnet:dotnet-tester | tester | C# tests, template pin | kept (D-30) |
| cc-ai-dotnet:dotnet-xmldocs | documentation | XML docs | kept (D-30) |
| cc-ai-dotnet:dotnet-reviewer | reviewer | Phase 5, all rounds | used |
| agent-skills:code-review-and-quality | reviewer | Phase 5 | not installed here — replaced by skill-creator as yardstick (D-38) |

**Mapping and loads in this session:**

| Task | Mapped skills | Loaded by implementer | Confirmed by verifier |
|---|---|---|---|
| RW-1 | dotnet-fundamentals, dotnet-tester, dotnet-xmldocs | all three, before the first write (claim) | V1: claim only; verifier loaded the same three |
| RW-2 | skill-creator, dotnet-tester | both, before the first write (claim) | V1: claim only; verifier loaded both |
| RW-3 | skill-creator (as announced) | none — see section 11 | — |
| RW-4 | skill-creator, dotnet-tester | both, before the first write (claim) | V1: claim only; verifier loaded both |
| RW-5 | skill-creator | before the first write (claim) | combined verifier/reviewer loaded skill-creator + dotnet-reviewer |

## 6. Tasks

Tasks #1–#3 (original run, other machine; details in the round-1 report, B2 and hand-over):

### Task #1 — Core `GateInfoReader` (Status: done)
- Delivers AC-11, AC-12, AC-14, AC-15. Files: `src/CreativeCoders.ShadeKit.Core/Gates/IGateInfoReader.cs`, `GateInfoReader.cs`, `GateInfoResult.cs`, `GateInfoGate.cs`, DI registration; 36 reader tests. Verifier PASS (other machine).

### Task #2 — CLI `gate info` (Status: done)
- Delivers AC-11, AC-14, AC-15. Files: `src/CreativeCoders.ShadeKit.Cli/Commands/Gate/**`, 46 tests, CLAUDE.md. Verifier PASS; smoke-check exit 0/10/31/32/37/41 (other machine).

### Task #3 — Skill, templates, docs (Status: done)
- Delivers AC-1–AC-10, AC-13, AC-16–AC-18. Files: `skills/shade-kit-gate-check/SKILL.md`, both checklist templates, 06-4, 05. Verifier PASS (other machine).

### RW-1 — `gate info` CLI cleanup: R-1, M6, S1 (Status: done)
- Files: `GateInfoCommandOptions.cs` (no `-f`), `GateInfoCommandData.cs` (uses `PhaseSnapshotData`), `GateInfoCommandPhaseData.cs` (deleted), `Commands/Phase/PhaseSnapshotData.cs` (XML docs for both uses), `GateInfoCommandTests.cs` (attribute test pins `ShortName == null`; theory split into AC5 / AC6 / no-trait).
- Tests: 4721 total, unchanged (10 rows → 3 + 1 + 6). The attribute test went red first ("found f").
- Implementer: sub-agent. Verifier: sub-agent — PASS. Mutations in a scratch copy: `'f'` restored → attribute test red; `[JsonPropertyName]` on `PhaseSnapshotData.Name` → 4 JSON tests red.
- Smoke-check (both agents): `gate info refine --json` exit 0; `--feature x` exit 31; `--help` lists only `--feature`; `-f x` is silently ignored (pre-existing parser behaviour for every command, see section 9).
- Deviations: none.

### RW-2 — skill text, DoD template, package docs: M1–M5, S2, S3 (+D-37), N1 (Status: done)
- Files: `SKILL.md`, `definition-of-done.template.md` (DOD-02 without `config.json`), `ShippedTemplatesTests.cs` (pin, red first), `05`, `06-4` (functional sections), `README.md`, `06-skill-foundation.md`.
- Implementer: sub-agent, plus a follow-up for D-37. Verifier: sub-agent — PASS, then PASS on the D-37 delta (template revert in a copy → pin red; `--branch` parsing edge cases checked with real git).
- Smoke-check: `init` writes the new DOD-02 text (both agents).
- Deviations: a README consistency-log row added (later corrected for accuracy in RW-3, RW-4b, RW-5).

### RW-3 — scenario evidence for R-2 and the RW-2 rules (Status: done)
- Scratchpad: 6 fixtures (e1, e2, e3, s9, s10a, s10b), expected results, `check_run.py` with self-test. 6 runner sub-agents, none of which saw the expected results. Repo: 06-4 AC section (per-scenario table: text, runner, result), README row.
- Results: all 6 as expected; checker without problems.
- Verification: the scenario results were checked by script; the main agent read-checked the doc update. No separate verifier sub-agent, but the review rounds 2 and 3 covered it.
- Deviations: in 4 of 6 runs the runners used shell listings outside the rule. This led to round-2 m1 and round-3 r3-s2.

### RW-4 — round-2 fixes: m1–m4, s2, n1, n2, n3, D-40 (Status: done)
- Files: `SKILL.md`, `06-4`, `README.md`, `GateInfoCommandTests.cs` (helper renamed to `RunWithReaderFailureAndAssertEnvelopeAsync`).
- Verifier: sub-agent — PASS.
- RW-4b (doc update after the 3 re-runs e3r, s9r, s10ar on the rework-2 text): the fixture builder made the update, the main agent corrected one sentence in the README (the test run did cover the rename), and the round-3 review checked both.

### RW-5 — round-3 fixes: r3-m1, r3-s1, r3-s2, r3-n1, r3-n2, r3-n3 (Status: done)
- Files: `SKILL.md` (step 3 lists the folder with file tools, step 6 edits in place, closed shell list), `06-4`, `README.md`. Scratchpad: expectations for the re-runs rewritten and the logs checked against them (`rerun-check.md`: e3r 14/14, s9r 23/24, s10ar 21/22; each miss is the runner's own `find`/`file`); `RUNNER.md` tightened.
- The main agent corrected "two" to "three code review reworks", an error in its own D-41 wording.
- Verification and review round 4 ran in one independent sub-agent (deviation from the workflow: no separate verifier). Verdict PASS, V1–V5.

## 7. Review

| Round | Report | Critical | Major | Minor | Sugg. | Nit. | Rework done | ≤ Minor selected | Deferred |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `docs/reviews/2026-11-08-feature-feature-06-04-uncommitted.md` | 0 | 1 | 8 | 3 | 2 | R-1, R-2 (D-34, D-35) | M1–M6, S1–S3, N1 (D-36) | N2 |
| 2 | `docs/reviews/2026-11-08-feature-feature-06-04-uncommitted-round2.md` | 0 | 0 | 4 | 2 | 3 | — | m1–m4, s2, n1, n2, n3 (D-39, D-40) | s1 |
| 3 | `docs/reviews/2026-11-08-feature-feature-06-04-uncommitted-round3.md` | 0 | 0 | 1 | 2 | 3 | — | all six (D-41) | — |
| 4 | `docs/reviews/2026-11-08-feature-feature-06-04-uncommitted-round4.md` | 0 | 0 | 0 | 1 | 3 | — | none (D-42) | r4-s1, r4-n1, r4-n2, r4-n3 |

Conformance lines: round 1: 6 proven / 11 read-checked / 1 unproven (of 18). Round 2: 6 proven / 12 read-checked /
0 unproven, D-37 proven (executed). Round 3: unchanged over the 18 ACs, and the 6 changed rules read-checked only.
Round 4: verifier V1–V5 PASS, and no AC verdict changed.

Reviewer: cc-ai-dotnet:dotnet-reviewer in all rounds. The second reviewer was agent-skills:code-review-and-quality
in round 1 (other machine) and skill-creator as the yardstick in rounds 2–4 (D-38). Repeated findings: none as a
text defect. The runners' shell listings were raised in round 2 (m1) and again in round 3 (r3-s2), and are fixed
in the text since D-41.

## 8. Verify Before Committing

- `skills/shade-kit-gate-check/SKILL.md`: read the whole Change set section and the Rules. This text changed in
  five passes. The D-41 wordings (step 3 listing with file tools, step 6 edit in place, the closed shell list)
  have no scenario run.
- `docs/feature-packages/06-4-gate-check-skill.md`: the many **Decided … ; the wording is Proposed** labels and
  the long acceptance-criteria section with the scenario table. Check that you agree with the labels.
- `docs/feature-packages/README.md:177`: the consistency-log row is long. Its claims were checked against the
  logs (round 3, round 4).
- `GateInfoCommandOptions.cs`: `-f` is gone. Anyone who already typed `gate info <phase> -f <id>` now silently
  gets the current feature (section 9).
- `src/CreativeCoders.ShadeKit.Core/Templates/Resources/checks/definition-of-done.template.md:18`: the DOD-02 text
  changed. Repositories initialised earlier keep their old copy (`init` writes templates; the upgrade path is
  package `upgrade` *(planned)*).
- D-37/D-40 behaviour: on a feature branch without own commits, the skill decides DOD-06/08 from the uncommitted
  changes; on the default branch it asks. s1 (fail without asking when the visible change already proves it) was
  deferred.

## 9. Open Items

- Deferred findings (user decisions): N2 (round 1, D-36): question options in step 5. s1 (round 2, D-39). r4-s1
  (D-42): an agent that can change files only through the shell has no allowed way to write the copy (whether
  this applies to Codex CLI is **unverified**). r4-n1: the `find` exclusion should also cover
  `-execdir`/`-ok`/`-okdir` and GNU `-fprint`/`-fls`. r4-n2: the closed shell list is POSIX-only (PowerShell
  aliases **unverified**). r4-n3: 06-4:204 (AC3) still describes the shell list as an open class.
- Evidence gaps (recorded in 06-4): there is no fixture with a new untracked folder for D-40, no CRLF fixture for
  "keep the line endings" (06-4 open point), and no scenario run on the D-41 wordings. s5 and s6 were not re-run
  by a fresh sub-agent (D-35).
- Outside the package: the CLI parser (CreativeCoders.SysConsole.Cli.Parsing) silently ignores unknown options
  for every command (pre-existing; README 06-1 row). `plan.template.md:81`, `verification.template.md:8` and
  `14-verify.md:42` name `config.json` for `coverageThreshold` (06-4 open point for package 14).
- Follow-ups for 06-5 (from the hand-over): pass featureId, `gate.phase` and `gate.checklist` from
  `phase start`, and decide what `error` means when approval is off (06-4 open point).
- The **Proposed** defaults in 06-4 still wait for a decision (threshold unit, `.shadekit/` excluded, empty change
  set → ask, return shapes).
- The scenario fixtures, logs and checker exist only in this session's scratchpad
  (`/private/tmp/claude-501/-Users-chris-src-github-shade-kit/76183cc5-0b41-4846-ae21-ce48da486e97/scratchpad/rw3/`,
  `rw3-expected/`, `rw3-tools/`), which the system may delete.
- Harness note: a context-mode hook rewrites every Agent call, so each sub-agent start needed a second identical
  call before auto mode accepted it.

## 10. Final Verification

- Build: `dotnet build` → 0 warnings, 0 errors (last C# change: the RW-4 rename; run after it by the RW-4 verifier
  and by the main agent).
- Full suite: `dotnet test` → 4721 total, 4720 passed, 0 failed, 1 skipped (pre-existing platform skip).
- Skill frontmatter: `quick_validate.py` → 18 of 18 valid (round-4 reviewer, after RW-5).
- Smoke-check: RW-1/RW-2 `gate info` and `init` in scratch repositories (section 6). RW-3 to RW-5 changed no
  runnable code.
- Files changed: 16 in `git status --porcelain` (12 tracked: 11 modified, 1 deleted; 4 untracked: the review
  reports of rounds 2–4 and this report).

## 11. Skill-Invocation Log

Task #1 — Core `GateInfoReader` (other machine; from the round-1 hand-over)
  [x] dotnet-fundamentals, [x] dotnet-tester, [x] dotnet-xmldocs — implementer turns 1–3, before the first write; accepted by the verifier
Task #2 — CLI `gate info` (other machine)
  [x] dotnet-fundamentals, [x] dotnet-tester, [x] dotnet-xmldocs — turns 1–3, before the first write
Task #3 — Skill + templates + docs (other machine)
  [x] skill-creator:skill-creator — first tool call, before the first write (checked by the verifier in the implementer transcript)
RW-1 — R-1, M6, S1
  [x] cc-ai-dotnet:dotnet-fundamentals, [x] cc-ai-dotnet:dotnet-tester, [x] cc-ai-dotnet:dotnet-xmldocs — implementer turn 5, before the first write of `GateInfoCommandTests.cs` (implementer report; verifier V1: claim only, no transcript access)
RW-2 — M1–M5, S2, S3/D-37, N1
  [x] skill-creator:skill-creator, [x] cc-ai-dotnet:dotnet-tester — before the first write (the `ShippedTemplatesTests.cs` pin) (implementer report; verifier V1: claim only)
RW-3 — scenario evidence and 06-4 AC section
  [n/a] skill-creator:skill-creator — no skill text was changed in RW-3. The task wrote scratchpad fixtures and the feature-package documents 06-4 and README; `SKILL.md` was untouched, which the RW-4 verifier and the round-3 reviewer confirmed by diff. Skill-creator governs skill texts. The skill table I announced at the start of the rework listed it for RW-3, which was a mapping error.
RW-4 — m1–m4, s2, n1, n2, n3, D-40
  [x] skill-creator:skill-creator, [x] cc-ai-dotnet:dotnet-tester — before the first write (implementer report; verifier V1: claim only)
RW-5 — r3-m1, r3-s1, r3-s2, r3-n1, r3-n2, r3-n3
  [x] skill-creator:skill-creator — before the first write (implementer report; the round-4 verifier/reviewer loaded it and dotnet-reviewer itself)
Review rounds 2–4
  [x] cc-ai-dotnet:dotnet-reviewer, [x] skill-creator:skill-creator — loaded by each reviewer sub-agent (D-38)

## 12. Simplify review (2026-11-10, D-43)

A `/simplify` review (reuse, simplification, efficiency, altitude) of the branch against `main` found twelve points; the user chose to apply all of them:

- **Code.** `GateInfoReader`/`IGateInfoReader` are gone; `IPhaseWorkflow.GetGateInfoAsync` loads the feature through the same `LoadAsync` as the phase commands (with an optional feature id; `EnsureFeatureExists` moved to `KitStoreFeatureExtensions`). The engine owns the gate rules: `IPhaseEngine.HasGate` (phase check, no gate into the first phase) and `TransitionGate.ForPhase` (default gate), which `PhaseEngine` uses too. `GateInfoGate` holds a `PhaseGate`; `GateInfoResult.Paths` is derived from the id. `WorkflowPhase.Artifacts` (`PhaseArtifact`, names in `WorkflowArtifactNames`) lists what each phase writes; `FeaturePhase.ListOf` builds the phases with their artifacts, and `feature show` and `gate info` write them as one `phases` array (`FeaturePhaseData`), so `gate info` now also lists stored phases the profile no longer has. While a transition into the phase is pending, `gate info` still reports the configured approval (user decision).
- **Tests.** `GateInfoReaderTests` became `PhaseWorkflowGateInfoTests` (theories, the checklist constant, `TestConfigJson` in `tests/Shared` shared with `FeatureStoreTestContext`); new `PhaseEngineGateTests` and `FeaturePhaseTests`, and artifact tests in `WorkflowPhaseTests` and `BuiltInWorkflowProfilesTests`. The feature, phase, and gate end-to-end tests share `CliEndToEndHarness`; its `AssertError` also checks `data: null`. Result: Core 3 750 tests (1 skipped on non-Windows), CLI 997 tests, all green.
- **Skill.** Changed with skill-creator; the old text was kept as a snapshot and run as the baseline. Four scenarios (quick DoD, feature DoD, bugfix DoR, `error`), each in a fresh subagent per text, checked by a script: new text 28 of 28 checks, old text 27 of 28. The scenarios, fixtures, outputs, and the review page stay outside the repository (D-28); the results are in the 06-4 package.

