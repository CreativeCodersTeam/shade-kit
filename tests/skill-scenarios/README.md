# Skill scenario runs

Reusable building blocks for the scenario runs that `CLAUDE.md` ("Skill conventions") requires for a changed skill: a
subagent loads the skill, follows it with canned CLI envelopes, and records the commands it would run and its output. The
skill texts have no automated tests; these runs and a review by hand are how a changed skill is checked.

Nothing here is compiled or shipped: no project picks up this folder, and the skill guard tests scan only `skills/` (and,
for `SingleSkillSourceTests`, files named `SKILL.md`, of which there are none here).

## Layout

| Path | Purpose |
|---|---|
| `RUNNER-PROTOCOL.md` | The rules every runner follows: which files it may read, how it records commands, questions, and answers, and the sections of its output. |
| `SCENARIOS.md` | The scenario catalog with the shared fixture table. Placeholders: `<work>` (the work folder), `<repository>` (this repository), `<contract>` (the CLI contract), `<sso-feature>`, `<quick-feature>`. |
| `generate_fixtures.py` | Builds the work folder from the current engine: fixtures, fixture repositories, `ids.json`, and resolved copies of the catalog and the protocol. It reads the CLI contract from the version handshake sentence of `skills/shade-kit-phase-entry/SKILL.md`. |
| `checks/check_runs.py` | The expectations and the checker. Runners must never see it. |

The work folder (outside this repository) holds after the generator:

| Path | Content |
|---|---|
| `fixtures/*.json` | The JSON envelopes of the real engine calls; the fixture table of the catalog names the ones the runners use. `env-version-engine-outdated.json` and `env-version-skills-outdated.json` are the refused handshakes (`--expect-contract 999` and `0`). They come from these stand-in calls, so their `error.message` names the contract 999 or 0, not the contract the skill passes. |
| `ids.json` | `sso` and `quick` (the generated feature ids), `contract` (the CLI contract, a number), `notesFile` (the notes of S13), and `csv` (the feature that S17 creates). |
| `repo/` | The fixture repository: `init`, a `feature` profile feature in `refine`, and a `quick` profile feature whose `finish` was requested, with five team items in its Definition of Done copy. |
| `repo-s5/`, `repo-s6/` | Copies of `repo/` that the gate-check runs edit. |
| `repo-s9/` | A copy of `repo/` whose quick `request.md` asks for a change that is too large for `quick`. |
| `repo-empty/` | An initialised repository without features (no current feature). S18 reads it, and S19 reads `repo/`, without a copy: neither gets past the handshake. |
| `repo-s10/`, `repo-s14/` | Copies of `repo-empty/`; the `discuss` run of S10 writes its notes into `repo-s10/`. |
| `repo-s13/` | A copy of `repo-empty/` with discussion notes in `.shadekit/discussions/` (`notesFile` in `ids.json`). |
| `repo-s11/`, `repo-s15/`, `repo-s16/` | Copies of `repo/` with the sso feature selected as current; S11 writes its notes into `repo-s11/`. |
| `repo-s12/` | A copy of `repo/` whose quick feature is finished and selected again, without `templates/discussion.md`. |
| `repo-s17/` | A copy of `repo/` on the branch `feature/<sso>`, without the local override, so the branch selects the sso feature. |
| `out/` | Empty; each runner writes `out/<id>.md`. |
| `SCENARIOS.md`, `RUNNER-PROTOCOL.md` | The resolved copies the runners read. |

## Procedure

1. Build the CLI: `dotnet build` from the repository root (the generator builds it too unless `--engine-dll` is given).
2. Generate the work folder outside this repository:

   ```sh
   python3 -I tests/skill-scenarios/generate_fixtures.py [--work <dir>] [--engine-dll <path to shade-kit.dll>]
   ```

   Without `--work` it creates a new temporary folder and prints its path. A `--work` folder must not exist yet or be
   empty, and must lie outside this repository (`init` would otherwise write into the wrong git repository). Unlike the
   unit tests, the generator runs `git` and the real engine (with a `PATH` that holds only `git` and `dotnet`, so that no
   installed agent changes the `version` envelope). It stops with a non-zero exit code and a message when an engine call
   returns `ok: false` or an unexpected `outcome`, so a workflow change shows up at once; then update the generator, the
   catalog, and the expectations together.
