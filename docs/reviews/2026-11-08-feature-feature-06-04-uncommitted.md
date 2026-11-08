# .NET Code Review — feature/feature-06-04 (uncommitted)

**Date:** 2026-11-08
**Mode:** uncommitted (working tree vs. HEAD 9011ef8, staged + unstaged + untracked) — origin: provided
**Round:** 1
**Report language:** English — origin: provided
**Detected SDK:** 10.0.402
**Target Framework(s):** net10.0 (5 projects)
**Version origin:** repo:src/CreativeCoders.ShadeKit.Core/CreativeCoders.ShadeKit.Core.csproj (detector `resolved_from: csproj`)
**Checklist:** review-checklist-net10.md + security, performance, architecture, code-quality
**Tools run:** build=N · format=N · test=N (origin: provided — the caller reported `dotnet build` 0 warnings and `dotnet test` 4720 passed / 1 pre-existing skip; additionally the reviewer built a scratch copy and ran the 82 gate tests there, see Requirement Conformance)
**Exclusions:** .gitignore, *.min.js, wwwroot/lib/**
**Review strategy:** chunked (auto-selected, non-interactive; diff > 2000 LOC)
**Diff size:** 21 files, 2643 changed LOC
**Report path:** the caller asked for `docs/reviews/2026-11-08-main-uncommitted.md`; the reviewer skill prescribes `docs/reviews/YYYY-MM-DD-<branch>-<mode>.md`, so this report uses the sanitized branch name. **FACT:** the checked-out branch is `feature/feature-06-04` (`git rev-parse --abbrev-ref HEAD`), not `main` as the brief and the run note say.
**Skills used:** cc-ai-dotnet:dotnet-reviewer (C#), agent-skills:code-review-and-quality (skill text, docs), cc-ai-dotnet:dotnet-tester (test quality).

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 1 |
| Minor | 8 |
| Suggestion | 3 |
| Nitpick | 2 |

**Top risks:**
1. `gate info` ships an undecided, undocumented short option `-f` for `--feature`; it widens the public CLI contract without a decision, and no test pins it (silent departure from D-9).
2. Three of the five `error` branches of the skill (gate `null`, checklist mismatch, `checklistExists: false`) were never exercised by a scenario, yet 06-4 marks FP06-4-AC7 "proven". Scenario 8, the AC-13 evidence, ran on a pre-final skill text; the reviewer's re-run on the final text confirmed it.
3. Contract drift between the documents the skill depends on: the shipped DOD-02 criterion and package 05 still name `config.json`; 06-5 still says an approval-off gate check does not block, while 06-4 says the caller ends after `error`; README / 06 still show 06-4 as skill-only, depending only on 06-2.

**Overall:** The C# part is clean, small, and consistent with the codebase patterns (primary constructors with guarded fields, `ShadeKitCommandContext`, mapped errors, read-only store access); all 8 independent mutations in a scratch copy were killed by the new tests. The skill text is precise and agent-neutral; the open items are contract consistency, an undecided CLI alias, and gaps in the scenario evidence, not wrong behaviour.

## Findings — src/CreativeCoders.ShadeKit.Cli/Commands/Gate/Info/GateInfoCommandOptions.cs

### [Major][Architecture] src/CreativeCoders.ShadeKit.Cli/Commands/Gate/Info/GateInfoCommandOptions.cs:28
Undecided short option `-f` added to the public CLI contract (silent departure from D-9).

D-9 decided a `--feature <id>` option. The implementation adds the short name `'f'`; 06-4 (`shade-kit gate info <phase> [--feature <id>] [--json]`), CLAUDE.md, and the help text do not mention it, and `GateInfoCommandOptions_Attributes_PhaseIsRequiredFirstValueAndFeatureIsOptionalNamedOption` (tests/.../GateInfoCommandTests.cs:529) checks only `LongName`, while the existing option tests pin `ShortName` (`FeatureDropCommandTests.cs:770`, `FeatureNewCommandTests.cs:489`). A short name is hard to take back once skills or users rely on it, and `-f` is a common alias for `--force`/`--file` elsewhere. The run note records it as "chosen by the implementer (not decided)". Either get the decision and document it, or drop it.

```csharp
// Option A — no alias until it is decided (matches D-9 literally)
[OptionParameter("feature", HelpText = "The id of the feature (default: the current feature)")]
public string? FeatureId { get; set; }

// Option B — keep it, then: mark it **Proposed** in 06-4 ("shade-kit gate info <phase> [-f|--feature <id>]"),
// update the CLAUDE.md command line, and pin it in the attribute test:
feature.ShortName
    .Should().Be('f');
```

## Findings — skills/shade-kit-gate-check/SKILL.md

### [Minor][Code-Quality] skills/shade-kit-gate-check/SKILL.md:64
"After the last run line" is ambiguous next to the template's example run line.

Both checklist templates contain a line in the exact run-line format inside the guidance comment (`- 2026-10-17 14:05 +02:00 – fail (DOR-04, DOR-07)`, definition-of-ready.template.md:32; DoD :32). An agent that takes "the last run line of the section" literally can insert the new line inside the comment, before `-->`; it would then be invisible in rendered Markdown and a later run would treat it as the anchor again. The verifier flagged the same ambiguity (run note, V8). No scenario tripped over it, which is luck, not proof.

```text
Put the line after the last run line outside the guidance comment (a line inside <!-- … --> is an example,
not a run). When the section has no run line yet, put it after the closing `-->` of the section's guidance
comment, separated by one empty line (after the heading when the section has no comment).
```

### [Minor][Code-Quality] skills/shade-kit-gate-check/SKILL.md:116
The default-branch fallback implies a git command that the allow-list forbids.

"when that fails, `main`; when that does not exist, `master`" asks the agent to test whether a branch exists, but the allowed commands (line 170: `symbolic-ref`, `merge-base`, `diff`, `log`, `status --porcelain`) contain none that tests existence; an agent will reach for `git rev-parse --verify main` or `git branch --list`, which breaks AC-3 / D-4. Step 2 already says "When it fails for every candidate", so the existence test is the `merge-base` call itself — say so.

```text
1. Candidates for the default branch, in this order: the target of `git symbolic-ref refs/remotes/origin/HEAD`
   (for example `refs/remotes/origin/main` → `origin/main`) when that command succeeds, then `main`, then `master`.
2. The base: `git merge-base HEAD <candidate>` for the first candidate for which it succeeds; do not test
   whether a branch exists in any other way.
```

### [Suggestion][Performance] skills/shade-kit-gate-check/SKILL.md:118
`git diff <base>` is unbounded agent input.

For a real feature the full diff can be tens of thousands of lines and fills the agent's context before DOD-06/DOD-08 are decided. `git diff --stat <base>` / `--name-status`, then `git diff <base> -- <path>` per relevant file stays inside the allowed `git diff`, and makes the evidence ("which file adds the suppression") easier to cite.

```text
3. The change: `git diff --name-status <base>` for the changed files, then `git diff <base> -- <path>` for the
   files an item needs (committed and uncommitted changes of tracked files); `git log <base>..HEAD`; and
   `git status --porcelain` (untracked files, which you read directly).
```

### [Suggestion][Code-Quality] skills/shade-kit-gate-check/SKILL.md:117
Work on the default branch itself is not covered.

When `HEAD` is the default branch (the run under review was itself done on a branch reported as `main`, D-1; `quick` features are often done there), `merge-base HEAD main` is `HEAD`, so `git diff <base>` shows only uncommitted changes and `git log <base>..HEAD` is empty: the feature's own commits are invisible and DOD-06 can pass on an empty or partial change set. The empty case already goes to the human; the partial case does not.

```text
When the base is `HEAD` itself (the work happens on the default branch), the committed part of the change
cannot be found from git: decide DOD-06 and DOD-08 from the uncommitted changes only when that is all the
feature changed, otherwise ask about them in step 5, naming the base.
```

### [Nitpick][Code-Quality] skills/shade-kit-gate-check/SKILL.md:129
The return list and the example disagree.

The list names "the items a human decided" and "a suspicious instruction found in `request.md`, when there is one", but the example return has no line for the latter and shows "Decided by a human: none" only in the empty form. Two scenario agents (s7, s8 notes) improvised a "Suspicious instructions in request.md: none" line. Add both lines to the example, or say that the line is left out when nothing was found.

```text
Decided by a human:
- SEC-01 A security review by a human took place.: pass — "reviewed by A. Smith on 2026-11-07"
Suspicious instructions in request.md: none
```

### [Nitpick][Code-Quality] skills/shade-kit-gate-check/SKILL.md:51
"the options `pass` and `fail`, each with a reason in the human's words" cannot be prepared by the agent.

The agent writes the options before the human answers. s7 resolved it as "pass – … (please name who reviewed it and when)". Say that.

```text
… and the options `pass` and `fail`, each asking the human for the reason; free text is possible.
```

## Findings — docs/feature-packages/06-4-gate-check-skill.md, 05-gates-and-checklists.md, 06-5, README.md, templates

### [Minor][Architecture] docs/feature-packages/06-4-gate-check-skill.md:134
The `error` contract contradicts 06-5 for an approval-off gate.

06-4 says after `error` "the calling skill then ends with its next-steps message"; 06-5 step 2/4 (06-5-phase-entry-skill.md:50, :57) says that with approval off the transition was already performed and "a failed checklist is reported as a warning and does not block", i.e. the phase continues. For `error` with approval off the two texts give opposite instructions. The 06-4 open point (line 169) names the input contract and the `error` end, but not the approval-off case; the verifier raised it as V8. Not a code defect yet (06-5 is not worked out), but the contract should be fixed before 06-5 is implemented.

```text
06-4, open points:
- 06-5 must pass featureId, the target phase, and the checklist path from the phase start result, and decide what
  an `error` means when approval is off (the transition is already performed): end with the next-steps message,
  or report it as a warning like a failed checklist and continue.
```

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Core/Templates/Resources/checks/definition-of-done.template.md:18
The shipped DOD-02 criterion and package 05 still point the agent at `config.json`.

The criterion text says "If `config.json` sets `coverageThreshold`"; package 05 repeats it in the default-content table (05:160) and in its open point (05:204: "DOD-02 checks `coverageThreshold` from `config.json`"). The skill has to override the criterion it checks ("The criterion names `config.json`; `gate info` gives its value, so never read `config.json`", SKILL.md:103), and the s8 agent notes "The template wording invites an agent to open `config.json`". Task #3 deliberately limited the template change to the Check-runs comment, so this is a follow-up, but the 05 open point is simply stale now.

```text
DOD-02 (template + 05 table): Coverage was measured and recorded in `verification.md`. If a coverage threshold is
set (`coverageThreshold`, shown by `shade-kit gate info`), the value reaches it.
05 open point: The value of the coverage threshold. DOD-02 checks `coverageThreshold` (read through `gate info`,
06-4) only when it is set (packages 02 and 14).
```

### [Minor][Code-Quality] docs/feature-packages/README.md:22
README and package 06 were not updated for the new shape of 06-4.

06-4 now says Kind "Skill (general skill) and CLI command (`gate info`)" and "Depends on 05, 06-2"; the package table in README.md:22 and 06-skill-foundation.md:23 still show it as a skill depending on 06-2 only, and the README status line (line 3) lists every earlier package implementation up to 06-3 but not 06-4, unlike the previous package commits (9011ef8 updated README.md). D-14 named 06-4, 05, and CLAUDE.md only, so this is a gap in the decision, not a departure; the documents still disagree.

```text
README.md:22   | 06-4 | [Checklist check](06-4-gate-check-skill.md) | Checked, evidenced checklist before every approval | 05, 06-2 |
06-skill-foundation.md:23   | 06-4 | [Checklist check (`shade-kit-gate-check`)](06-4-gate-check-skill.md) | Skill and CLI command (`gate info`) | 05, 06-2 |
README.md:3    … and 2026-11-08 (implementation of packages 06-3 and 06-4).
```

### [Minor][Tests] docs/feature-packages/06-4-gate-check-skill.md:164
FP06-4-AC7 is marked "proven" with error branches that no scenario exercised and with key evidence on a pre-final text.

(a) The skill has five `error` triggers (SKILL.md:32–38); scenarios cover two (s5 unusable copy, s6 failure envelope), both in iteration 2 by the implementing agent itself. `data.gate == null`, a checklist that differs from the passed path, and `checklistExists: false` have no scenario (also run note V8). (b) Scenario 8 — the only evidence for DOD-02 from `gate info` (AC-13) and the DOD-06 change-set rule — wrote its run line at 14:41, before the final SKILL.md (mtime 14:43:54) and was not re-run by the verifier; 06-4:156–164 does not say which skill text each scenario proves. The reviewer's re-run of s8 on the final text (R-A, Requirement Conformance) gave the same result, so (b) is now a documentation gap only; (a) remains open.

```text
06-4, acceptance criteria: per scenario, name the skill text it ran on (iteration 1 / final) and who ran it
(subagent / implementing agent / verifier); add three error scenarios (gate null, checklist mismatch,
checklistExists false) on the final text, each expecting `error`, no changed file.
```

## Findings — tests/CreativeCoders.ShadeKit.Cli.Tests/Commands/Gate/Info/GateInfoCommandTests.cs

### [Minor][Tests] tests/CreativeCoders.ShadeKit.Cli.Tests/Commands/Gate/Info/GateInfoCommandTests.cs:280
The mapped-failure theory credits FP06-4-AC5 and FP06-4-AC6 to all 10 rows.

Rows such as `GitUnavailableException`, `FormatTooNewException`, `UpgradeNeededException`, `UnknownProfileException` prove neither AC5 (31/32/37) nor AC6 (41); a trait query for AC5 then lists 10 cases where 3 are relevant, which hides a missing case. `ExecuteAsync_PlainTextMappedFailure_ThrowsPrintedAbortExceptionWithoutOutput` (:353) carries AC6 for a plain-text abort that is not about the first phase, 41, or a done feature. Also noted by the verifier.

```csharp
[Theory]
[Trait("Requirement", "FP06-4-AC5")]
[MemberData(nameof(FeatureResolutionFailures))]   // FeatureNotFound, InvalidFeatureId, NoCurrentFeature
public async Task ExecuteAsync_JsonFeatureResolutionFailure_WritesErrorEnvelopeWithMappedCode(...)

[Fact]
[Trait("Requirement", "FP06-4-AC6")]
public async Task ExecuteAsync_JsonPhaseNotInProfile_WritesErrorEnvelopeWithPhaseNotInProfile() { ... }

[Theory]                                           // no requirement trait: the shared mapping of read failures
[MemberData(nameof(OtherReadFailures))]
public async Task ExecuteAsync_JsonOtherReadFailure_WritesErrorEnvelopeWithMappedCode(...)
```

## Findings — src/CreativeCoders.ShadeKit.Cli/Commands/Gate/Info/GateInfoCommandPhaseData.cs

### [Suggestion][Architecture] src/CreativeCoders.ShadeKit.Cli/Commands/Gate/Info/GateInfoCommandPhaseData.cs:16
`GateInfoCommandPhaseData` is a copy of `PhaseSnapshotData`.

Same shape (`Name`, `PhaseState State`), same `FromSnapshot(PhaseSnapshot)` factory, same JSON output as `Commands/Phase/PhaseSnapshotData.cs:16`. Two types for one wire shape drift (for example if the state serialization changes). `GateInfoCommand` already uses `Commands.Phase` (`PhaseTextFormat`), so reusing it adds no new dependency; only the XML doc of `PhaseSnapshotData` ("the `currentPhase` object") needs to become general. Also noted by the verifier.

```csharp
public sealed record GateInfoCommandData(
    string FeatureId,
    string Profile,
    string FeatureDir,
    IReadOnlyList<PhaseSnapshotData> Phases,
    GateInfoCommandGateData? Gate,
    double? CoverageThreshold)
{
    public static GateInfoCommandData FromResult(GateInfoResult result)
    {
        Ensure.NotNull(result);

        return new GateInfoCommandData(result.FeatureId, result.Profile, result.Paths.FeatureDir,
            [.. result.Phases.Select(PhaseSnapshotData.FromSnapshot)],
            result.Gate is null ? null : GateInfoCommandGateData.FromGate(result.Gate),
            result.CoverageThreshold);
    }
}
```

## Files reviewed without findings

- `src/CreativeCoders.ShadeKit.Core/Gates/GateInfoReader.cs`, `IGateInfoReader.cs`, `GateInfoResult.cs`, `GateInfoGate.cs`: read-only (`FormatAccess.Read`, `FileExists`), phase checked before `gates.json`/`config.json`, the checklist copy path from the shared `KitStorePaths.GetFeatureChecklistPath` (same rule as `FeatureChecklistWriter`), feature loading through `IFeatureStore` (D-16), cancellation passed through, XML docs complete. `IndexOf` is ordinal like `TryGetPhase`.
- `src/CreativeCoders.ShadeKit.Cli/Commands/Gate/GateCommandGroup.cs`, `Info/GateInfoCommand.cs`, `GateInfoCommandData.cs`, `GateInfoCommandGateData.cs`: follow the agent contract (`ShadeKitCommandContext.RunAsync`/`WriteSuccess`, no direct `ICliJsonWriter`), every dynamic value escaped, invariant number formatting tested under a comma-decimal culture.
- `ShadeKitCoreServiceCollectionExtensions.cs` (+ tests): singleton registration, consistent with the other gate services (all dependencies singletons).
- `tests/.../GateInfoReaderTests.cs`, `GateCommandsEndToEndTests.cs`: behaviour-focused, AAA, `Method_Scenario_Expected` names, real `KitStore`/`FeatureStore`/provider with only the resolver and git faked; file-system snapshots prove "changes no file"; the resolver is asserted not called with `--feature`. No conditional logic in test bodies.
- CLAUDE.md (2 places), both template comments, 05 (2 places, apart from the stale open point above).

## Tool Output Appendix

### dotnet build / dotnet test / dotnet format
- Not run on the working tree (tools = no, caller-provided results: build 0 warnings, 4720 passed / 1 pre-existing skip).
- Reviewer's scratch copy (`rsync` of the working tree without bin/obj): baseline build 0 errors; gate test classes 36 (Core) + 46 (Cli) passed, 0 failed, before and after the mutation runs.

### Skill scenario re-runs (reviewer)
- Fixtures and logs: `/private/tmp/claude-501/-Users-chris-src-github-shade-kit/5f334d24-397d-453a-8efa-030c3ebc3250/scratchpad/review-runs/` (`A-s8-final`, `B-s8-mut-dod02`, `C-s2-mut-na`, each with `before/`, `repo/`, `canned/`, `SKILL.md`, `run-log.md`). Each was run by a fresh subagent that was not told the expected result.
- R-A `fail (DOD-02, DOD-06)` · R-B `fail (DOD-06)` · R-C `fail (DOD-04, DOD-07, DOD-09)`. The agents' notes confirm the Minor finding about the Check-runs example line (R-A, R-B) and raised no new problem with the final text.

## Requirement Conformance

### B1 — Acceptance criteria

Evidence legend: "subagent" = a fresh agent ran the scenario; "self" = the implementing agent ran it from the skill text (weaker evidence, the author checks their own text); "verifier" = the independent verifier re-ran it. Reviewer re-runs: R-A (s8 fixture, final skill text, unmutated), R-B (s8 fixture, DOD-02 rule mutated to read `config.json`), R-C (s2 fixture, n/a rule deleted), each by a fresh subagent from the reviewer.

Reviewer checks on every written copy (s1–s8, s4 run 1 snapshot, the 4 verifier re-runs, R-A/B/C), done with a script that compares each copy to its `before/` snapshot: only the checklist copy changed (in s4 the extra `review.md` is the harness setup for run 2), IDs, criteria and row order are unchanged, every Evidence cell is non-empty, every non-row line is byte-identical apart from one added run line (plus the separating empty line on the first run), LF endings are kept, and in s4 run 2 the first run line stayed unchanged.

| AC | Task | Tests / scenarios | All exist | All pass | Falsifiable? | Verdict |
|---|---|---|---|---|---|---|
| AC-1 (FP06-4-AC1) n/a with reason for an artifact/phase the profile lacks | #3 | s2 (subagent, iteration-1 text; verifier re-run on final text), R-C | Yes | Yes | Yes | proven (executed: deleted the n/a rule and the known cases in a scratch copy of SKILL.md → R-C on the s2 `quick` fixture went red: DOD-04, DOD-07, DOD-09 `fail` instead of `n/a`, run line `fail (DOD-04, DOD-07, DOD-09)` instead of `pass`) |
| AC-6 missing artifact in a profile that writes it → fail | #3 | s4 run 1 (subagent, iteration 1; verifier re-run on final text: DOD-01 `fail` without `review.md`), s3 (self) | Yes | Yes | Yes | read-checked — not executed (no mutation run for this direction; the verifier re-run on the final text agrees) |
| AC-2 (FP06-4-AC2) every item pass/fail/n/a + Evidence, one more run line, earlier lines unchanged | #3 | s1, s2, s3, s4 (2 runs), s7, s8, verifier re-runs, R-A/B/C | Yes | Yes | Yes | read-checked — not executed (reviewer re-checked all written copies by script, see above; no mutation of the write/log rules was run) |
| AC-3 (FP06-4-AC3) no other file changed, no `phase` command | #3 | all scenarios + R-A/B/C (logs list commands; copies diffed) | Yes | Yes | Yes | read-checked — not executed (only the copy changed in every run; no `phase`, build, or out-of-list git command in any log) |
| AC-4 return: result, failed items with evidence, n/a items, run line, warnings (D-22) | #3 | s2, s7 (warning `gates-defaults-used` returned), s8, R-A | Yes | Yes | Yes | read-checked — not executed (returns in the logs have every field; no mutation) |
| AC-5 guidance comments of the copy kept | #3 | all writing scenarios | Yes | Yes | Yes | read-checked — not executed (byte comparison of the non-row lines by the reviewer) |
| AC-7 team items checked | #3 | s7 `SEC-01` (subagent, iteration 1; verifier re-run on final text) | Yes | Yes | Yes | read-checked — not executed |
| AC-8 overall pass iff no fail | #3 | s2 (`pass` with 3 `n/a`), s4, s8, R-A, R-C | Yes | Yes | Yes | read-checked — not executed (every run line matches its results) |
| AC-9 no docs/research path, no skeleton comment, frontmatter 0.7.0, no handshake, no next-steps load | #3 | static: grep of SKILL.md (no `docs/`, `research/`, skeleton comment), YAML parse with pyyaml (`metadata.version` 0.7.0, description unquoted and valid), text SKILL.md:16, :66, :140 | Yes | Yes | Partly (static) | read-checked — not executed (static text properties) |
| AC-10 no build/test/lint/coverage run | #3 | all scenario logs | Yes | Yes | Yes | read-checked — not executed |
| AC-11 (FP06-4-AC4) gate info content | #1, #2 | GateInfoReaderTests (AC4 cases), GateInfoCommandTests, GateCommandsEndToEndTests | Yes | Yes (36 + 46 in the scratch copy) | Yes | proven (executed: MA gate lookup forced to `TransitionGate.Default` → 8 reader tests + 3 E2E red; MB phase states forced to `pending` → 4 reader + 4 E2E red; MC trailing `/` dropped from `featureDir` → 3 GateInfoCommandTests + 3 E2E red) |
| AC-12 (FP06-4-AC4) changes no file | #1, #2 | `ReadAsync_AnyFeature_ChangesNoFile`, `GateInfo_Json_ChangesNoFileInKitStore` | Yes | Yes | Yes | proven (executed: MD reader writes `probe.txt` into the store root → both tests red) |
| AC-13 (FP06-4-AC7) DOD-02 from gate info, never config.json | #3 | s8 (subagent, pre-final text), R-A (final text, unmutated: DOD-02 `fail` 76.4 % vs 80, `config.json` not read), R-B | Yes | Yes | Yes | proven (executed: DOD-02 rule in a scratch SKILL.md switched to "read `config.json`" → R-B read `config.json` (50) and DOD-02 flipped to `pass`, run line `fail (DOD-06)` instead of R-A's `fail (DOD-02, DOD-06)`) |
| AC-14 (FP06-4-AC5) `--feature` or current; 31/32/37 | #1, #2 | AC5 cases in all three test classes | Yes | Yes | Yes | proven (executed: ME `featureId` ignored by the reader → 6 reader tests + 3 E2E red (feature not found, invalid id, done other feature)) |
| AC-15 (FP06-4-AC6) first phase → null; not in profile → 41; done answered | #1, #2 | AC6 cases in all three test classes | Yes | Yes | Yes | proven (executed: MF first-phase check broken → 3 reader + 1 E2E red; MG phase check removed → 2 reader + 2 E2E red; MH done feature refused → 3 reader + 1 E2E red) |
| AC-16 (FP06-4-AC7) undecidable items asked after all items | #3 | s7 (subagent, iteration 1; verifier re-run on final text) | Yes | Yes | Partly | read-checked — not executed ("after all items" can be seen only from the log order, not from the copy) |
| AC-17 (FP06-4-AC7) gate info failure / unusable copy → error, no file changed | #3 | s5, s6 (iteration 2 self-runs; subagent runs on the iteration-1 text) | No (3 of 5 branches) | Yes (the 2 covered) | Yes | unproven (gate `null`, checklist mismatch, `checklistExists: false` have no scenario; the 2 covered branches are self-runs only) |
| AC-18 run line with local time + UTC offset; template examples updated | #3 | all writing runs (real `date`, `+0100` → `+01:00`); template diff | Yes | Yes | Yes | read-checked — not executed |

All C# mutations ran in a scratch copy (`rsync` of the working tree); each changed file was restored and compared byte for byte with the working tree (`filecmp`, all identical), and the final rebuild ran the 82 gate tests green. Verifier mutations from the run note (reversed phase order, deleted `current.json`, first-phase index, threshold forced null, `ChecklistExists` forced true, `featureId` passed as null in the CLI, threshold mapped to null) are not counted here; they agree with the result.

**Count:** 6 proven (executed), 11 read-checked, 1 unproven, of 18 ACs.

### B2 — Tasks

| Task | Done when met? | Files within scope? | Docs with substance? |
|---|---|---|---|
| #1 Core GateInfoReader | Yes: `IGateInfoReader.ReadAsync(store, phase, featureId?, ct)` (D-18), result/gate records, DI registration + 3 DI theories, 36 reader tests; builds and passes | Yes: `Core/Gates/*` (4 new), `ShadeKitCoreServiceCollectionExtensions.cs` (+1 line), 2 test files | Yes: XML docs with order of checks, null rules, exceptions |
| #2 CLI gate info | Yes: group `gate`, command `info`, JSON shape per D-19, exit codes reused; 46 tests | Yes: `Cli/Commands/Gate/**`, 2 test files, CLAUDE.md (layout + command line). Outside the decision: the `-f` alias (Major finding) | CLAUDE.md: yes, but without `-f` |
| #3 Skill + templates + docs | Yes: SKILL.md worked out (single file, D-15), both templates changed in the Check-runs comment only, 06-4 rewritten with **Decided**/**Proposed** labels, 05 in 2 places | Yes; nothing else changed. README / 06 not updated (Minor finding) | 06-4: yes, detailed; 05: yes, but its open point L204 is stale (Minor finding) |

