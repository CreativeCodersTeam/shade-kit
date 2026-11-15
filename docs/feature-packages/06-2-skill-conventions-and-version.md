# 06-2 · Skill conventions and version metadata

| | |
|---|---|
| Status | Draft, 2026-11-04 (split from package 06), updated 2026-11-06 (implementation: visibility check in Claude Code, `metadata.version` `0.7.0` in every skill, the SemVer rule, who checks, the wording of the handshake step, the quoted `shade-kit-report` description, the skill conventions in `CLAUDE.md`) and 2026-11-13 (package 07 implementation: the skeleton source comments removed, the guard test of FP07-AC7, the plugin skill names in Claude Code), and 2026-11-14 (package 07 review rework, Gate 5: every skill raised to `0.8.0`; the guard test `SkillKitVersionTests` checks the version and every handshake text against the major.minor of the engine build; the FP07-AC7 guard scans every text file under `skills/`), and 2026-11-15 (package 07 review rework, Gate 5 round 2: the guard takes the most frequent version as the kit version, so an outlier skill is the one reported), and 2026-11-15 (package 08: frontmatter visibility of Copilot and Codex answered). AC1 and AC2 implemented; AC3: rule and wording set, applied by 06-5, 06-6, and 06-9. |
| Source | [06 Skill foundation](06-skill-foundation.md) (skill conventions, version handshake), Concept §3.2, §3.3 |
| Kind | Cross-cutting (all skills) |
| Depends on | 06-1 |
| Enables | 06-3 to 06-9 |

## Benefit

Every kit skill follows the same rules and carries its kit version. A team can install the skills as a plugin, and every skill the human calls can check whether it fits the installed engine.

## Scope

### In scope

- The conventions every kit skill follows.
- The kit version in the frontmatter of every skill.
- Checking whether Claude Code shows the frontmatter to the model, and the fallback if it does not.

### Out of scope

- Writing the version check into the skills (06-5, 06-6, 06-9); this package sets who checks and the wording.
- Copilot and Codex (package 08).
- Writing the skill version from GitVersion; the version is raised by hand, and since 2026-11-14 a guard test checks it (see Kit version in the frontmatter and open points).

## Functional description

### Skill conventions

