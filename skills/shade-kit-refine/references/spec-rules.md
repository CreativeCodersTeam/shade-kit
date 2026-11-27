# spec.md rules

`shade-kit-refine` reads this file before it writes `spec.md` and applies it in every round. The repository's template `.shadekit/templates/spec.md` gives the form of a new file. These rules say what goes into each section. They matter most in later rounds: the guidance comments of the template are removed when the file is written, so the file itself no longer tells you its rules, and the Definition of Ready checks the file against them before `plan` starts.

## Sections

`spec.md` has these sections, as `## ` headings in this order. Every section is present, also when it holds nothing; the rule says what an empty section holds.

| Section | What it holds |
|---|---|
| Goal | One short paragraph: which problem the feature solves and for whom. |
| Glossary | The domain terms and key entities the requirements use, as a table `Term \| Kind \| Meaning`, kind `term` or `entity`. An entity names its key attributes and relations, without implementation details. Empty: `None.` |
| Requirements | One `### ` subsection per requirement (see [requirements](#requirements)). |
| Edge cases | Boundary conditions, error scenarios, and unusual inputs, each `- <edge case> → AC-nnn.m` or `- <edge case> → out of scope` (then it is listed in "Out of scope" too). Empty: `None.` |
| Security considerations | See [security considerations](#security-considerations). |
| Out of scope | What the feature does not do, including the "won't" requirements. Empty: `Nothing excluded.` |
| Assumptions | See [assumptions](#assumptions). Empty: `None.` |
| Open questions | See [questions](#questions). Empty: `None.` |
| Decisions | See [questions](#questions). Empty: `None.` |
| Changes | See [changes](#changes). |

The spec says what is needed and why. How it is built belongs to `plan.md`; leave out class names, libraries, and file layouts unless the requirement itself is about them.

## IDs

| Kind | Form | Example |
|---|---|---|
| Requirement | `REQ-nnn`, three digits, from `REQ-001` | `REQ-004` |
| Acceptance criterion | `AC-<requirement number>.<n>`, from `.1` within its requirement | `AC-004.2` |
| Assumption | `A-nnn`, from `A-001` | `A-002` |
| Question | `Q-nnn`, from `Q-001` | `Q-007` |

A new ID is the highest existing number of its kind plus one. Count every ID in the file, also removed requirements and criteria, and for `Q-` both "Open questions" and "Decisions". Never renumber an ID and never reuse one: `tasks.md`, the tests, and the report refer to them, and a renumbered ID silently points at something else.

## Requirements

```markdown
### REQ-001 – <title>

- Priority: must
- Status: active
- Rationale: <why this is needed>

<requirement statement>

Acceptance criteria:

- **AC-001.1** Given <context>, when <action>, then <result>.
- **AC-001.2** <one verifiable statement>
```

- **Priority**: `must`, `should`, or `could` (MoSCoW). A "won't" is not a requirement; it goes into "Out of scope".
- **Status**: `active`, or `removed (<reason>)`.
- **Acceptance criteria**: at least one per requirement, listed under it. Given/When/Then for behaviour; one verifiable statement for a non-functional criterion (performance, security, compatibility). Each criterion can be decided by an automated test, or ends with `(manual: <reason>)`; manual criteria get a playbook item in `verify`.
- **Meaning.** A requirement keeps its meaning for its whole life, because everything that refers to its ID means that meaning. Change it in place when it stays the same requirement: better wording, a priority, a criterion added, changed, or removed. When an answer turns it into a different requirement (for example "password login for guests" into "guests sign in through Entra ID"), mark the old one as removed and add the new one with a new ID. The same holds for a criterion.
- **Removing.** A requirement is never deleted. Set its status to `removed (<reason>)` and keep its heading, statement, and criteria unchanged, so every reference to it stays valid. A single criterion of an active requirement is removed by appending `(removed: <reason>)` to its line. You may mark a requirement or criterion as removed yourself when the request, the notes, or an answer make it obsolete; the review of the round puts every such removal to the human, and a removal the human rejects goes back to `active`.

## Security considerations

Every finding of the section `refine / security` in `researches.md` appears here, in your words (finding IDs such as `RS-2` change with every research run, so do not cite them), unless it has a `Dismissed:` note. A security finding dismissed as a duplicate of a `refine / gaps` finding still appears: the point it names is a security point, whichever of the two findings was kept.

- `- <finding> → REQ-nnn` or `→ AC-nnn.m`: the requirement or criterion that handles it; several of them separated by commas (`→ REQ-005, REQ-006`) when the finding is handled in more than one place;
- `- <finding> → open: Q-nnn`: no requirement or criterion handles it yet; the question asks how it is handled.

A point that an answer has made obsolete (the thing it is about no longer exists, for example the passwords of a password login that was dropped) is left out, and its finding in `researches.md` gets `Dismissed: obsolete, <reason>.` appended (the reason without finding IDs), so that the next research run does not report it again.

When `researches.md` has no `refine / security` section because its focus failed, nothing was researched, so do not write `None identified`. Add the open question `- **Q-nnn** (spec) Which security points does the requirement raise? The security research of <date> failed.` and the line `- The security research is missing → open: Q-nnn`. A later round whose security research runs asks this question together with its findings.

When the research found nothing, write `None identified: <reason>`, for example `None identified: the request handles no personal data, roles, or credentials (refine / security, 2026-11-26).`

## Assumptions

`- **A-nnn** <assumption> (<status>)`, status `unconfirmed`, `confirmed`, or `risk`.

Add an assumption when you fill in a default where the request and the answers are silent and the spec cannot go on without one, for example a limit, a format, or who may do something. It starts as `unconfirmed`. Only the human's answer in the interview changes its status: `confirmed`, or `risk` when the human keeps it knowing it may be wrong. When the human corrects an assumption, replace its text with the corrected statement, set it to `confirmed`, and change the requirements that relied on it to match; the ID stays.

## Questions

- **Open question:** `- **Q-nnn** (REQ-nnn, REQ-mmm) <question>`, with the requirements it affects, or `(spec)` when it affects no single requirement. Mark the unclear statement inline with `(open: Q-nnn)` right after it: in a requirement statement, a criterion, an edge case, or a glossary entry. When the question is about something the spec does not say yet (a missing case), put the marker at the end of the statement of the requirement it affects.
- **Decision:** `- **Q-nnn** (REQ-nnn, REQ-mmm) <question> → <answer> (<decided by>, <date>)`, with the requirements the question affected and the ones the answer led to (for example a removed requirement and the one that replaces it), or `(spec)`. An answered question moves from "Open questions" to "Decisions" with the same ID, and every `(open: Q-nnn)` marker for it is removed. Keep "Decisions" in ID order. In a later round, where the interview records each answer right away, a decision may be recorded with `(spec)` because the requirement it leads to does not exist yet; put that requirement's ID in its place in the same round, once it is written; the question, the answer, who, and when stay as recorded.
- **From findings:** a finding of `refine / gaps` or `refine / security` that the interview leaves open (deferred, not known, skipped, not decided, or not asked because the interview stopped) becomes an open question with the next free `Q-` ID, unless an open question already asks the same; then keep that one. A dismissed finding becomes no question: its reason is in `researches.md`.
- **Numbering:** findings that get a new `Q-` ID get it in the order in which the interview asked them, decided and open ones alike; a topic that was not asked (because the interview stopped) comes after the asked ones.
- Only the human's answers become decisions. Your proposal is a decision only when the human accepted it.

## Changes

The last section. One line per round that wrote the spec, oldest first; a round appends its line and changes no earlier one:

- the round that creates the file: `- <date>: created`;
- a later round: `- <date>: added <IDs>; changed <IDs>; removed <IDs>`, with only the parts that apply and only `REQ-` and `AC-` IDs. A requirement that is added or removed is named by its `REQ-` ID alone; its criteria go with it. A criterion that is added, changed, or removed in a requirement that stays is named by its `AC-` ID; a requirement that stays is named as changed only when its title, priority, or statement changed. A status set to `removed` counts as removed, a removal undone as changed. Changes to the other sections (goal, glossary, edge cases, and so on) are not listed: later phases trace requirements and criteria;
- a later round that changed no requirement or criterion: `- <date>: no requirement or criterion changed`.

Later phases compare these dates with their own artifacts after `refine` was reopened, so name every changed ID. A round writes its line at its end, when it knows what it changed; until then a new file holds the heading of the section only. When an existing `spec.md` has no "Changes" section (it was written before the section existed, or by another phase), add the section at the end with the line of this round; do not reconstruct earlier rounds.

## Writing the file

- **New file** (the first round): start from `.shadekit/templates/spec.md`. Keep its line ending and encoding. Replace every `{{placeholder}}`, remove every guidance comment `<!-- … -->` and the empty line it leaves (no two empty lines in a row), and replace the example lines of a section you have nothing for with what an empty section holds. No `{{` and no `<!--` may be left: the Definition of Ready fails on template leftovers.
- **Existing file** (later rounds): edit it in place and change only the lines that this round changes. Never write the whole file anew, so that its line endings, its encoding, and the edits people made by hand stay as they are.
- Write in English, the language of the kit's templates, whatever language the conversation uses; keep names, identifiers, and quoted text unchanged.
- The title line is `# Spec: <feature title>`, and the feature line below it stays in the template's form: ``Feature: `<id>` · Request: [request.md](request.md) · Research: [researches.md](researches.md)``.