### B3 — Decisions

| D | Honoured? | Where |
|---|---|---|
| D-3 threshold via a CLI command | Yes | `GateInfoResult.CoverageThreshold`; SKILL.md:103, :171 |
| D-4 read-only git, base = merge-base, no base → fail | Yes, with a wording gap (Minor: existence test of `main`) | SKILL.md:112–120, :170 |
| D-7 `gate info <phase>`, group `gate` | Yes | `GateCommandGroup`, `GateInfoCommand` |
| D-8/D-10 content incl. profile phases | Yes | `GateInfoCommandData` |
| D-9 `--feature <id>` | Yes, **plus an undecided `-f`** — silent departure (Major) | `GateInfoCommandOptions.cs:28` |
| D-11 only request.md is data | Yes | SKILL.md:40 |
| D-12/D-26 ask the human, after all items | Yes | SKILL.md:45, :51, :173 |
| D-13 Core service in Core/Gates/ | Yes | |
| D-14 docs in 06-4 (+05, CLAUDE.md) | Yes | README/06 gap is outside D-14 (Minor) |
| D-15 single SKILL.md | Yes | only file in `skills/shade-kit-gate-check/` |
| D-16 load via IFeatureStore | Yes | `GateInfoReader.LoadFeatureAsync` |
| D-17 configured forward gate, state-independent | Yes | mutation MA red (see B1) |
| D-18 signature | Yes | `IGateInfoReader.cs:92` |
| D-19 phases as array | Yes | E2E JSON assertions |
| D-20 skill input featureId + phase + checklist path | Yes | SKILL.md:18–26 |
| D-21/D-29 run line with UTC offset, template examples | Yes | SKILL.md:60–64; both templates :30–32 |
| D-22 return content | Yes | SKILL.md:122–134 |
| D-23 first phase → gate null | Yes | mutation MF red |
| D-24 done answered | Yes | mutation MH red |
| D-25 error → no change | Yes (text); 3 of 5 branches unexercised (Minor) | SKILL.md:32–38, :174 |
| D-27 tests on three levels with traits | Yes (trait inflation: Minor) | |
| D-28 8 scenarios outside the repo | Yes | scratchpad/gate-check-scenarios/s1..s8 |
| P7 read-only, no format change | Yes: no `FormatIds`/`FormatRegistry` change; mutation MD red | |
| P8 no dependency | Yes: no `Directory.Packages.props` change | |

