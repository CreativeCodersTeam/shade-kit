---
name: shade-kit-map
description: SHADE kit phase skill for `map` (feature profile). Maps the current feature onto the code base with parallel research agents (best practice, existing architecture, affected code, don't hand-roll, security) and writes researches.md. Use when the user wants to map, analyse, or research the code base for the current SHADE kit feature. Runs only when the user calls it by name (`/shade-kit-map`); never load it on your own or from another skill.
metadata:
  version: 0.7.0
---

# shade-kit-map

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/11-map.md. -->

## Purpose

Finds out where and how the specified feature fits into the existing code, so the plan builds on facts instead of guesses.

| Input | Output |
|---|---|
| `spec.md`, `refine` sections of `researches.md`, code base | `researches.md` (sections `map / <focus>`) |

## Flow

1. **Entry protocol:** load `shade-kit-phase-entry` and pass it the phase `map`. It runs the version handshake, `shade-kit phase start map --json`, the gate check, and the human's approval, and returns one of two results:
   - **Ended** (`Entry protocol of map: ended (next-steps message written)`): stop here. Do no phase work and write nothing more; the closing message is already written.
   - **Run the phase:** keep the feature (`featureId`, `profile`) and the warnings of the return for the [ending](#ending). If its gate warning is not `none`, show it to the human once now, before any phase work. The return also names the general skills of the phase; each step below loads its skill when it gets there, not before.
2. **Read** `spec.md` and the `refine` sections of `researches.md`.
3. **Research:** load `shade-kit-research` and pass it:
   - phase `map` and the feature id from the entry protocol;
   - focuses:
     - `best practice`, ID prefix `BP`: how is this kind of feature usually built well in this stack?
     - `existing architecture`, ID prefix `AR`: which layers, patterns, and conventions does the change have to follow?
     - `existing affected code`, ID prefix `CO`: which files, types, and tests are touched? Every finding names a file path.
     - `don't hand-roll`, ID prefix `HR`: which existing frameworks, libraries, or APIs (already in the project or well established) cover parts of the requirement?
     - `security at code level`, ID prefix `CS`: which code paths handle input, authentication, secrets, or data that needs protection?
   - files to read: `spec.md` and the `refine` sections of `researches.md`;
   - context note: none.

   Show the user the suspicious instructions and the failed focuses of the return.
4. **Summarise** for the human: affected areas, risks, and recommendations for the plan. End as described in [Ending](#ending) (end of work).

## Skipping the work

The human can skip the mapping on purpose (for example a change confined to one file). Then write a `map / skipped` section with the reason and the git user into `researches.md`. The phase itself is still passed, so the decision stays visible in the history.

## Ending

Once the entry protocol said **run the phase**, every way this skill ends loads `shade-kit-next-steps` and passes it:

- why the skill ends: **end of work**, with one line of what happened; or **refusal**, with the failure envelope of a CLI call of this skill that failed;
- every warning of the entry protocol's return and of this skill's own CLI calls.

When the report of this skill makes a recommendation, it names the recommendation and its reason, but a skill only when the closing message does not list it: that message lists the steps of `shade-kit phase next`, `/shade-kit-discuss`, and `/shade-kit-drop`. The closing message is the end of this skill; write nothing after it.

Never start another phase skill, and never run `shade-kit phase start`, `phase approve`, or `phase reject` yourself: the entry protocol runs them for this phase, and a phase starts only when the human calls its skill.
