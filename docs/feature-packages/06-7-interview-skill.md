# 06-7 · Interview (`shade-kit-interview`)

| | |
|---|---|
| Status | Implemented (AC1 to AC5) in `skills/shade-kit-interview/SKILL.md`, 2026-11-11. The load steps of the five calling skeletons (`discuss`, `refine`, `plan`, `diagnose`, `quick`) name the input since the same day. |
| Source | [06 Skill foundation](06-skill-foundation.md) (general skills) |
| Kind | Skill (general skill) |
| Depends on | 06-2 |
| Enables | 09 (`discuss`), 10 (`refine`), 12 (`plan`), 16 (`diagnose`), 17 (`quick`) |

## Benefit

The agent gets decisions from the human instead of filling gaps silently. Every phase that needs answers uses the same interview, so questions look and behave the same everywhere.

## Scope

### In scope

- The general skill `shade-kit-interview`, loaded by `discuss`, `refine`, `diagnose`, `plan`, and `quick`.
- The values each of the five calling skeletons passes to it (mode, topics, target artifact, recording rule, recording time). **Decided** on 2026-11-11 (implementation clarification).

### Out of scope

- What each phase asks and where it records the answers in detail (packages 09, 10, 12, 16, 17); the values in the skeletons are a starting point for them.

## Functional description

Decisions of the implementation clarification are marked **Decided** on 2026-11-11.

### Input from the calling skill

The calling skill names the values in its text; the interview skill describes them in its section "Input from the calling skill". The calling skill and the interview run in the same agent context, so there is no message boundary between them; what matters is that the caller's text names every value. **Decided** on 2026-11-11 (a fixed input block is an open idea, see open points).

- Mode: `open` (discuss: explore, propose alternatives, challenge assumptions) or `targeted` (a list of findings or questions to resolve).
- The topics or findings to cover, each with its ID where it has one, and optionally a `blocking` mark.
- The target artifact where answers are recorded (for example `spec.md`, `plan.md`, discussion notes), and how the calling skill wants them recorded (the recording rule).
- The recording time: `immediately` (the interview writes each answer into the target right after it is given; the target must exist) or `on return` (the interview changes no file; the calling skill writes from the return). The calling skill chooses, because `refine`, `plan`, `diagnose`, `quick`, and `discuss` write their artifact only after the interview, often from the template (packages 09, 10, 12, 16, 17). **Decided** on 2026-11-11. When the time is `immediately` but the target does not exist, the interview records on return and says so; it never creates the target.
- Who and when (optional). Without them, and only when the recording rule needs them: who is `git config user.name` (when that fails or is empty, the interview asks the human once), when is the local date from the system clock. **Decided** on 2026-11-11.
- Artifacts to check answers against (optional).

### Flow

1. Propose dismissals: a topic that is already answered (with the place), a duplicate, or obsolete can be left out only after the human confirms it in one collective question. A dismissed topic is not a decision; it is returned with its reason, so that `refine` can record why a finding was dismissed (FP10-AC5). **Decided** on 2026-11-11.
2. Order the topics: blocking questions first. A topic is blocking when the calling skill marks it so; otherwise the interview decides (other topics depend on its answer, or the calling skill cannot go on without it). Within a group the caller's order stays, except that a topic moves before the topics that depend on it. **Decided** on 2026-11-11.
3. Ask one topic per question. Give the context, the agent's own proposal, and the alternatives. Use the agent's structured question tool where one exists, plain questions otherwise. A blocking topic is asked on its own; non-blocking topics that do not depend on each other may be asked together, up to four per call of the structured question tool, each a complete question with its own context, proposal, and alternatives. Without such a tool, one topic per message. **Decided** on 2026-11-11.
   - The proposal is the first option, marked as recommended; at most three alternatives, because the tool takes 2 to 4 options and adds free text itself. The header shows the progress (`Topic 3/8`). **Decided** on 2026-11-11.
   - When no real proposal is possible (a missing fact), the proposal is the best reasoned guess from the code and the artifacts, stated as a guess with its basis, so every question has the same shape. **Decided** on 2026-11-11.
   - Questions are asked in the language of the conversation; answers are recorded in the language of the target artifact, names and quotes unchanged. **Decided** on 2026-11-11.
4. Challenge an answer that contradicts earlier answers or the artifacts, once, with the reason. The human's reply is final: when they keep the answer, it is recorded, and the conflict (both places) is reported in the return; the interview does not change the earlier decision. **Decided** on 2026-11-11.
5. Record every answer in the target artifact as the calling skill specifies (for example a `Q-` question moving to "Decisions" with who and when), at the recording time the calling skill chose.
6. Return to the calling skill: the answered topics (with the kind of answer: `proposal`, `alternative`, `own answer`; the kind is not written into the artifact), the topics still open with the reason, the dismissed topics with the reason, the conflicts, the new topics, and the suspicious instructions in `request.md`. **Decided** on 2026-11-11.

