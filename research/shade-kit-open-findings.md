# SHADE kit open consistency findings

Status: 2026-10-18, all findings fixed in the clarification on 2026-10-18 (marked "fixed" below). Findings from the consistency review of `README.md`, [shade-kit-concept.md](shade-kit-concept.md), [shade-kit-skills.md](shade-kit-skills.md), the [feature packages](../docs/feature-packages/README.md), and the skill skeletons that are not resolved yet. Design notes only; nothing here is shipped.

The numbers are the ones from the review, so they can be traced. Resolved on 2026-10-18 and therefore not listed: 1 (entry protocol re-entry and rejection), 2 (router), 3 (ADR 0001 moved into concept §9), 4 (`quick` phase), 5 (`feature drop` confirmation), 11 (`status` in `feature current`), 13 (seven research agents), 20 (`feature` command group), 21 (**Implemented** label), 22 (package 19), 23 (README), 26 (version handshake in the entry protocol), 28 (approval off with a checklist only warns).

## Contradictions between packages

### 6. The short `bugfix` plan has no test strategy — fixed

- **Finding:** [16](../docs/feature-packages/16-bugfix-profile.md) line 45 limits the short `plan.md` to "the chosen fix option and why, risks". The test strategy with the quality commands is missing. FP12-AC6, [13](../docs/feature-packages/13-implement.md) ("Quality commands"), [14](../docs/feature-packages/14-verify.md) (coverage command from `plan.md`), and DOD-01 in [05](../docs/feature-packages/05-gates-and-checklists.md) all take the commands from it.
- **Proposal:** the short plan keeps Summary, Test strategy (with commands), and Risks; every other section says "n/a in bugfix".
- **Fixed (2026-10-18):** Short `plan.md` in `bugfix` fills Summary (fix option and why), Test strategy with commands, Risks, and Deviations; every other section says "n/a in bugfix". Deviations stays because `implement` and DOD-07 use it. Changed: 16 (+FP16-AC8), `shade-kit-plan`, `plan.template.md`.

### 7. DoD in the `quick` profile — fixed

- **Finding:** FP17-AC3 marks only the playbook item `n/a`. [05](../docs/feature-packages/05-gates-and-checklists.md) line 125 also lists DOD-04 (`tasks.md`) and DOD-07 (`plan.md`) as `n/a` in `quick`. DOD-01 refers to "the quality commands from `plan.md`". By the `n/a` rule it would be `n/a` in `quick` as well, so build, test, and lint would never be gated. The self-check of the `quick` phase (package 17) does not mention coverage, which DOD-02 requires.
- **Proposal:** change DOD-01 to "the approved quality commands (`plan.md`, or `review.md` where the profile has no plan)". Change FP17-AC3 to "DOD-04, DOD-07, and DOD-09 are `n/a`". Add the coverage measurement to the self-check in package 17.
- **Fixed (2026-10-18):** DOD-01 takes the commands recorded in `review.md`. In `quick`, DOD-04 and DOD-07 are `n/a`; `quick` writes `playbook.md` only for manual criteria, so DOD-09 is `n/a` only without a playbook. The self-check measures coverage. Changed: 05, 17, DoD template, `shade-kit-quick`.

### 8. DOR-10 and DOR-09 in the `bugfix` profile — fixed

- **Finding:**
  - DOR-10: [05](../docs/feature-packages/05-gates-and-checklists.md) line 125 calls it `n/a` in `bugfix`. [16](../docs/feature-packages/16-bugfix-profile.md) line 40 says it passes on the "affected code" section of `diagnosis.md`, and FP16-AC3 calls it `n/a` again. `n/a` means "not checked", a pass means "checked with evidence".
  - DOR-09: it requires the "Security considerations" section, but `diagnose` has no security research and fills that section nowhere.