3. Start one fresh subagent per scenario with the runner prompt below. Never run a scenario in the context that knows the
   expectations (`checks/`), and never run two scenarios in one subagent. Runs can go in parallel.
4. Check the outputs:

   ```sh
   python3 -I tests/skill-scenarios/checks/check_runs.py <work> [outdir] [--only S10,S11]
   ```

   With `--only`, only the checks of the named scenarios are reported and counted.

   It prints one `PASS`/`FAIL` line per check and the totals, and exits with 1 when a check fails. Read the `## Unclear`
   section of every output too: a check can pass while the skill still made the runner guess.
5. Record the result (date, scenarios, agents, check totals, what was unclear and what changed) in the feature package
   of the skill under `docs/feature-packages/`.

## Runner prompt template

Replace `<work>`, `<repository>`, and `<id>`:

```text
You are a scenario runner for the SHADE kit skills. Read <work>/RUNNER-PROTOCOL.md and follow it exactly.
Your scenario is <id> in <work>/SCENARIOS.md: use the shared fixture table and the section of <id> (and of the
scenario it refers to with "Otherwise identical to").
The SHADE kit repository is <repository>; read only the skill files under <repository>/skills/.
Write your output to <work>/out/<id>.md. Do not read any other file in <work>/out/.
```

## Rules

- Runners read only the skill texts, the protocol, the catalog, the fixtures, and the repository folder of their
  scenario. They never read `checks/`, the rest of this repository, or the outputs of other runs.
- Outputs of runs and generated fixtures are never committed; they live in the work folder only. The fixtures are
  generated from the current engine each time, so no envelope file goes stale.
- The expectations in `checks/check_runs.py` describe what the skill texts do now; when a skill change changes what a
  scenario does, change the expectation together with the skill.
- To add a scenario: a catalog entry in `SCENARIOS.md` (agent, skill, inputs, canned answers, repository folder, and
  every fixture it needs in the fixture table), its expectations in `checks/check_runs.py`, and its fixtures (engine
  calls, repository content, or a repository copy) in `generate_fixtures.py`. Use the placeholders for every value the
  generator produces.

## Runs

- **2026-11-18:** nine scenarios S1–S9 for the skill text clarifications after scenario runs: `shade-kit-gate-check`
  (S5, S6), `shade-kit-interview` (S7, S8), `shade-kit-next-steps` (S1, S2), `shade-kit-phase-entry` (S3, S4), and
  `shade-kit-quick` (S9), played as Claude Code, GitHub Copilot CLI, and OpenAI Codex CLI in default and plan mode. All
  checks passed after the clarifications (69 of 69).
- **2026-11-20:** seven scenarios S10–S16 for package 09: `shade-kit-discuss` (S10 no current feature, S11 active
  feature, S12 finished feature without template) and `shade-kit-new` (S13 notes with `--move-source`, S14 issue URL
  without `gh`, S15 active feature ended, S16 two lines of bug-fix text), with their own fixture tables and
  repositories. Iteration 1: 130 of 133 checks (after fixing two checker false positives); iteration 2 after the skill
  clarifications: 133 of 133. After the review: S17 (the branch selects the active feature) added, the git check
  of S13–S17 fails on any `git` command, the generator refuses an engine `PATH` with `gh`; iteration 3 of S13–S17:
  83 of 84 (S13: the runner ran a read-only `git status` outside the skill), S10–S12 re-checked: 72 of 72. After
  review round 2: S17 checks read the closing message, `feature new` must carry its own `--json`, step 0 of a runner
  is left out of the git check, `--only` refuses unknown ids; iteration 3 re-checked 84 of 84, S17 iteration 4 18 of
  18.
- **2026-11-22:** two scenarios S18–S19 for the CLI contract handshake: `shade-kit-new` with an engine older than
  the skills (S18, `engine-outdated`, Claude Code) and `shade-kit-next-phase` with skills older than the engine
  (S19, `skills-outdated`, OpenAI Codex CLI default mode). 22 of 22 checks. After review round 1: the engine-update
  check of both (`ENGINE_UPDATE`) also counts an update named through the tool ("get a newer shade-kit") and no
  longer counts "install the kit plugin that matches the engine" (checker gap D-23 of 06-1, closed); re-checked on
  the same outputs 22 of 22.
