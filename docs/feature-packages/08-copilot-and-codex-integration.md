# 08 · GitHub Copilot and OpenAI Codex CLI integration

| | |
|---|---|
| Status | Draft, 2026-10-14 |
| Source | Concept §1 (Success, Constraint), §2 (Agents), §3.4, §10 open question 5, [concept §9](../../research/shade-kit-concept.md#9-version-drift) |
| Depends on | 02, 06, 07 |
| Enables | Phase packages 09–17 are built and accepted on all three agents |

## Benefit

A team can use any of the three target agents for the same feature, even mixed within one team, because they all run the same skills against the same engine and the same `.shadekit/` data. This is part of the v1 success criterion: "a team can take a feature from `new` to `finish` with any of the three target agents".

## Scope

### In scope

- The research that answers concept open question 5, written down as a decision record.
- `integrations install --agent copilot` and `--agent codex`, and their part of `integrations update`.
- Agent-specific adaptations of the content from package 06, limited to what the research shows is needed.
- The instruction file Copilot reads, as a possible addition to package 02's managed section.

### Out of scope

- New skill behaviour. This package makes existing skills run on two more agents; it adds no features.
- Hooks and MCP configuration (as in package 07).

## Functional description

### Step 1: compatibility research

Before any integration work, check for GitHub Copilot and OpenAI Codex CLI (**Unverified** today, concept §3.4):

| Question | Why it matters |
|---|---|
| Can it load the `SKILL.md` format and the plugin layout unchanged? | Decides whether one skill source serves all agents. |
| Does it have a plugin mechanism, and how is it installed at user level? | Decides between plugin install and writing to the user directory. **Decided** fallback: user directory. |
| Does it support subagents, and can they run in parallel? | The research skill (package 06) runs one subagent per focus; without subagents it runs the focuses one after another in the main session. The independent review in `implement` (package 13) runs in a subagent; without one, the fallback (for example a fresh session) has to be decided in the ADR. |
| Does it have a structured question tool? | The interview skill (package 06) falls back to plain questions. |
| Which instruction file does it read (`AGENTS.md`, `.github/copilot-instructions.md`, other)? | Decides which files package 02 writes the managed section into. |

The result is an ADR in `docs/adr/` with a table per agent: supported, supported with a different format, not supported, and the chosen handling. For each "not supported", it states what is lost (concept §1 constraint).

### Step 2: install and update

- `integrations install --agent copilot|codex` installs the same content as for Claude Code at user level, as a plugin where the agent has one, otherwise in the agent's user directory. **Decided.**
- If the research shows a format difference, the engine transforms the content at install time from the one skill source. There is no second copy of the skill text in the kit repository. **Proposed.**
- Version recording and `integrations update` work as in package 07.

### Step 3: degraded behaviour

Where a capability is missing, the skill keeps its function and changes only how it runs, for example sequential research instead of parallel subagents. If a function would be lost, the ADR names it and the human decides whether it is acceptable for v1.

### Instruction file

If Copilot does not read `AGENTS.md`, package 02's `init` also writes the managed section into the file Copilot reads, and package 18's `upgrade` maintains it there.

### Rule for later packages

From this package on, every phase package (09–17) counts as done only when its acceptance criteria pass on all three agents, with the degraded behaviour from the ADR where it applies. **Proposed.**

## Acceptance criteria

- **FP08-AC1** An ADR answers every research question for both agents, with sources.
- **FP08-AC2** After `integrations install --agent copilot`, a Copilot session can load `shade-kit-next-phase` and run the version handshake.
- **FP08-AC3** After `integrations install --agent codex`, a Codex CLI session can load `shade-kit-next-phase` and run the version handshake.
- **FP08-AC4** The kit repository holds one source per skill; agent-specific differences are produced at install time.
- **FP08-AC5** The research skill (package 06) produces the same `researches.md` structure on every agent, also where it runs sequentially.
- **FP08-AC6** `init` writes the managed section into every instruction file the ADR names.

## Open points

- Concept open question 5 is the content of step 1.