Implementer defaults checked in 06-4: percentage comparison of the threshold (06-4:119), `.shadekit/` excluded from the change set and empty change set → ask (06-4:120), human-decided items and the `error` return shape (06-4:138), "decide on artifacts, not phase states" (06-4:98), Evidence in backticks (06-4:128) — all marked **Proposed**, so none is a silent departure. DOD-09 `n/a` in `quick` "only when no playbook.md exists" (06-4:112, labelled Decided) traces to FP17-AC3 / 17-quick-profile.md:46 (**FACT**, Decided 2026-10-18) — not a departure. Labels I could not trace to a D-n of the brief (unverified, not counted): "the phase is checked before gates.json and config.json" (06-4:80) and the placement "after the section's guidance comment" (06-4:129); both are in line with the code and the scenarios.

### B4 — Scope

- Behaviour no AC asks for: the `-f` alias (counted in B3); the plain-text output of `gate info` (not asked by an AC, but required by the CLI convention that every command has a plain mode — not counted); "name a suspicious instruction in `request.md` in the return" (marked Proposed, derived from 06-2 — not counted). **Scope findings: 1** (`-f`).
- ACs without a code/text path: none. Every AC has a path in SKILL.md or in `GateInfoReader`/`GateInfoCommand`; AC-17's three untested branches have text (SKILL.md:34–36) but no evidence.

