# 11 · Map the feature onto the code base (`map`)

| | |
|---|---|
| Status | Draft, 2026-10-14 |
| Source | Concept §3.3 (research focuses), §5.1 (`map`), §5.2 (DoR item "affected areas known") |
| Depends on | 06, 10 |
| Enables | 12 (`plan`) |

## Benefit

Before planning, the team knows where the feature lands in the code: which architecture it has to fit, which code it touches, which existing libraries and APIs already solve parts of it, and which security risks the change brings. Plans then build on facts about the code base instead of assumptions, and nobody re-implements what a framework already provides.

## Scope

### In scope

- The `map` phase skill in the `feature` profile.
- Its five research focuses and their output in `researches.md`.
- Skipping `map` on purpose.

### Out of scope

- Changing code. `map` only reads.
- Memory from earlier features (later, concept §8).
- The `refactor` profile, which also uses `map` (later).

## Functional description

### Run

1. Read `spec.md` and the `refine` sections of `researches.md`.
2. Run the research skill (package 06) with five focuses, in parallel where the agent supports it:

| Focus | Question it answers |
|---|---|
| Best practice | How is this kind of feature usually built well in this stack? |
| Existing architecture | Which layers, patterns, and conventions does the change have to follow? |
| Existing affected code | Which files, types, and tests are touched, with paths? |
| Don't hand-roll | Which existing frameworks, libraries, or APIs (already in the project or well established) cover parts of the requirement? |
| Security at code level | Which code paths handle input, authentication, secrets, or data that needs protection? |

3. Every finding names its source (file path with line, or link). A finding without a source is marked as an assumption. **Proposed**, in line with the repository's "verify every assumption" rule.
4. Merge into `researches.md` as `map / <focus>` sections.
5. Summarise for the human: affected areas, risks, and recommendations for the plan.

### Skipping `map`

The human can skip `map` on purpose, for example for a change confined to one file. The agent then records in `researches.md` a `map / skipped` section with the reason and the git user. The DoR item "affected areas known (`map` done, or skipped on purpose)" (package 05) passes on that record. **Proposed.**

Skipping means skipping the work, not the phase: the engine does not allow skipping a phase (package 04), so the feature still passes through `map` and its gate. This keeps the decision visible in the history. **Decided** (confirmation list, 2026-10-18).

## Acceptance criteria

- **FP11-AC1** After a `map` run, `researches.md` has one section per focus, and the `refine` sections are unchanged.
- **FP11-AC2** Every finding in the "existing affected code" section names a file path.
- **FP11-AC3** A finding without a source is marked as an assumption.
- **FP11-AC4** A skipped `map` leaves a `map / skipped` section with reason and user, and the DoR check accepts it.
- **FP11-AC5** A `map` run changes no file outside `.shadekit/features/<id>/`.

## Open points

- None.