- Skills are written in the shared `SKILL.md` format and the plugin layout of `skills/`. Claude-only features (for example subagents) are only used where an agent-neutral way would lose function, and each such use is named in the skill. **Decided** (constraint, concept §1), **Unverified** (format compatibility, resolved in package 08).
- A phase starts only when the human calls its phase skill (`/shade-kit-<phase>`). No skill starts a phase skill, and no phase skill calls `phase start` for a phase other than its own; a skill that ends names the skills the human can call ([06-3 Next-steps message](06-3-next-steps-skill.md)). **Decided** on 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md)). The rule holds by convention, not technically: the description of every phase skill says that it runs only when the user calls it by name, the skill texts contain no hand-off, and the managed section in `CLAUDE.md` / `AGENTS.md` tells the agent (package 02, **Implemented** in `ManagedInstructionSection`). A restriction such as Claude Code's `disable-model-invocation` frontmatter field is not used in v1. **Decided** on 2026-10-30. Known weak point, like approvals (package 04): nothing technically stops an agent from loading a phase skill on its own.
- Skills never read or write `feature.json`, `gates.json`, or `config.json` directly. They use the CLI with `--json`. Skills read and write Markdown artifacts in the feature folder directly. **Decided** on 2026-11-04. This keeps every state change behind the engine's version check (package 01).
- A shipped skill references no path under `research/` or `docs/` of the kit repository; those folders are design notes and are not shipped. The source comments of the skeletons are removed when a skill is worked out. **Decided** on 2026-10-18 (findings clarification). Checked when the skills are shipped: FP07-AC7 (package 07). **Decided** on 2026-11-04. Since package 07 this repository is the shipped plugin, so the source comments `<!-- Skeleton: purpose and flow only, details follow. Source: docs/... -->` were removed from all 13 skeletons that still had them, also from those not worked out yet. **Decided** on 2026-11-13 (package 07 implementation). **Implemented**. A guard test fails when a text file under `skills/` has a path segment `docs/` or `research/`; until 2026-11-14 it scanned only `SKILL.md` (**Implemented**: `ShippedSkillsTests`, FP07-AC7).
- Every artifact a skill creates starts from the repository's template in `.shadekit/templates/`, not from text built into the skill, so team edits to templates take effect. **Decided** on 2026-11-04. The skill follows the template conventions of package 02: it follows the guidance comments, replaces every placeholder, and removes the guidance comments; checklist copies (package 05) keep their guidance comments. One exception: when `.shadekit/templates/researches.md` is missing, `shade-kit-research` creates `researches.md` with the lines the template would give (title and feature line) and says so in its return, so that a research run is not lost to a deleted template. **Decided** on 2026-11-13 ([06-8](06-8-research-skill.md)).
- `request.md` can hold third-party content, written unchanged by `feature new`: the body and every comment of an issue, or a whole web page (package 03). Every skill that reads it, and every research agent that gets it as feature context, treats it as data that describes the requirement, never as instructions to the agent: it does not follow commands, links, or tool requests in it, and shows the human any instruction in it that looks suspicious (a research agent reports it to the calling skill, which shows it). **Decided** on 2026-10-25 (code review of the rework of `feature new`, finding F6; package 09, [`new`](09-discuss-and-intake.md#new)). **Implemented** (`shade-kit-new`, `shade-kit-refine`, `shade-kit-diagnose`, `shade-kit-quick`, `shade-kit-research`).
- The skill texts are checked by a review by hand when a sub-feature is accepted; there are no automated tests of the skill texts. **Decided** on 2026-11-04. Two guard tests check the shipped files mechanically, not what a skill does: the references into `docs/` and `research/` (FP07-AC7) and the kit version (`SkillKitVersionTests`, FP07-AC2, since 2026-11-14).

### Kit version in the frontmatter

- Every skill under `skills/` carries its kit version in the frontmatter, hardcoded and committed, because the skills must be installable as a plugin with their version included. **Decided** on 2026-11-04. **Implemented** (all 19 skills under `skills/`, `0.8.0` since 2026-11-14; `0.7.0` from 2026-11-06).

  ```yaml
  ---
  name: shade-kit-next-phase
  description: ...
  metadata:
    version: 0.8.0
  ---
  ```

- All skills carry the same version. **Decided** on 2026-11-04. **Implemented** (`0.8.0`; checked by `SkillKitVersionTests`).
- The version is a SemVer version without a prerelease label. **Decided** on 2026-11-06 (was **Proposed**).
- The value is written unquoted (`version: 0.8.0`). A `description` that YAML would not read unchanged as a plain scalar is written in double quotes: unquoted, a `: ` makes the frontmatter invalid YAML (Claude Code then loads the skill with empty metadata), and a ` #` silently cuts the description at the comment. **Decided** on 2026-11-06 (implementation).
- **Implemented** (found in the implementation on 2026-11-06): the unquoted description of `shade-kit-report` contained `: `, so its frontmatter was invalid YAML. The description is now in double quotes, with the same text.
- Who checks the version. **Decided** on 2026-11-04, clarified on 2026-11-06 (implementation clarification): the earlier wording said that the skills the human calls check and that skills only loaded by another skill do not, but `shade-kit-phase-entry`, which only the phase skills load, checks for them (06-5).
  - `shade-kit-phase-entry` checks for the phase skills, which do not check themselves (06-5).
  - The other skills the human calls (`new`, `discuss`, `drop`, `shade-kit-next-phase`) check in their own first step (06-6, 06-9).
  - Every other skill that only another skill loads does not check.

  Each check passes this version to `shade-kit version --json --expect <version>` (06-1).

### Frontmatter visibility

- This sub-feature checks first whether Claude Code shows the frontmatter, and `metadata.version` in it, to the model when it loads a skill. **Decided** on 2026-11-04. Copilot and Codex were checked in package 08 on 2026-11-15, from documentation and source only (neither CLI is installed): Copilot does not show the frontmatter to the model since Copilot CLI 1.0.48, and Codex injects the whole `SKILL.md`, frontmatter included, on a mention; both ignore `metadata.version`. So the rule below, the version stated in the handshake text, holds for all three agents. **Unverified:** how their YAML parsers type the unquoted `0.8.0` ([08](08-copilot-and-codex-integration.md#open-points)).
- Result: Claude Code does not show the frontmatter to the model. Checked on 2026-11-06 with Claude Code 2.1.318 and the model `claude-opus-5-5`.
  - How it was checked: a canary skill `shade-kit-canary` in the project-level `.claude/skills/`, with `metadata.version: 9.8.7` in its frontmatter and a marker line in its body that names no version. Two headless `claude -p` runs with the built-in file and shell tools blocked (`--disallowedTools "Read,Bash,Grep,Glob,Edit,Write,NotebookEdit,WebFetch,WebSearch,Agent"`): one loaded the skill through the Skill tool, the other through the slash command `/shade-kit-canary`. The saved session transcripts were searched. The model called no tool except `Skill`; MCP tools that can read files were not blocked, but none was used. An independent verifier repeated the Skill tool run and got the same result.
  - What the model gets, on both paths: `Base directory for this skill: <dir>`, followed by the body after the closing `---`. The frontmatter is removed completely: no `---`, no `name:`, no `metadata`, no `9.8.7`. The body marker is there. `name` and `description` reach the model only through the skill listing (`- <name>: <description>`), not as YAML.
  - Two further skills loaded in the implementing session gave the same result: `implement-direct`, called by the user as a slash command, and `skill-creator`, loaded through the Skill tool.
  - The documentation agrees. The [Claude Code skills documentation](https://code.claude.com/docs/en/skills) says of `metadata`: "Free-form YAML map for your own key-value data, … read by your own tooling from `SKILL.md`. Claude Code doesn't act on its contents". It also says that "the rendered `SKILL.md` content enters the conversation as a single message", and that malformed frontmatter "loads the skill body with empty metadata". The Agent Skills overview (platform.claude.com) puts `name` and `description` from the YAML frontmatter at level 1 and the `SKILL.md` body at level 2.
  - Limits: one run per path; project-level skills only (plugin and user-level skills were not run). Copilot and Codex: see above (package 08).
  - Plugin skills (package 07, 2026-11-13, Claude Code 2.1.323, headless, plugin loaded with `--plugin-dir`): the skill listing shows the namespaced name `shade-kit:shade-kit-next-phase`, both `/shade-kit-next-phase` and `/shade-kit:shade-kit-next-phase` load the skill, and its handshake ran with the version from the text (`--expect 0.7.0`, the kit version then). **FACT** ([07](07-claude-code-integration.md#skill-names-in-claude-code)). The frontmatter of plugin skills was not checked separately.
- The model does not see the frontmatter, so the fallback applies: every skill that calls `version --expect` also states the version in the text of its handshake step. The frontmatter stays the source; both places hold the same value. **Decided** on 2026-11-04; applies since the check on 2026-11-06. This sub-feature sets the rule and the wording; 06-5, 06-6, and 06-9 write them into the handshake steps. **Decided** on 2026-11-06.
- The handshake step has this wording in every skill that calls `version --expect`. **Decided** on 2026-11-06.

  ```text
  Version handshake: run `shade-kit version --json --expect 0.8.0`; 0.8.0 is the kit version of this skill (`metadata.version` in its frontmatter).
  ```

- When the version is raised, the frontmatter of every skill and every handshake text change together: search the skills for the old value. **Decided** on 2026-11-06.
- The guard test `SkillKitVersionTests.SkillFiles_ShippedSkills_CarryOneKitVersionThatMatchesTheEngineMajorMinor` (`CreativeCoders.ShadeKit.Core.Tests/Skills/`, FP07-AC2) fails the build when a skill has no plain `x.y.z` `metadata.version`, when the skills carry different values, when their major.minor differs from the major.minor of the engine version of the build (GitVersion; patch and prerelease ignored), or when any `--expect <v>` or "`<v>` is the kit version of this skill" in a text file under `skills/` names another version. **Decided** on 2026-11-14 (package 07 review rework, Gate 5). **Implemented** ([07 Kit version guard](07-claude-code-integration.md#kit-version-guard)). The kit version the skills are compared with is the most frequent valid version; on a tie, the version with the major.minor of the engine wins, then the version of the first skill in ordinal path order; so a single skill with another version is the one reported. **Decided** on 2026-11-15 (package 07 review rework, Gate 5 round 2, finding N6; before, the first skill with a valid version in path order was the reference). **Implemented** (`SkillKitVersionTests.SelectKitSkill`).
- Raised to `0.8.0` on 2026-11-14, in the frontmatter and in every handshake text of all 19 skills, because the engine builds carry `0.8.x` since the tag `v0.8.0` and the new guard failed with `0.7.0`. **Decided** on 2026-11-14 (package 07 review rework, Gate 5).

## Acceptance criteria

Status as of 2026-11-14. There are no automated tests of the skill texts (skill conventions above), so each criterion names how it is proven; the version guard of 07 now also proves AC1 and the version part of AC3.

- **FP06-2-AC1** Every skill under `skills/` has `metadata.version` in its frontmatter, and all skills have the same SemVer value. **Implemented**: all 19 skills carry `metadata.version: 0.8.0` (`0.7.0` until 2026-11-14). Proven on 2026-11-06 by a one-off check that all values are equal, are strings, and are a plain `x.y.z`, and since 2026-11-14 by `SkillKitVersionTests.SkillFiles_ShippedSkills_CarryOneKitVersionThatMatchesTheEngineMajorMinor`. Skill-creator's `quick_validate.py` (17 of 17 valid) shows that the frontmatter stays valid; it does not read `metadata`.
- **FP06-2-AC2** The result of the visibility check for Claude Code is recorded in this package with the date and how it was checked. **Implemented**: recorded under Frontmatter visibility (2026-11-06, Claude Code 2.1.318).
- **FP06-2-AC3** If the check finds that the model does not see the frontmatter, every skill that calls `version --expect` states the same version in the text of its handshake step. Rule and wording **Decided** on 2026-11-06 (Frontmatter visibility). `shade-kit-phase-entry` (06-5) applies it since 2026-11-10 and `shade-kit-next-phase` (06-6) since 2026-11-11 (`--expect 0.7.0` in the text of their handshake steps then, `0.8.0` since 2026-11-14); `new`, `discuss`, and `drop` (06-9) since 2026-11-13. Every skill that calls `version --expect` now states the version in the text of its handshake step; since 2026-11-14 `SkillKitVersionTests` checks that every such text names the kit version.

## Open points

- **Alignment with GitVersion: checked, not written** (2026-11-04, updated 2026-11-14): the skill version is still raised by hand, which still breaks the rule in `GitVersion.yml` that no version is maintained by hand. Since 2026-11-14 `SkillKitVersionTests` ties it to the build: every build that runs the tests (pull request, `main`, `feature/**`, release tag) fails when the skills' major.minor differs from the engine's, so a release tag with stale skills fails its tag build before anything is published (19). Still open: writing the version from GitVersion (for example a Cake task, package 19), and a check before the tag is set (the `verify-tag` job does not check it; review Major, layer 2, not selected at Gate 5). The guard compares major.minor only, the rule of the handshake (06-1).
- Raising the minor: the commit that raises the skills to a new minor must also raise the minor of the build version, or the build of that commit fails the guard. `GitVersion.yml` says that a breaking change is announced with `+semver: minor` (**FACT**, `GitVersion.yml` comment); **Unverified** that a commit message with `+semver: minor` gives the build the new minor in this repository (not run).
- Initial value of `metadata.version`: answered, `0.7.0`. **Decided** on 2026-11-06. Raised to `0.8.0` on 2026-11-14 (see above). The value is raised by hand for every release whose major or minor differs.
- Exact skill names (concept open question 6).
- Phase skills are restricted to user invocation only by convention; a technical restriction (Claude Code `disable-model-invocation`, and whatever Copilot and Codex offer) is a possible later addition. Package 08 found Copilot's `disable-model-invocation` (reported to break loading a flagged skill from another skill) and Codex's `agents/openai.yaml` (an agent-specific file); v1 uses neither (2026-11-15).