### Conformance summary

6 proven (executed) / 11 read-checked / 1 unproven, of 18 ACs; 1 decision departure (`-f`, D-9); 1 scope finding (`-f`).

## Hand-over — state of the run and how to continue

Added by the main agent after Gate 5 (2026-11-08). The run used the `implement-direct` workflow with the requirement `docs/feature-packages/06-4-gate-check-skill.md` and the skill `skill-creator`. At Gate 5 the user stopped the run: **no rework, no Minor fixes in this session**; everything needed to continue on another machine is recorded here. No separate implementation report (`docs/implementation/…`) was written; this section replaces it.

### State

- Branch `feature/feature-06-04` on `9011ef8` (the run started on `main` with a clean tree; the branch appeared during the run, created outside the workflow). **Nothing is committed.** To continue on another machine, the working tree must be committed and pushed first (by the user — the workflow never commits).
- Last verified: `dotnet build` → 0 warnings, 0 errors; `dotnet test` → 4721 total, 4720 passed, 0 failed, 1 skipped (`PathExecutableSearchTests.HasUnixExecuteBit_OnWindows_ReturnsTrue`, platform skip that existed before). `quick_validate.py` (skill-creator) → 18 of 18 skills valid.
- Changed files (uncommitted):
  - Task #1: `src/CreativeCoders.ShadeKit.Core/Gates/IGateInfoReader.cs`, `GateInfoReader.cs`, `GateInfoResult.cs`, `GateInfoGate.cs` (new); `src/CreativeCoders.ShadeKit.Core/ShadeKitCoreServiceCollectionExtensions.cs`; `tests/CreativeCoders.ShadeKit.Core.Tests/Gates/GateInfoReaderTests.cs` (new); `tests/CreativeCoders.ShadeKit.Core.Tests/ShadeKitCoreServiceCollectionExtensionsTests.cs`.
  - Task #2: `src/CreativeCoders.ShadeKit.Cli/Commands/Gate/GateCommandGroup.cs`, `Gate/Info/GateInfoCommand.cs`, `GateInfoCommandOptions.cs`, `GateInfoCommandData.cs`, `GateInfoCommandGateData.cs`, `GateInfoCommandPhaseData.cs` (new); `tests/CreativeCoders.ShadeKit.Cli.Tests/Commands/Gate/Info/GateInfoCommandTests.cs`, `Commands/Gate/GateCommandsEndToEndTests.cs` (new); `CLAUDE.md` (command line + one sentence in the `src/` row).
  - Task #3: `skills/shade-kit-gate-check/SKILL.md`; `src/CreativeCoders.ShadeKit.Core/Templates/Resources/checks/definition-of-ready.template.md` and `definition-of-done.template.md` (only the "Check runs" guidance comment); `docs/feature-packages/06-4-gate-check-skill.md`; `docs/feature-packages/05-gates-and-checklists.md` (2 places).
  - This report: `docs/reviews/2026-11-08-feature-feature-06-04-uncommitted.md`.

