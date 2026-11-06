# 06-9 · Align the skill skeletons

| | |
|---|---|
| Status | Draft, 2026-11-04 (split from package 06) |
| Source | [06 Skill foundation](06-skill-foundation.md), open points in [README](README.md#open-questions) (skill skeletons not aligned with the built `phase` commands) |
| Kind | Cross-cutting (all skill skeletons) |
| Depends on | 06-3, 06-5, 06-6 |
| Enables | Every phase package (09–17) |

## Benefit

Every skill the human calls is gated and ends the same way before its phase is worked out: the phase packages 09–17 build on skeletons that already use the entry protocol and the next-steps message.

## Scope

### In scope

- The 8 phase skills: `refine`, `map`, `plan`, `implement`, `verify`, `finish`, `diagnose`, `quick`.
- `new`, `discuss`, and `drop`, which the human calls but which have no entry protocol.
- The review by hand of all skill texts (06-2).

### Out of scope

- The phase work of each skill (packages 09–17).
- Removing the source comments that point to `docs/` and `research/`: they stay until a skill is worked out; FP07-AC7 checks them when the skills are shipped. **Decided** on 2026-11-04.
- `shade-kit-report` (package 15) and `shade-kit-review` (package 13), apart from the review by hand.

## Functional description

### Phase skills

- Step 1 of every phase skill loads `shade-kit-phase-entry` (06-5) with its phase and continues only as the entry protocol says.
- The last step of every phase skill loads `shade-kit-next-steps` (06-3).
- The description of every phase skill says that it runs only when the user calls it by name (06-2).

### `new`, `discuss`, `drop`

- Step 1 is the version handshake with the version stated in the text of the step, as 06-2 sets out (the frontmatter is the source, but the model does not see it): `shade-kit version --json --expect <version>` (06-1, 06-2). **Decided** on 2026-11-04, the version in the text on 2026-11-06.
- The last step loads `shade-kit-next-steps` (06-3).

### Clean-up

- The sentence in the source comments of the skeletons that every command other than `version`, `init`, and `feature …` is planned is removed or corrected (13 skeletons on 2026-11-04). **Decided** on 2026-11-04.
- Skeletons that show CLI results tell the human about the warnings (through `shade-kit-next-steps`).
- The skeletons of `finish` and `report` keep their expectation of the `report` setting; package 15 owns it (FP15-AC7).

### Review of the skill texts

All skill texts under `skills/` are reviewed by hand against the conventions of 06-2. **Decided** on 2026-11-04.

## Acceptance criteria

- **FP06-9-AC1** A phase skill called directly, without `shade-kit-next-phase`, runs the entry protocol before any phase work.
- **FP06-9-AC2** No skill starts a phase skill, and no phase skill calls `phase start` for a phase other than its own; a review of the skill texts finds no hand-off.
- **FP06-9-AC3** Every phase skill ends with the next-steps message, built from `phase next --json` when a current feature exists.
- **FP06-9-AC4** The description of every phase skill says that it runs only when the user calls it by name.
- **FP06-9-AC5** No kit skill writes a kit JSON file directly; a review of the skill texts finds only CLI calls for state changes.
- **FP06-9-AC6** For a feature whose current phase is `in-progress`, the phase skill of that phase, called by the human, runs no gate.
- **FP06-9-AC7** `new`, `discuss`, and `drop` stop before any other work when the engine's minor version differs from their own, and end with the next-steps message.
- **FP06-9-AC8** No skill says that a built `phase` command is planned.

## Open points

- Until 06-5, 06-6, and 06-9 are built, the handshake step of the skeletons cannot detect a mismatch: it calls `version --json` without `--expect`, and the model does not see `metadata.version` (06-2).