- **Proposal:** in `bugfix`, DOR-10 is a pass with `diagnosis.md` as evidence; adjust package 05 and FP16-AC3. Add a `diagnose` step that fills "Security considerations".
- **Fixed (2026-10-18):** DOR-10 reworded (`map` done, skipped on purpose, or `diagnosis.md` lists the affected code); passes in `bugfix`. `diagnose` runs the code-level security research into `diagnose / security`, which fills "Security considerations". Changed: 05, 16 (+FP16-AC7), DoR and spec templates, `shade-kit-diagnose`, `shade-kit-plan`, concept §3.3.

### 9. Report skill, review skill, interview users — fixed

- **Finding:**
  - [06](../docs/feature-packages/06-skill-foundation.md) still lists a general Report skill (line 84), and [15](../docs/feature-packages/15-finish-and-report.md) line 34 writes the report "with the report skill (package 06)". [shade-kit-skills.md](shade-kit-skills.md) keeps the report inline in `shade-kit-finish`.
  - The general skill `shade-kit-review` (used by `implement` and `quick`) is missing from package 06.
  - Package 06 does not list `quick` as a user of the interview skill.
  - The packages README describes package 06 as "shared interview, gate, research, report skills".
- **Proposal:** in package 06, replace Report with Review. Package 06 owns the review skill; package 13 owns the `review.md` format. Add `quick` to the interview users. In package 15 step 3, write the report inline. Update the packages README row.
- **Fixed (2026-10-18):** The report skill stays a general skill but is optional: `report` in `config.json` (`ask` default, `always`, `never`); new skeleton `shade-kit-report`. The review skill is owned completely by package 13 and listed in 06. `quick` added to the interview users, `diagnose` to the research users. Changed: 02, 06, 13, 15, 16, 17, README, `shade-kit-skills.md`, `shade-kit-finish`.

### 10. No CLI command for the possible next steps — fixed

- **Finding:** `shade-kit-next-phase` needs the possible next steps (planned `phase next --json`, follow-up 1 in [shade-kit-skills.md](shade-kit-skills.md), open point in package 06). Neither concept §3.2 nor package 04 has the command. Without it, the skill derives the steps from the profile and duplicates engine logic.
- **Proposal:** add `phase next --json` to package 04 and concept §3.2. It returns `continue`, `forward`, `reopen[]`, each with its gate information, matching the table in package 06.
- **Fixed (2026-10-18):** `phase next --json` added to 04 and concept §3.2 with `current`, `continue`, `pending`, `forward`, `reopen`; a `done` feature returns only `current`. Changed: 04 (+FP04-AC14), 06 (+FP06-AC12), `shade-kit-next-phase`, `shade-kit-skills.md`.

### 12. What is a phase in `shade-kit-skills.md` — fixed

- **Finding:** [shade-kit-skills.md](shade-kit-skills.md) line 7 says "one skill per phase, including the thin phases `new`, `drop`, and `finish`", and the "Phase skills" table also lists `shade-kit-next-phase`, `shade-kit-discuss`, and `shade-kit-drop`. Concept §5.4 and package 04 say `discuss` and `drop` are not phases. The exception for `shade-kit-new` (no entry protocol) is documented since 2026-10-18; the categories are not.
- **Proposal:** split the skills into phase skills (`new`, `refine`, `map`, `diagnose`, `plan`, `implement`, `quick`, `verify`, `finish`) and workflow skills (`next-phase`, `discuss`, `drop`), and remove `drop` from the list of phases in line 7.
- **Fixed (2026-10-18):** `shade-kit-skills.md` splits phase skills from workflow skills (`next-phase`, `discuss`, `drop`; no entry protocol, only the version handshake); line 7 no longer lists `drop`.

## Concept not updated

The packages decided these points, but the concept still says otherwise. Proposal for all of them: confirm or reject the list "Changes and additions to the concept that need confirmation" in the [packages README](../docs/feature-packages/README.md), then update the concept in one pass and empty the list.

### 14. `gates.json` shape — fixed

