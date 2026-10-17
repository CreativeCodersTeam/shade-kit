---
name: shade-kit-research
description: SHADE kit general skill that runs a set of research focuses, one research agent per focus in parallel where the agent supports subagents, and merges the results into researches.md. Loaded by the SHADE kit skills refine and map; not meant to be called directly.
---

# shade-kit-research

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/06-skill-foundation.md. The research agent definitions do not exist yet. Unverified: whether GitHub Copilot and OpenAI Codex CLI support subagents. -->

## Purpose

Collects facts with sources before decisions are made, and keeps them in one place (`researches.md`) per feature.

## Input from the calling skill

- The calling phase (`refine` or `map`).
- The focuses to run (for example "gaps and open questions", "existing architecture").
- The feature context: `request.md`, `spec.md`, earlier `researches.md` sections.

## Flow

1. **Start** one research agent per focus. Run them in parallel as subagents where the agent supports it (Claude Code), sequentially otherwise.
2. **Collect** the findings. Every finding names its source (file and line, or link); a finding without a source is marked as an assumption.
3. **Merge** into `researches.md`, created from the template on the first run: one section per `<phase> / <focus>` with date, findings, and sources. Replace the section of the same phase and focus; leave all other sections unchanged.
4. **Return** to the calling skill: a short summary per focus.