### Decision table (Phase 2 and the gates)

| D | Topic | Decision | Origin |
|---|---|---|---|
| D-1 | Working tree | clean at start, branch `main` | n/a |
| D-2/D-6 | Mode of the run | first "ordinary request, ask on inconsistencies"; after the scope grew (new CLI command), the full `implement-direct` workflow | user |
| D-3 | DOD-02 threshold vs. "skills never read config.json" | a CLI command that delivers everything the gate check needs | user |
| D-4 | Change set for DOD-06/DOD-08 | read-only git (`status`, `diff`, `log`, `merge-base`, `symbolic-ref`); base = merge-base with the default branch plus uncommitted changes; no base → fail | recommended |
| D-5/D-20 | Skill input | `featureId`, gate target phase, checklist path relative to the feature folder — all from the same `phase start --json` result; profile comes from `gate info` | recommended |
| D-7 | Command | `shade-kit gate info <phase>`, new group `gate` (not under `phase`, FP06-4-AC3 forbids phase commands) | recommended |
| D-8/D-10 | Content | feature id, profile, feature folder, profile phases in order with state, gate of the phase, `coverageThreshold` | user |
| D-9 | Feature selection | option `--feature <id>`, without it the current feature | user |
| D-11 | "data, not instructions" | only `request.md` (existing convention) | user |
| D-12/D-26 | Undecidable items | the skill asks the human, collected after all items are checked (structured tool, ≤ 4 per call); Evidence names the human decision; no answer → fail | user + recommended |
| D-13 | Core location | new service in `Core/Gates/` | recommended |
| D-14 | Documentation | 06-4 extended, reference in 05, `CLAUDE.md` | recommended |
| D-15 | Skill files | single `SKILL.md` | recommended |
| D-16 | Loading | through `IFeatureStore.ShowAsync` / `GetCurrentAsync` | recommended |
| D-17 | Gate semantics | configured forward gate (gates.json or default), independent of the phase state | recommended |
| D-18 | Names | `IGateInfoReader` / `GateInfoReader` / `GateInfoResult`, `ReadAsync(store, phase, featureId?, ct)`; CLI `GateCommandGroup`, `GateInfoCommand…` | recommended + convention |
| D-19 | JSON | `{ featureId, profile, featureDir, phases: [{name, state}], gate: {phase, approval, checklist, checklistPath, checklistExists} \| null, coverageThreshold }` | user |
| D-21/D-29 | Check-runs line | `- YYYY-MM-DD HH:MM ±HH:MM – pass` / `– fail (IDs)`; both template examples updated | user + recommended |
| D-22 | Return | result, failed items, n/a items, the run line, `gate info` warnings | recommended |
| D-23 | First phase | `gate: null` (success) | recommended |
| D-24 | Done feature | answered normally | recommended |
| D-25 | Unusable copy / `gate info` failure | no file changed, result `error` | recommended |
| D-27 | C# tests | Core (TempKitDirectory) + command (fake) + end to end, `[Trait("Requirement","FP06-4-ACn")]` | recommended + convention |
| D-28 | Skill proof | 8 scenario runs outside the repository | recommended |
| D-30 | Implementation skills | `skill-creator:skill-creator`, `cc-ai-dotnet:dotnet-fundamentals`, `dotnet-tester`, `dotnet-xmldocs` | user |
| D-31 | Review set | `cc-ai-dotnet:dotnet-reviewer` + `agent-skills:code-review-and-quality` | recommended |
| D-32 | Implementation mode | sub-agents (implementer + verifier per task), sequential | recommended |
| D-33 | Gate 5 | stop: no rework and no Minor fixes in this session; record the hand-over here | user |
| P7 / P8 | Persistence / dependencies | read-only, no format change / no new package | pre-answered / n/a |