- **Finding:** concept §5.2 (around line 240) keys `gates.json` by phase and switches off approval for `verify`. That also contradicts §5.2 itself (line 227: "the human **always** approves"). [05](../docs/feature-packages/05-gates-and-checklists.md) keys it by profile and needs approval for `verify`.
- **Proposal:** take the shape and defaults from package 05.
- **Fixed (2026-10-18):** Concept §5.2 states the rule (keyed by profile, then target phase; every transition needs approval except into the `quick` phase; missing entry needs approval) and links package 05 instead of an own JSON example.

### 15. Feature folder tree — fixed

- **Finding:** the tree in concept §4.1 lacks `discussions/` (in the kit root and in the feature folder), `diagnosis.md`, `verification.md`, and `report.md`. It still lists `data.md` (line 150). `review.md` and `playbook.md` are still "proposed".
- **Proposal:** take the artifact table from the packages README. Remove `data.md`, or keep it as open question 1 without listing it in the tree.
- **Fixed (2026-10-18):** Concept §4.1 tree has `discussions/` (kit root and feature), `diagnosis.md`, `verification.md`, optional `report.md`; `review.md` and `playbook.md` are no longer "proposed". `data.md` stays in the tree as "open; not created in v1"; open question 1 stays.

### 16. CLI commands and integration content — fixed

- **Finding:** concept §3.2 lacks `feature show` (package 03), `phase complete` (04), and `phase next` (see 10). Concept §3.1 lists "hooks, MCP config" as integration content; [07](../docs/feature-packages/07-claude-code-integration.md) ships neither in v1.
- **Proposal:** add the three commands to §3.2 and note in §3.1 that hooks and MCP configuration come later.
- **Fixed (2026-10-18):** Concept §3.2 lists `feature show`, `phase complete`, and `phase next`; §3.1 says hooks and MCP configuration come later.

### 17. Open questions that the packages answered — fixed

- **Finding:** concept §5.1 still calls the storage of `discuss` output "open", and §10 still lists questions 2 (`discuss` storage) and 4 (switch off approval or only the checklist) as open. Packages 09 and 05 adopted the concept's own proposals.
- **Proposal:** after confirmation, mark both as decided in §5.1 and §10.
- **Fixed (2026-10-18):** Concept §5.1 and §10 questions 2 and 4 are marked **Decided** (numbers kept); README open-questions table updated.

### 18. Obsolete entry in the packages README — fixed

- **Finding:** the sequence in concept §11.4 now ends in `phase reject` and "continue in map". Item 10 of the confirmation list in the packages README still describes the old "stay in refine".
- **Proposal:** remove item 10 from the list.
- **Fixed (2026-10-18):** The whole confirmation list (items 1–9) was confirmed and taken over into the concept and `CLAUDE.md`; the list is empty, item 10 removed with it. The matching package labels are **Decided**.

### 19. No return path from `verify` in the phase flow diagram — fixed

- **Finding:** concept §11.1 shows no way back from `verify` to `implement`. [14](../docs/feature-packages/14-verify.md) sends a failed verification back to `implement` through a reopen.
- **Proposal:** add a dashed edge `verify -. "failures, reopen" .-> impl` to the diagram.
- **Fixed (2026-10-18):** Concept §11.1 has the dashed edge `verify -. "failures, reopen" .-> impl` and a sentence that any phase can reopen an earlier one with an approval.

## `CLAUDE.md`

### 24. v1 scope in `CLAUDE.md` — fixed

- **Finding:** in `CLAUDE.md` (lines 20–23):
  - The phases are "spec → plan → implement → verify → review"; concept §2 asks for them to be replaced.
  - "Quality gates … must pass before a phase can complete": in v1 the agent runs the checks and reports, and the CLI only enforces them later (concept §2 and §6).
  - "Installs … the kit's skills in a target repository": skills are installed at user level, never in a repository (concept §9.6, package 02).
  - The deliverables describe the plugin (`skills/`) as "skills, agents, hooks, and commands"; package 07 ships no hooks in v1.