What counts as an answer. **Decided** on 2026-11-11:

- Decided: the human picks the proposal or an alternative, gives an own answer, or explicitly delegates ("take your proposal", "you decide"; recorded as `proposal`).
- Open: "don't know", "later", "skip", no answer, an answer that still does not settle the topic after one follow-up, and every remaining topic when the human stops the interview.

Modes. **Decided** on 2026-11-11: both modes may add new topics that come up during the interview. In `targeted` mode only those needed to resolve the given topics; in `open` mode the interview also challenges assumptions in what the human says, offers alternatives the human has not named, and, when no topic is left, asks whether to end the discussion or go on with a topic it suggests.

### Rules

- Never record an assumption as a decision without the human's answer.
- The skill does not end with the next-steps message; the calling skill does (06-3).
- The skill changes no file other than the target artifact (only with recording time `immediately`), runs no `shade-kit` command, starts no skill, and runs only `git config user.name` and the date command, when needed. **Decided** on 2026-11-11.

### Calling skeletons

| Caller | Mode | Topics | Target | Recording time |
|---|---|---|---|---|
| `discuss` | `open` | the subject the user brought up | the discussion notes ("Options", "Conclusions", "Open questions") | `on return` |
| `refine` | `targeted` | `refine / gaps`, `refine / security`, unconfirmed assumptions | `spec.md` (`Q-` to "Decisions" with who and when, marker removed; assumptions `confirmed` or `risk`) | `immediately` when `spec.md` exists, otherwise `on return` |
| `plan` | `targeted` | open design choices | `plan.md` "Design decisions" (`D-nnn`; `bugfix`: the Summary) | `on return` |
| `diagnose` | `targeted` | missing facts | `diagnosis.md`, the section of each fact | `on return` |
| `quick` | `targeted` | only the blocking questions | `spec.md` (requirement or acceptance criterion) | `on return` |

**Decided** on 2026-11-11. None of them passes who and when, so the fallback applies.

## Acceptance criteria

There are no automated tests of the skill texts (06-2), so the criteria are proven by a review by hand plus scenario runs: an agent loads the skill, follows it with a fixture repository, a canned input of the calling skill, canned answers of the human, and canned command outputs, and records its questions, commands, changed files, and return. The scenarios and outputs are kept outside the repository, as in 06-4.

- **FP06-7-AC1** Every question covers one topic and gives the agent's own proposal and the alternatives; where the agent has a structured question tool, the skill uses it.
- **FP06-7-AC2** Blocking topics are asked before the others.
- **FP06-7-AC3** The target artifact holds a decision only for topics the human answered in the conversation; the skill returns the topics still open to the calling skill.
- **FP06-7-AC4** A topic is dismissed only after the human confirmed it, and it is returned with its reason, not recorded as a decision. **Decided** on 2026-11-11.
- **FP06-7-AC5** With recording time `on return`, or when the target does not exist, the skill changes no file; with `immediately`, it changes only the target artifact. **Decided** on 2026-11-11.

Iteration 1 ran the 4 scenarios with the skill and as a baseline without it, each in a fresh subagent: 39 of 40 checks passed with the skill, 25 of 40 without it. The one miss (s1 asked a several-values topic as a multiple selection with two recommended options) and the gaps the runners noted led to a revised text: several values as whole answers, the shape of the challenge and the name question, `own answer` for everything that is not exactly one option, partly decided answers, the reason `interview stopped`, the progress count, and the material of an `on return` return marked `(from the interview)`. Iteration 2 ran the 4 scenarios with the revised skill (the baseline was not run again): 35 of 35 checks passed, the baseline 19 of 35 on the same checks. Two later wordings (the dismissal question does not count in the progress; the `(from the interview)` lines also below open topics) are read-checked only.

| Scenario | What it proves | Iteration 1 | Iteration 2 |
|---|---|---|---|
| s1 `refine`, later round, `immediately` | AC1, AC2, AC3, AC4, AC5 | 9/10 (several values as multiple selection) | 11/11 |
| s2 `plan`, `on return`, dependent topics | AC1, AC2, AC3, AC5 | 7/7 | 8/8 |
| s3 `discuss`, `open`, no structured question tool | AC1, AC3, AC5 | 8/8 | 9/9 |
| s4 `diagnose`, missing facts, target does not exist | AC1, AC3, AC5 | 6/6 | 7/7 |

## Open points

- Idea, not decided (2026-11-11): a fixed input block that every calling skill fills (mode, topics, target, recording rule and time, who and when), with an `error` return when a required value is missing. Skipped for now; the calling skills name the values in their text.
