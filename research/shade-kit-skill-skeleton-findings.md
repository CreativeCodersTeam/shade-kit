# SHADE kit skill skeleton findings

Status: 2026-10-17. Findings from creating the skill skeletons in `skills/` from [shade-kit-skills.md](shade-kit-skills.md). Design notes only; nothing here is shipped.

## What exists

One `skills/<name>/SKILL.md` per skill with frontmatter (`name`, `description`), purpose, input/output, and flow. No details yet.

- Phase skills: `shade-kit-next-phase`, `shade-kit-discuss`, `shade-kit-new`, `shade-kit-refine`, `shade-kit-map`, `shade-kit-diagnose`, `shade-kit-plan`, `shade-kit-implement`, `shade-kit-quick`, `shade-kit-verify`, `shade-kit-finish`, `shade-kit-drop`.
- General skills: `shade-kit-interview`, `shade-kit-gate-check`, `shade-kit-research`, `shade-kit-review`. Each names the input it expects from the calling skill and what it returns.

## Findings to resolve

1. **Package 06 contradicts `research/shade-kit-skills.md`.** `docs/feature-packages/06-skill-foundation.md` still describes a router skill, a separate report skill, and a router that runs the gate dialogue. The skeletons follow the newer `shade-kit-skills.md`: no router, report inline in `shade-kit-finish`, entry protocol in every phase skill. Package 06 needs an update. **Resolved 2026-10-18:** package 06 describes `shade-kit-next-phase` and the entry protocol.
2. **Version handshake added to the entry protocol.** The entry protocol in `shade-kit-skills.md` does not list `shade-kit version --json`, but concept §9.6 (formerly ADR 0001) and package 06 require it before every skill. Every skeleton runs it as its first step. **Resolved 2026-10-18:** the entry protocol in `shade-kit-skills.md` lists it as step 1.
3. **No entry protocol in `new`, `discuss`, and `drop`.** `shade-kit-skills.md` says every phase skill runs it, but before `feature new` there is no feature to start a phase for, and `discuss` and `drop` are not phases of any profile (concept §5.4).
4. **`quick` and `implement` share the `implement` phase.** Both skeletons hand off to each other based on the profile. Package 04 says the engine maps every phase per profile to a skill; if `phase start` returns `shade-kit-quick` for the `quick` profile, the hand-off in the skills is only a safeguard. **Resolved 2026-10-18:** `quick` is a phase of its own (`new` → `quick` → `finish`); the hand-offs are removed.
5. **Most CLI commands do not exist yet.** FACT (checked in `src/`): only `shade-kit version` and `shade-kit features list` (plural group) exist. The concept uses the singular `feature current|set|new|show|drop`. The naming has to be made consistent. Every skeleton notes in a comment that the other commands are planned. **Resolved 2026-10-18:** the group is renamed to `feature` in the code (`feature list`).
6. **`shade-kit phase next --json` is missing** (follow-up 1 in `shade-kit-skills.md`). `shade-kit-next-phase` uses it as planned.
7. **`verify` also writes `verification.md`.** Package 14 defines it; the table in `shade-kit-skills.md` only names `playbook.md`.
8. **Source comments point to `research/` and `docs/`.** Each `SKILL.md` has an HTML comment with its source packages as a pointer for the detailed work. Shipped skills must not depend on `research/` (`CLAUDE.md`), so remove these comments when the skills are worked out.

## Agent-neutral choices

- The frontmatter only uses `name` and `description`. The Claude Code field `disable-model-invocation` (user-only phase skills) is left out on purpose; decide when the skills are worked out.
- Subagents are only used where the agent supports them (`shade-kit-research`, `shade-kit-review`, parallel tasks in `shade-kit-implement`), with a sequential fallback. **Unverified:** subagent support in GitHub Copilot and OpenAI Codex CLI (concept open question 5).

## Next steps

- Work out each skill in detail; the `skill-creator` skill helps with descriptions and evals.
- Define the research agents that `shade-kit-research` depends on (concept §3.3).
- Create the templates the skills refer to (`discussion.md`, `request.md`, `spec.md`, `plan.md`, `tasks.md`, `review.md`, `verification.md`, `playbook.md`, `report.md`, `researches.md`, checklists).