- **Proposal:** update the v1 scope and the deliverables once the concept is accepted.
- **Fixed (2026-10-18):** `CLAUDE.md` deliverable 1 and the v1 scope describe the three profiles, checklist gates run by the agent, the optional report, and the user-level install; the note in concept §2 is removed.

## Smaller points

### 25. `verify` row in `shade-kit-skills.md` — fixed

- **Finding:** the `shade-kit-verify` row (line 40) names only `playbook.md`, but package 14 and the skeleton also write `verification.md`.
- **Proposal:** add `verification.md` to the row.
- **Fixed (2026-10-18):** The `shade-kit-verify` row names `verification.md` and `playbook.md`.

### 27. Source of "Decided" labels — fixed

- **Finding:** since 2026-10-18 the label definition allows "a later interview or review named with its date", and the former ADR decisions are traceable through concept §9. Several **Decided** items in the packages still have no named source, for example the v1 content of `config.json` in package 02.
- **Proposal:** add the source to every **Decided** item that is not in the concept, for example "**Decided** (package interview 2026-10-14)", or downgrade it to **Proposed**.
- **Fixed (2026-10-18):** Every bare **Decided** in the packages now names its source: a concept section, "implemented in code", or the clarification on 2026-10-18. Not covered: the v1 content of `config.json` in 02, downgraded to **Proposed** except the fields decided in concept §9.1–9.2 and `report`. Rule used: a concept statement without a **Proposed**, **Open**, or **Unverified** label counts as decided, because the concept is the interview result.

## New points from the fixes on 2026-10-18

### N1. Two engine rules need confirmation — fixed

Package 04 got two rules so that the entry protocol has no dead ends. Both are marked **Proposed**:

- `phase start <current>` while a transition is pending has the same effect as `phase reject <pending>`: the human keeps working on the current phase.
- A repeated `phase start <next>` while the transition to `<next>` is already pending returns the same gate again and adds no history entry.
- **Fixed (2026-10-18):** `phase start <current>` while a transition is pending now changes nothing and reports it; the phase skill asks the human and calls `phase reject` only after a yes (**Decided**, FP04-AC13, FP06-AC11, entry protocol of every phase skill, concept §3.2). The repeated `phase start <next>` rule is **Decided**.

### N2. Unverified facts in package 19 — fixed

- What the Cake task `creategithubrelease` writes into the release notes today. This decides how much of FP19-AC6 is already met.
- Whether the branch protection of `main` blocks a merge when the pull request build fails (FP19-AC1).
- **Fixed (2026-10-18):** Checked: `CreateGitHubRelease` (`CreativeCoders.CakeBuild` 6.10.0) takes `ReleaseBody` from `build/BuildContext.cs`, which is the fixed text `SHADE kit Release`, so FP19-AC6 is not met. `main` has no classic branch protection; ruleset 25112099 has a `required_status_checks` rule with an empty list, so a failing PR build does not block the merge (FP19-AC1 not met). Package 19 records both and the step to add `ubuntu-latest`, `windows-latest`, `macos-latest` as required checks. GitHub settings were not changed.

### N3. Remaining skeleton findings — fixed

From [shade-kit-skill-skeleton-findings.md](shade-kit-skill-skeleton-findings.md):

- Finding 3 (no entry protocol in `new`, `discuss`, `drop`) is partly covered by the `shade-kit-new` exception in `shade-kit-skills.md`; the categories are finding 12 above.
- Finding 6 (`phase next`) is finding 10 above.
- Finding 7 (`verification.md`) is finding 25 above.
- Finding 8: every `SKILL.md` has an HTML comment pointing to `research/` and `docs/`. Shipped skills must not depend on `research/` (`CLAUDE.md`), so these comments must go when the skills are worked out.
- **Fixed (2026-10-18):** Skeleton findings 3, 6, 7 resolved through 12, 10, 25; finding 8 through a convention and FP06-AC13 in package 06 (no shipped skill references `research/` or `docs/`; comments go when a skill is worked out). All marked in `shade-kit-skill-skeleton-findings.md`.