### Tasks and verification

| Task | Result | Verifier | Evidence |
|---|---|---|---|
| #1 Core `GateInfoReader` | done | PASS | 36 reader tests; verifier mutations (reversed phase order, deleted `current.json`, first-phase index, threshold null, `ChecklistExists` true) all red, restore byte-identical |
| #2 CLI `gate info` | done | PASS | 46 gate CLI tests; mutations (`featureId` passed as null, threshold mapped to null) red; smoke-check in a scratch repo: exit 0/10/31/32/37/41 as expected, `.shadekit` shasum unchanged |
| #3 Skill + templates + docs | done | PASS | 9 scenario runs (5 run by the implementing agent itself because a hook refused agent calls); verifier re-ran s2, s4 (both runs), s7 from the final text; reviewer re-ran s8 on the final text and two mutated texts (R-A/R-B/R-C) |

Skill-Invocation Log:
- Task #1 — [x] dotnet-fundamentals, [x] dotnet-tester, [x] dotnet-xmldocs — invoked by the implementer in turns 1–3, before its first write (implementer report, accepted by the verifier). dotnet-tester's gap-analysis sub-agent was refused by the hook; the implementer did the gap analysis itself.
- Task #2 — [x] dotnet-fundamentals, [x] dotnet-tester, [x] dotnet-xmldocs — turns 1–3, before the first write (implementer report).
- Task #3 — [x] skill-creator:skill-creator — first tool call, before the first write (checked by the verifier in the implementer transcript).
- Review — [x] cc-ai-dotnet:dotnet-reviewer, [x] agent-skills:code-review-and-quality, [x] cc-ai-dotnet:dotnet-tester.

