---
name: shade-kit-discuss
description: SHADE kit free brainstorming on any topic (an idea, a design question, a problem), with or without a current feature. Writes discussion notes that can feed a new feature or a refine round. Use when the user wants to discuss, brainstorm, or think something through in a SHADE kit repository. Runs only when the user calls it by name (`/shade-kit-discuss`) or asks for it; never load it on your own or from another skill.
metadata:
  version: 0.7.0
---

# shade-kit-discuss

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/09-discuss-and-intake.md. -->

## Purpose

Open brainstorming outside every profile, possible at any time and in any phase. The result is a set of discussion notes. This skill never changes the feature state.

Keep every `warnings` entry (`{ code, message }`) of every CLI call of this run. Do not show them on the way; the closing message shows each one once.

## Flow

1. Version handshake: run `shade-kit version --json --expect 0.7.0`; 0.7.0 is the kit version of this skill (`metadata.version` in its frontmatter).
   - `ok` is `true`: continue. A warning `engine-prerelease-version-mismatch` means that a prerelease engine is used although its major or minor version differs; keep it like every warning and continue.
   - `ok` is `false` (for example `version-mismatch`), or the shell reports that `shade-kit` is not found: stop before any other CLI call. End ([Ending](#ending)) with **refusal** and the failure envelope, or with "`shade-kit` not found".
2. **Context:** `shade-kit feature current --json` to find out whether a feature is current. No feature is needed. A failure envelope (for example `kit-root-not-found` without `.shadekit/`, where the notes would be written): end with **refusal** and the envelope, before the discussion.
3. **Discuss:** load `shade-kit-interview` and pass it:
   - mode `open`: ask, propose alternatives, and challenge assumptions;
   - topics: the subject the user brought up;
   - target artifact: the discussion notes of step 4;
   - recording rule: the options discussed, with their pros and cons, go to "Options", what the user decided to "Conclusions", and every question still unresolved to "Open questions";
   - recording time `on return`: the notes are written in step 4, from the return.
4. **Notes:** at the end, or when the user asks, write the notes from the `discussion.md` template in `.shadekit/templates/`: topic, date, participants, context, options, conclusions, open questions.
   - No current feature: `.shadekit/discussions/<yyyymmdd>-<hhmm>-<slug>.md`
   - Current feature: `.shadekit/features/<id>/discussions/<yyyymmdd>-<hhmm>-<slug>.md`
5. **Report:** end with **end of work** and the line "Discussion notes written to `<path>`." With a current feature, add that the notes are input for its next `refine` round, and that `/shade-kit-new` creates a feature of their own from them (the closing message does not list `/shade-kit-new` while a feature is current). Do not start either skill yourself.

## Ending

Every way of ending loads `shade-kit-next-steps` and passes it:

- why the run ends: **refusal**, with the failure envelope of the call that failed, or that `shade-kit` was not found; or **end of work**, with one line of what happened;
- every warning of the CLI calls of this run.

When the line of what happened makes a recommendation, it names the recommendation and its reason, but a skill only when the closing message does not list it: that message lists the steps of `shade-kit phase next` for the current feature, `/shade-kit-discuss`, and `/shade-kit-drop`, and without a current feature `shade-kit feature list`, `shade-kit feature set <id>`, and `/shade-kit-new`. The closing message is the end of this skill; write nothing after it.

## Rules

- No entry protocol: `discuss` is not a phase of any profile.
- Never start a phase skill: a phase starts only when the user calls its skill.
- Never write `feature.json` or any other kit JSON file.
