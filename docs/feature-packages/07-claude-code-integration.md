# 07 · Claude Code integration

| | |
|---|---|
| Status | Draft, 2026-10-14 |
| Source | Concept §2 (Lifecycle), §3.1 (`integrations install/update`), §3.4, [concept §9.6](../../research/shade-kit-concept.md#9-version-drift) |
| Depends on | 01, 06 |
| Enables | 08 (same mechanism for more agents); end-to-end use of all phase packages in Claude Code |

## Benefit

A developer installs the kit's skills and agents for Claude Code with one command, once per machine, and every repository on that machine can use them. Updating the engine and running `integrations update` keeps engine and skills on the same version, so the version handshake (package 06) does not fail.

## Scope

### In scope

- `shade-kit integrations install --agent claude`.
- `shade-kit integrations update`.
- Where the integration content comes from and how its version is recorded.
- Reporting installed integrations in `shade-kit version --json` (announced in package 01).

### Out of scope

- GitHub Copilot and OpenAI Codex CLI (package 08).
- Hooks and MCP configuration. The concept lists them as possible integration content, but no v1 feature needs them (approval enforcement and the MCP server are later). **Decided** (confirmation list, 2026-10-18).
- Writing anything into a repository (concept §9.6, **Decided**).
- Removing an integration. `integrations uninstall` is a later addition. **Proposed.**

## Functional description

### Content source

- The integration content (skills from package 06 and the phase packages, research agent definitions) is packed into the engine release, so an engine always installs exactly the content of its own version. **Decided** (confirmation list, 2026-10-18). This makes "plugin and CLI release together" (concept §9.6) hold by construction on every machine.

### `integrations install --agent claude`

- Installs the content at **user level** as a Claude Code plugin. **Decided** (concept §3.1).
- **Unverified:** the exact way to register a plugin at user level from a CLI (for example a local plugin marketplace plus a plugin install command, or writing to the user's plugin directory). This has to be checked against the Claude Code documentation before the package is built. If no supported way exists, the fallback is writing the same content to the user's `~/.claude/` skills and agents directories. **Decided** as the general fallback in the concept §3.1.
- Records the installed agent, kit version, and install location in an engine-owned file in the user's configuration directory (`schemaVersion` like every kit JSON file). **Proposed.** This file is outside every repository, so `upgrade` (package 18) does not touch it; `integrations install` and `integrations update` migrate it when its format is older.
- Running it again for an installed agent behaves like `integrations update`.
- Without `--agent`, it installs for every supported agent the engine can detect on the machine and reports what it found. **Proposed.** In this package, Claude Code is the only supported agent.

### `integrations update`

- Updates every installed integration to the content of the running engine version.
- Reports per agent: old version, new version, or "already current".
- Does not touch agents that are not installed.

### Version reporting

`shade-kit version --json` adds the installed integrations with agent and kit version. A skill or a human can then see at once whether engine and integration differ. **Proposed.**

## Acceptance criteria

- **FP07-AC1** After `integrations install --agent claude`, a new Claude Code session in any repository can load `shade-kit-next-phase`.
- **FP07-AC2** The installed skills have the same kit version as the engine that installed them.
- **FP07-AC3** After an engine update, `integrations update` replaces the Claude Code content and reports old and new version.
- **FP07-AC4** `shade-kit version --json` lists the installed Claude Code integration with its kit version.
- **FP07-AC5** No file in the current repository is created or changed by `integrations install` or `integrations update`.
- **FP07-AC6** `integrations update` with no installed integration reports that nothing is installed and exits without error.

## Open points

- **Unverified:** supported way to install a Claude Code plugin at user level from a script (see above).