### Open work (to do on the other machine)

Rework the workflow classifies as mandatory (deferred by the user at Gate 5, D-33):

- **R-1** `src/CreativeCoders.ShadeKit.Cli/Commands/Gate/Info/GateInfoCommandOptions.cs:28` — the undecided short option `-f`. Either remove it (`[OptionParameter("feature", …)]`) and pin "no short name" in `GateInfoCommandOptions_Attributes_PhaseIsRequiredFirstValueAndFeatureIsOptionalNamedOption`, or decide to keep it: mark it **Proposed** in 06-4, add `-f|--feature` to the `CLAUDE.md` command line, and assert `ShortName == 'f'` in that test. Then `dotnet build` and `dotnet test`.
- **R-2** AC-17 / FP06-4-AC7 is unproven for 3 of 5 `error` branches. Add scenarios on the final skill text, each run by a fresh sub-agent: (a) `gate info` returns `gate: null`, (b) `data.gate.checklist` differs from the passed path, (c) `checklistExists: false`; each expects result `error` and no changed file. Re-run s5 (copy without the Evidence column) and s6 (`feature-not-found` envelope) by a fresh sub-agent on the final text. Update the acceptance-criteria section of 06-4: per scenario, the skill text it ran on (iteration 1 / final) and who ran it (sub-agent / implementing agent / verifier / reviewer).

