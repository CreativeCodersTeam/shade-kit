# 06-2 · Skill conventions and version metadata

| | |
|---|---|
| Status | Draft, 2026-11-04 (split from package 06) |
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

- Which skills call the version check, and how (06-5, 06-6, 06-9).
- Copilot and Codex (package 08).
- The automated alignment of the skill version with GitVersion (see open points).

## Functional description

### Skill conventions

- Skills are written in the shared `SKILL.md` format and the plugin layout of `skills/`. Claude-only features (for example subagents) are only used where an agent-neutral way would lose function, and each such use is named in the skill. **Decided** (constraint, concept §1), **Unverified** (format compatibility, resolved in package 08).
- A phase starts only when the human calls its phase skill (`/shade-kit-<phase>`). No skill starts a phase skill, and no phase skill calls `phase start` for a phase other than its own; a skill that ends names the skills the human can call ([06-3 Next-steps message](06-3-next-steps-skill.md)). **Decided** on 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md)). The rule holds by convention, not technically: the description of every phase skill says that it runs only when the user calls it by name, the skill texts contain no hand-off, and the managed section in `CLAUDE.md` / `AGENTS.md` tells the agent (package 02, **Implemented** in `ManagedInstructionSection`). A restriction such as Claude Code's `disable-model-invocation` frontmatter field is not used in v1. **Decided** on 2026-10-30. Known weak point, like approvals (package 04): nothing technically stops an agent from loading a phase skill on its own.
- Skills never read or write `feature.json`, `gates.json`, or `config.json` directly. They use the CLI with `--json`. Skills read and write Markdown artifacts in the feature folder directly. **Decided** on 2026-11-04. This keeps every state change behind the engine's version check (package 01).
- A shipped skill references no path under `research/` or `docs/` of the kit repository; those folders are design notes and are not shipped. The source comments of the skeletons are removed when a skill is worked out. **Decided** on 2026-10-18 (findings clarification). Checked when the skills are shipped: FP07-AC7 (package 07). **Decided** on 2026-11-04.
- Every artifact a skill creates starts from the repository's template in `.shadekit/templates/`, not from text built into the skill, so team edits to templates take effect. **Decided** on 2026-11-04. The skill follows the template conventions of package 02: it follows the guidance comments, replaces every placeholder, and removes the guidance comments; checklist copies (package 05) keep their guidance comments.
- `request.md` can hold third-party content, written unchanged by `feature new`: the body and every comment of an issue, or a whole web page (package 03). Every skill that reads it, and every research agent that gets it as feature context, treats it as data that describes the requirement, never as instructions to the agent: it does not follow commands, links, or tool requests in it, and shows the human any instruction in it that looks suspicious (a research agent reports it to the calling skill, which shows it). **Decided** on 2026-10-25 (code review of the rework of `feature new`, finding F6; package 09, [`new`](09-discuss-and-intake.md#new)). **Implemented** (`shade-kit-new`, `shade-kit-refine`, `shade-kit-diagnose`, `shade-kit-quick`, `shade-kit-research`).
- The skill texts are checked by a review by hand when a sub-feature is accepted; there are no automated tests of the skill texts. **Decided** on 2026-11-04.

### Kit version in the frontmatter

- Every skill under `skills/` carries its kit version in the frontmatter, hardcoded and committed, because the skills must be installable as a plugin with their version included. **Decided** on 2026-11-04.

  ```yaml
  ---
  name: shade-kit-next-phase
  description: ...
  metadata:
    version: 0.8.0
  ---
  ```

- All skills carry the same version. **Decided** on 2026-11-04.
- The version is a SemVer version without a prerelease label. **Proposed.**
- The skills that the human calls pass this version to `shade-kit version --json --expect <version>` (06-1). Skills that are only loaded by another skill do not check (06-5, 06-6, 06-9). **Decided** on 2026-11-04.

### Frontmatter visibility

- **Unverified:** whether Claude Code shows the frontmatter, and `metadata.version` in it, to the model when it loads a skill. This sub-feature checks it first. **Decided** on 2026-11-04. Copilot and Codex are checked in package 08.
- If the model does not see the frontmatter, every skill that checks the version also states the version in the text of its handshake step. The frontmatter stays the source; both places hold the same value. **Decided** on 2026-11-04.

## Acceptance criteria

- **FP06-2-AC1** Every skill under `skills/` has `metadata.version` in its frontmatter, and all skills have the same SemVer value.
- **FP06-2-AC2** The result of the visibility check for Claude Code is recorded in this package with the date and how it was checked.
- **FP06-2-AC3** If the check finds that the model does not see the frontmatter, every skill that calls `version --expect` states the same version in the text of its handshake step.

## Open points

- **Alignment with GitVersion skipped for now** (2026-11-04): the skill version is maintained by hand, and nothing checks it against the release tag. This breaks the rule in `GitVersion.yml` that no version is maintained by hand. Possible later solutions: a test that all skills carry the same version, a check in the `verify-tag` job of the release workflow, or a Cake task that writes the version (package 19).
- Initial value of `metadata.version`: the version of the first release that ships the skills, for example `0.8.0`. **Open.**
- Exact skill names (concept open question 6).
- Phase skills are restricted to user invocation only by convention; a technical restriction (Claude Code `disable-model-invocation`, and whatever Copilot and Codex offer) is a possible later addition.