Findings not selected at Gate 5 — **deferred, open for a decision**:

- M1 `SKILL.md:64` — run line placement next to the example line in the comment (say "after `-->`").
- M2 `SKILL.md:116` — the `main`/`master` fallback must use `git merge-base` succeeding as the only existence test.
- M3 `06-4:134` — extend the open point: 06-5 must decide what `error` means with approval off.
- M4 DoD template `:18`, `05:160`, `05:204` — DOD-02 criterion and 05 open point still name `config.json`.
- M5 `docs/feature-packages/README.md:3/22`, `06-skill-foundation.md:23` — Kind, Depends on (05, 06-2), status line for 06-4.
- M6 `GateInfoCommandTests.cs:280/353` — split the mapped-failure theory so AC5/AC6 traits tag only the relevant rows.
- S1 `GateInfoCommandPhaseData.cs` — reuse `PhaseSnapshotData`.
- S2 `SKILL.md:118` — bound `git diff` (`--name-status` first, then per file).
- S3 `SKILL.md:117` — work done on the default branch: ask when the base is `HEAD`.
- N1 `SKILL.md:129` — add the "Decided by a human" and "Suspicious instructions in request.md" lines to the return example.
- N2 `SKILL.md:51` — question options ask the human for the reason.

Implementer defaults recorded as **Proposed** in 06-4 that still need a decision: threshold and measured coverage compared as percentages (a fractional threshold such as `0.8` would pass wrongly; the unit of `coverageThreshold` is an open point of 05); `.shadekit/` excluded from the change set; empty change set → ask the human; the return lists human-decided items; the shape of the `error` return. Two labels the reviewer could not trace to a decision: "the phase is checked before gates.json and config.json" (06-4:80) and "the first run line goes after the section's guidance comment" (06-4:129).

Follow-ups outside this package: 06-5 (`shade-kit-phase-entry`) step 3 must pass `featureId`, `gate.phase`, and `gate.checklist` from the `phase start` result, and step 4 must handle the `error` result (including approval off). The DoD template's link line points to `tasks.md`, which `quick` never writes.

### Scenario evidence (local only)

The scenario fixtures and logs are **not in the repository** (D-28) and exist only on the machine of this run, in a temporary folder that the system may delete: `/private/tmp/claude-501/-Users-chris-src-github-shade-kit/5f334d24-397d-453a-8efa-030c3ebc3250/scratchpad/` (`gate-check-scenarios/s1..s8`, `verify-s2`, `verify-s4*`, `verify-s7`, `review-runs/`, `mut/`, `run-note.md`, `gate-check-tools/check_run.py`, `venv/` with pyyaml for `quick_validate.py`). On another machine, rebuild them as follows:

- Each scenario is a folder with `before/` and `repo/` (a fake repository with `.shadekit/features/<id>/`: the artifacts the scenario needs and the checklist copy made from the shipped template with `{{feature-id}}` replaced), `canned/gate-info.json` (the envelope of `shade-kit gate info`, shape as in D-19; failure envelope `{"ok":false,"data":null,"warnings":[],"error":{"code":"…","message":"…"}}`), canned git output where needed, and `run-log.md`.
- A fresh sub-agent reads `skills/shade-kit-gate-check/SKILL.md`, gets the caller input (`featureId`, phase, checklist path), logs every `shade-kit`/`git` command instead of running it and reads the canned output, may edit only inside the scenario folder, and records commands, questions, changed files, and its return. Afterwards `diff -r before repo` must show only Result/Evidence cells and one new run line.
- The scenarios: s1 feature DoR all pass · s2 quick DoD (DOD-04/07/09 n/a) · s3 bugfix DoR (DOR-10 passes on "Affected code" of `diagnosis.md`) · s4 feature DoD with `review.md` missing (fail, not n/a) and an open blocker, then a second run (first run line byte-identical) · s5 copy without the Evidence column → `error` · s6 `feature-not-found` → `error` · s7 team item `SEC-01` → question, canned answer "pass — reviewed by A. Smith on 2026-11-07" · s8 `coverageThreshold: 80`, `verification.md` 76.4 %, fixture `config.json` with 50 (must not be read), canned diff adds `#pragma warning disable CA1062` without a reason → DOD-02 and DOD-06 fail. Plus the three new `error` scenarios of R-2.
- `quick_validate.py` needs a Python with `pyyaml` (the system `python3` on the run machine had none).

### Notes on the run environment

- A context-mode hook rewrites every Agent call, so the auto-mode classifier refused each first call and every sub-agent had to be started twice; sub-agents that tried to start their own sub-agents were partly refused. Turning that hook off for Agent calls avoids this.
- On macOS, BSD `date` has no `%:z`; the skill uses `%z` and inserts the colon.
