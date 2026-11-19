# SHADE kit agent compatibility

Status: draft, 2026-11-15. Result of the compatibility research of [package 08](../docs/feature-packages/08-copilot-and-codex-integration.md) (Step 1, FP08-AC1) and of the decisions on it confirmed by the user at Gate 2 on 2026-11-15; updated 2026-11-15 (implementation of package 08: the **Proposed** items of section 2 decided as D1, D2, D5, D6; D3 stays **Open**). Answers [concept](shade-kit-concept.md) open question 5. Design notes only; nothing here is shipped.

Labels as in the [concept](shade-kit-concept.md): **Decided**, **Proposed**, **Open**, **Unverified**. **FACT** marks a claim taken from an official documentation page, the official changelog, the npm registry, or the source code at the named tag. A claim that rests only on inference, on a user report in an issue, or on a community post is **Unverified**.

## 1. Scope and method

- Agents researched:
  - **GitHub Copilot CLI** (`copilot`) 1.0.112, published 2026-11-13 (npm `@github/copilot` `latest`, GitHub release `v1.0.112`). In this note "Copilot" is the Copilot CLI, the agent `integrations install --agent copilot` installs for. VS Code and the Copilot coding agent read some of the same files but are not covered.
  - **OpenAI Codex CLI** (`codex`) 0.169.0, source tag `rust-v0.169.0` (latest stable, published 2026-11-13).
- Date: every source was read on 2026-11-15.
- Method: documentation and source reading only. Neither CLI is installed, and neither may be installed (user decision), so nothing was run locally. Every behaviour that only a run can show is **Unverified** and listed in [section 8](#8-checks-by-hand-for-fp08-ac2-and-fp08-ac3) for the check by hand.
- Copilot: the GitHub Docs Copilot CLI reference pages, the official changelog, the npm registry, and issues in `github/copilot-cli`. The plugin reference and the command reference were read twice (research pass and spot check).
- Codex: the official documentation (every `developers.openai.com/codex/*` URL redirects with 308 to `learn.chatgpt.com/docs/*`, treated as official) and the source of `openai/codex` at tag `rust-v0.169.0`. Source paths below are relative to `https://github.com/openai/codex/tree/rust-v0.169.0/`. The load-bearing source claims (plugin subcommands, marketplace upgrade, plugin cache replacement, skill frontmatter parser, skill injection, `request_user_input` mode check, `AGENTS.md` loading, subagent thread limit, skill namespacing) were read again in a clone at that tag.
- Source abbreviations used in the tables (all read 2026-11-15):
  - C-CMD: https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-command-reference
  - C-PLG: https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-plugin-reference
  - C-CFG: https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-config-dir-reference
  - C-PRG: https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-programmatic-reference
  - C-SKL: https://docs.github.com/en/copilot/concepts/agents/about-agent-skills and https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-skills
  - C-INS: https://docs.github.com/en/copilot/how-tos/copilot-cli/set-up-copilot-cli/install-copilot-cli
  - C-LOG: https://raw.githubusercontent.com/github/copilot-cli/main/changelog.md (the version in brackets is the release that made the change)
  - C-NPM: https://registry.npmjs.org/@github/copilot
  - X-SKL: https://learn.chatgpt.com/docs/build-skills
  - X-PLG: https://learn.chatgpt.com/docs/plugins and https://developers.openai.com/plugins/build/plugins
  - X-CMD: https://learn.chatgpt.com/docs/developer-commands?surface=cli
  - X-SUB: https://learn.chatgpt.com/docs/agent-configuration/subagents
  - X-AGM: https://learn.chatgpt.com/docs/agent-configuration/agents-md
  - X-SRC: `openai/codex` at `rust-v0.169.0`, file path given

## 2. Decisions

**Decided** on 2026-11-15 (package 08, Gate 2):

1. **One skill source for all three agents.** Both agents load the `SKILL.md` format and find the existing `.claude-plugin/` manifests, so the skills under `skills/` and the manifests `.claude-plugin/marketplace.json` and `.claude-plugin/plugin.json` serve Claude Code, Copilot, and Codex unchanged. There is no install-time transformation and no second copy (FP08-AC4). This replaces the **Proposed** transformation of package 08 Step 2.
2. **Install as a plugin through each agent's CLI**, at user level, from the same marketplace `CreativeCodersTeam/shade-kit` at the tag `v<engine version>` (`main` for a prerelease engine and for a release engine whose tag does not exist), exactly as for Claude Code (package 07). Both agents have a plugin mechanism, so the user-directory fallback of concept §3.1 is not used. The marketplace name is `creativecoders-shade-kit` and the plugin id `shade-kit@creativecoders-shade-kit` for every agent. The developer override `SHADE_KIT_MARKETPLACE_SOURCE` (a local path or `owner/repo#ref`) applies to every agent.
3. **Command sequences per agent:**
   - **Copilot:** the change commands (`plugin install`, `plugin update`, `plugin marketplace add`, `update`, `remove`) have no `--json`. Success is exit code 0; after the changes the engine reads the state again with `copilot plugin list --json` and `copilot plugin marketplace list --json`. Add: `copilot plugin marketplace add <owner/repo#ref | path>` → `copilot plugin install shade-kit@creativecoders-shade-kit`. Keep: `copilot plugin marketplace update creativecoders-shade-kit` → `copilot plugin update shade-kit@creativecoders-shade-kit` (or `plugin install` when the plugin is not installed). Switch: D2 below.
   - **Codex:** every command takes `--json`; there is no `plugin update`. Keep: `codex plugin marketplace upgrade creativecoders-shade-kit --json` → `codex plugin add shade-kit@creativecoders-shade-kit --json` (refined by D1 and D5 below). Switch: `codex plugin remove shade-kit@creativecoders-shade-kit --json` (only if installed) → `codex plugin marketplace remove creativecoders-shade-kit --json` → `codex plugin marketplace add <owner/repo> --ref <ref> --json` (a directory: `codex plugin marketplace add <path> --json`) → `codex plugin add shade-kit@creativecoders-shade-kit --json`. Add: the last two steps.
   - **Outcome** for Copilot and Codex: `installed` when the plugin was not installed before the run, otherwise always `updated`, never `current`. Reason: neither agent reports "up to date" for a plugin (Copilot's change commands have no JSON output, Codex has no update command), so the engine cannot tell an unchanged plugin from a refreshed one.
   - Process rules as for `claude`: absolute path, `NO_COLOR=1`, the user's home directory as working directory, process tree killed on cancellation; the list commands' stdout is parsed as one JSON value. No CLI flag that this research did not confirm is used.
4. **Windows:** for `copilot` and `codex` only, the locator prefers `<name>.exe`, then `<name>.cmd` (`.ps1` is not supported). A `.cmd` is started through `%ComSpec%` (an absolute path to `cmd.exe`) as `/d /s /c "<path> <args>"`. Every argument that contains `"`, `%`, `!`, `^`, `&`, `|`, `<`, `>`, `(`, `)`, a line break, or NUL is refused, not escaped; the refusal fails with the tool's command-failed exception (exit code 52) and names the character. This is a documented deviation from the "no shell" rule, decided after an objection: `cmd.exe` re-parses its command line, which is the argument-injection class of BatBadBut / CVE-2024-24576; refusing every metacharacter instead of escaping it keeps the remaining risk to arguments the engine builds itself (marketplace name, plugin id, ref, source path). The risk is accepted by the user. The shim path itself is checked with the same full list (D4, **Decided** on 2026-11-15 during the implementation), so a shim below `C:\Program Files (x86)\…` or in a profile whose name contains `&`, `(`, `)`, `!`, `%`, or `^` fails with exit code 52 (known limit); Unicode lookalikes such as the fullwidth `＆` and `％` are not refused, and whether `cmd.exe` or Node map them to ASCII (Windows "best-fit") is **Unverified** (**Open**). Claude Code stays `.exe` only. Why `.cmd` is needed at all: an npm install puts shims on the `PATH` (**Unverified**, standard npm behaviour, see the tables).
5. **Instruction file:** both agents read `AGENTS.md`, which `init` already writes, so `init` writes no new file (FP08-AC6). Copilot also reads `CLAUDE.md`, so in a repository initialised by `shade-kit init` it loads the managed section twice. Accepted as a known limit; `init` keeps writing both files.
6. **Call forms:** Claude Code and Copilot `/shade-kit-<x>`, Codex `$shade-kit:shade-kit-<x>`. The managed section gets one sentence that names the call form per agent, and `shade-kit-next-steps` writes the calls in the form of the agent it runs in (Codex: `$shade-kit:<skill>`).
7. **Tool names in the skill texts:** where a skill names a Claude Code tool (`AskUserQuestion`, `Agent`), it also names Copilot's (`ask_user`, `task`) and Codex's (`request_user_input`, available only in plan mode by default; `spawn_agent`). Outside Codex plan mode the skills ask plain questions, as their texts already describe for an agent without a structured question tool.
8. **User-only skills stay a convention in v1.** Copilot's `disable-model-invocation` is not used, because a flagged skill cannot be loaded by another skill (issue #4438); Codex's `agents/openai.yaml` with `policy.allow_implicit_invocation: false` would need an agent-specific file next to the skill, which decision 1 avoids. Not used in v1.
9. The rule for later packages (09–17 are done only when their acceptance criteria pass on all three agents, with the degraded behaviour of [section 6](#6-degraded-behaviour)) is **Decided** on 2026-11-15 (package 08, Gate 2).

**Decided** after Gate 2 (user, 2026-11-15, during the implementation of package 08; these were **Proposed** at Gate 2):

- **D1 Codex kept directory marketplace:** a kept Git marketplace runs `codex plugin marketplace upgrade creativecoders-shade-kit --json`, then `codex plugin add shade-kit@creativecoders-shade-kit --json`; a kept directory (local) marketplace skips `marketplace upgrade` and runs only `codex plugin add`. Reason: `codex plugin marketplace upgrade <name>` returns the error "marketplace `<name>` is not configured as a Git marketplace" for every marketplace whose `source_type` is not `git` (**FACT**, X-SRC `codex-rs/core-plugins/src/manager.rs` `upgrade_configured_marketplaces_for_config_with_mode`, `codex-rs/core-plugins/src/marketplace_upgrade.rs` `parse_configured_git_marketplace`), so the kept-marketplace sequence of decision 3 would fail for the developer override `SHADE_KIT_MARKETPLACE_SOURCE=<path>`. `plugin add` of an installed plugin stages a new copy and replaces the cache entry (**FACT**, X-SRC `codex-rs/core-plugins/src/store.rs`, the backup-and-rename branch), so it refreshes the content from the directory on its own.
- **D2 Copilot switch order:** `copilot plugin uninstall shade-kit@creativecoders-shade-kit` (only if installed) → `copilot plugin marketplace remove creativecoders-shade-kit` (no `--force`) → `copilot plugin marketplace add <new source>` → `copilot plugin install shade-kit@creativecoders-shade-kit`. Reason: `marketplace remove` "is refused if plugins from the marketplace are still installed; pass `--force` to also uninstall those plugins" (**FACT**, C-PLG). `marketplace remove --force` was the rejected one-step alternative.
- **D5 Codex keep or switch without a reported ref:** Codex reports a marketplace source only as `{sourceType, source}`, without the ref (**FACT**, X-SRC `codex-rs/cli/src/plugin_cmd.rs` `JsonMarketplaceSource`). For a Git marketplace with the repository of the source, the engine runs `codex plugin marketplace add <owner/repo> --ref <ref> --json` as a check: `alreadyAdded: true` → keep (`marketplace upgrade`, then `plugin add`); exit code 0 with `alreadyAdded: false` → only `plugin add`; a non-zero exit code → switch (`plugin remove` if installed → `marketplace remove` → `marketplace add` → `plugin add`). Known limits: a network or other failure of the check counts as "other source", so the switch then fails and a rerun repairs it; `version` reports `ref` and `kitVersion` `null` for Codex; `previousKitVersion` is set only on a confirmed keep. Further **FACT**s from the source: `plugin add` always sets `enabled = true`; `marketplace remove` does not refuse while plugins are installed; a directory source that contains `#` is refused by the engine (exit code 52), because Codex splits a source at its last `#`; a failing Codex command prints no JSON, its message is on stderr.
- **D6 Copilot conflict hint:** the hint of exit code 53 names two steps for Copilot, `copilot plugin uninstall shade-kit@creativecoders-shade-kit` and then `copilot plugin marketplace remove creativecoders-shade-kit`, because of the refusal quoted in D2; Codex needs only `codex plugin marketplace remove creativecoders-shade-kit`.

**Open:**

- **D3 Fallback for the independent review** (`shade-kit-review`, loaded by `implement` and `quick`): both agents can start subagents (section 4), so the review runs in a subagent on all three agents and no fallback is needed in the default configuration. For an agent whose subagents are switched off (Codex `agents.enabled = false`), the proposal is: the review runs in the main session from the review inputs only, and `review.md` records that it was not independent; the human can instead run `implement` in a new session for the review. Today `shade-kit-review` names no fallback ("a subagent where the agent supports it"). The independence of the review would be lost in that configuration, so the human decides (package 08 Step 3); the user left it **Open** on 2026-11-15, with no skill change in package 08.

## 3. GitHub Copilot CLI

### 3.1 Findings

**SKILL.md format and plugin layout**

- Copilot loads `SKILL.md` skills, one folder per skill with YAML frontmatter. **FACT** (C-SKL, C-CMD "Skills reference"). Frontmatter fields it honours: `name` (required; letters, digits, hyphens, underscores, dots, colons, and spaces), `description` (required, at most 1024 characters), `argument-hint`, `allowed-tools`, `user-invocable` (default `true`), `disable-model-invocation` (default `false`). **FACT** (C-CMD). The longest shade-kit description is 477 characters, quotes included. **FACT** (measured over `skills/*/SKILL.md` on 2026-11-15).
- `metadata` is not a documented field. Unknown fields are loaded and ignored: unknown fields load with warnings instead of being skipped [0.0.403], the warnings are suppressed [1.0.3]. **FACT** (C-LOG). Issue #951 (closed 2026-04-07; a skill with `metadata` as last field not found on 0.0.380): the maintainer states that "unknown fields like `metadata:` are silently ignored".
- Skill locations in priority order (first found wins for a duplicate name): project `.github/skills/`, `.agents/skills/`, `.claude/skills/`, parent `.github/skills/`, personal `~/.copilot/skills/`, `~/.agents/skills/`, plugin directories, `COPILOT_SKILLS_DIRS`, `--add-dir` roots, built-in skills, organisation/enterprise skills. **FACT** (C-CMD). `~/.claude/skills` is no longer read by the Copilot CLI [1.0.36]. **FACT** (C-LOG). So a project skill with the same name as a shade-kit skill would win over the plugin (**FACT** by the order above); shade-kit's `shade-kit-` prefix makes a clash unlikely.
- Plugin manifests: `marketplace.json` is looked up as `marketplace.json`, `.plugin/marketplace.json`, `.github/plugin/marketplace.json`, `.claude-plugin/marketplace.json`, in this order; the legacy plugin manifest as `.plugin/plugin.json`, `plugin.json`, `.github/plugin/plugin.json`, `.claude-plugin/plugin.json`; a root `plugin.json` with an Agent Plugins `$schema` takes precedence. **FACT** (C-PLG). This repository has none of the earlier files (no root `marketplace.json` or `plugin.json`, no `.plugin/`, no `.github/plugin/`). **FACT** (`ls` of the repository root and `.github/` on 2026-11-15). So Copilot reads `.claude-plugin/marketplace.json` and `.claude-plugin/plugin.json`.
- A marketplace entry has `name` and `source` (a path relative to the repository root, or an object); `./x` and `x` are equivalent. **FACT** (C-PLG). Whether `"source": "./"` (the repository root itself) resolves as in Claude Code: **Unverified**.
- `plugin.json`: `name` required, `version` optional, skills default to `skills/`. **FACT** (C-PLG). Since [1.0.108] an install reports manifest fields it ignores. **FACT** (C-LOG). shade-kit's `plugin.json` has no `version` and no field outside name, description, author, homepage, repository, license, keywords; which of these Copilot reports as ignored: **Unverified**.
- Components loaded from the plugin root by default (legacy plugins): `agents/`, `skills/`, `hooks.json` or `hooks/hooks.json`, `.mcp.json` or `.github/mcp.json`, `lsp.json` or `.github/lsp.json`; `commands/` only when a manifest field points to it. **FACT** (C-PLG). Because the plugin root is the repository root, package 07's rule (every component at a default location ships to every user) extends to these Copilot locations, `.github/mcp.json` and `.github/lsp.json` included. Today none of them exists apart from `skills/`. **FACT** (`ls` on 2026-11-15).

**Plugin mechanism and user-level install**

- Every `copilot plugin` subcommand is non-interactive. **FACT** (C-PLG, C-CMD "Managing plugins non-interactively"). `plugin install SPEC` (alias `add`), `plugin uninstall NAME`, `plugin list [--json]`, `plugin update NAME | --all`, `plugin enable NAME`, `plugin disable NAME`, `plugin marketplace add SOURCE`, `plugin marketplace list [--json]`, `plugin marketplace browse NAME [--json]`, `plugin marketplace update [NAME]` ("Re-fetch a marketplace's plugin catalog"), `plugin marketplace remove NAME [--force]`. Only `plugin list`, `marketplace list`, and `marketplace browse` take `--json` [1.0.85]. **FACT** (C-PLG, C-LOG).
- `plugin list --json` emits a flat array of `{ name, marketplace?, version?, enabled, source, installedFrom? }`. **FACT** (C-PLG). The shape of `marketplace list --json` is not documented. **Unverified**.
- Install specs: `plugin@marketplace`, `OWNER/REPO`, `OWNER/REPO:PATH`, a Git URL, a local path. Direct installs (repository, URL, path) are deprecated [1.0.108]; only `plugin@marketplace` installs will stay supported. **FACT** (C-PLG, C-LOG). shade-kit installs `plugin@marketplace`.
- Marketplace sources: `owner/repo`, `owner/repo#ref`, a URL, a local path. **FACT** (C-PLG). `owner/repo#ref` since [1.0.55]; `refs/tags/v2.1.0` works as ref [1.0.62]; `./name`, `../name`, `~/name` add a local directory [1.0.110]. **FACT** (C-LOG). A bare tag name such as `v0.8.0` after `#`: **Unverified**. A marketplace is registered under the `name` in its `marketplace.json`; there is no custom local name. **FACT** (C-PLG).
- Storage at user level: `~/.copilot/installed-plugins/<marketplace>/<plugin>`; marketplace cache below `~/.cache/copilot/marketplaces/` (Linux), `~/Library/Caches/copilot/marketplaces/` (macOS), or `COPILOT_CACHE_HOME`; installed-plugin metadata in `~/.copilot/config.json` (internal, not to be edited); `enabledPlugins` and `extraKnownMarketplaces` in `~/.copilot/settings.json`. **FACT** (C-PLG, C-CFG). The engine reads the state only through `plugin list --json` and `marketplace list --json` (decision 3), never these files.

**Subagents and parallelism**

- The model starts subagents with the `task` tool ("Run subagents"); also `list_agents`, `read_agent`, `write_agent`. Built-in agents include `general-purpose`, `explore`, `task`, `code-review`. **FACT** (C-CMD). Subagents run in parallel. **FACT** (C-CMD).
- Limits: the command reference gives two different statements. One table: concurrent subagents per session default `32` (range 1–256, `COPILOT_SUBAGENT_MAX_CONCURRENT`), nesting depth default `4` (range 1–128, `COPILOT_SUBAGENT_MAX_DEPTH`). Another passage: depth default `6` (max 256), concurrency by plan (Free/Education 2, Pro/Pro+ 4, Max 8, Business 16, Enterprise and usage-based billing 32). **FACT** that both are on the page; which one applies: **Unverified**. Either way at least two subagents run at once; a research run with more focuses than the limit queues or runs them in batches (**Unverified**).
- A custom agent started as subagent gets no repository instructions unless `include-custom-instructions: true`. **FACT** (C-CMD). The research skill uses no custom agent definition (its brief is everything the subagent gets), and the review reads the instruction files as named inputs, so this does not affect them.

**Structured question tool**

- `ask_user` ("Ask the user a question"). **FACT** (C-CMD). Added [0.0.387]; choices with an "Other" free-text option [1.0.71]; multi-select [1.0.79]. **FACT** (C-LOG). The parameter schema (number of options and questions per call, header length) is not documented. **Unverified**.
- `--no-ask-user` or `askUser: false` disables the tool ("the agent works autonomously without asking questions"). **FACT** (C-CMD). In autopilot mode the tool is answered automatically. **FACT** (C-LOG). See [section 6](#6-degraded-behaviour) for what that means for approvals.

**Calling a skill by name; keeping the agent from loading it on its own**

- The user types `/SKILL-NAME` or names the skill in a prompt; the model loads a skill with the `skill` tool. **FACT** (C-SKL, C-CMD). Plugin skills keep their bare name; when two plugins provide the same name, both are reachable as `/my-plugin/search` and `/other-plugin/search`, and the bare name goes to the higher-priority plugin. **FACT** (C-CMD). So the shade-kit call form is `/shade-kit-<x>` (decision 6). Root skills of a plugin show as `/plugin` [1.0.71]. **FACT** (C-LOG).
- Skill management: `/skills` in a session; `copilot skill list [--json]` in the terminal. **FACT** (C-CMD).
- `disable-model-invocation: true` is documented and "fully honoured" since [1.0.74]. **FACT** (C-CMD, C-LOG). But issue #4438 (open, reproduced on 1.0.79 to 1.0.8x, comments until 2026-11-10) reports that a flagged skill is removed from the `skill` tool's registry, so "Use the /name skill" prompts, `-p` runs, and a skill that loads another flagged skill fail with "Skill not found"; only a typed interactive `/name` works. Issue #4451 (closed 2026-11-04) reports a related failure after a typed `/name`. **Unverified** (user reports). Because shade-kit skills load each other by name (`shade-kit-phase-entry`, `shade-kit-next-steps`, …), the flag is treated as unusable (decision 8).
- That a skill text saying "load `shade-kit-next-steps`" makes the model call the `skill` tool with that bare name: **Unverified** (check by hand).

**Instruction file**

- Copilot CLI loads and merges, in the git root and the current directory: `CLAUDE.md`, `GEMINI.md`, `AGENTS.md`, `.github/instructions/**/*.instructions.md`, `.claude/rules/**/*.md`, `.github/copilot-instructions.md`; at user level `$HOME/.copilot/copilot-instructions.md` and `$HOME/.copilot/instructions/**/*.instructions.md`; `--no-custom-instructions` turns this off. **FACT** (C-CMD). Nested `AGENTS.md` files between the root and the current directory: **Unverified**.
- `init` writes the managed section into `CLAUDE.md` and `AGENTS.md` (`RepositoryInitializer.InstructionFileNames`). **FACT** (source, 2026-11-15). Copilot loads both, so the section is in its context twice (decision 5). Whether Copilot loads it once when `AGENTS.md` is a link to `CLAUDE.md` (a case `init` already handles): **Unverified**.

**Frontmatter visibility**

- "Skill content injected to the model no longer includes YAML frontmatter metadata" [1.0.48]. **FACT** (C-LOG). So, as in Claude Code, the model does not see `metadata.version`; the version stated in the text of the handshake step (06-2) is what the model reads. That the skill list shows name and description: **Unverified**.
- How the parser types `version: 0.8.0`: the YAML library is not documented (one native binary). `metadata` is an ignored field, so the type does not reach any code path. Under the YAML 1.2.2 core schema `0.8.0` is a string (https://yaml.org/spec/1.2.2/, read 2026-11-15); the YAML 1.1 float pattern (https://yaml.org/type/float.html, read 2026-11-15) formally matches it. For Copilot: **Unverified**; risk low because the field is ignored.

**CLI detection**

- npm package `@github/copilot` with bin `copilot` (`npm-loader.js`, Node 22 or later); platform packages such as `@github/copilot-win32-x64` with bin `copilot.exe`. **FACT** (C-NPM). Other routes: WinGet `GitHub.Copilot`, Homebrew `brew install --cask copilot-cli`, an install script (`/usr/local` as root, else `$HOME/.local`), release archives for darwin, linux, linuxmusl, win32 (x64, arm64). **FACT** (C-INS). The Windows npm shims `copilot.cmd` and `copilot.ps1`: **Unverified** (standard npm behaviour). The file name inside the Windows zip or msi (`copilot.exe`): **Unverified**.
- `copilot --version`: documented, output format not documented; `copilot version` also checks for updates over the network. **FACT** (C-CMD). The engine needs neither.
- Auto-update may run a newer cached version unless `--no-auto-update`. **FACT** (C-CMD). The engine's plugin commands would then run on that newer version (**Unverified** whether auto-update applies to `copilot plugin` subcommands).
- Whether `copilot plugin list --json` and `marketplace list --json` work without a signed-in account: **Unverified**.

### 3.2 Table

| Capability | Status | Chosen handling | What is lost | Sources |
|---|---|---|---|---|
| `SKILL.md` format | Supported | Same skill files as Claude Code (decision 1). `metadata` ignored. | — | C-SKL, C-CMD "Skill frontmatter fields", C-LOG 0.0.403, 1.0.3; issue https://github.com/github/copilot-cli/issues/951 (read 2026-11-15) |
| Plugin layout (`.claude-plugin/` manifests, `skills/`) | Supported | Same manifests and tree; no transformation (decision 1). `"source": "./"` **Unverified**. | — | C-PLG "Manifest lookup", "File locations" (read 2026-11-15) |
| Plugin mechanism, user-level install | Supported with a different format (change commands without `--json`) | `copilot plugin marketplace add` / `install` / `update` / `marketplace update`; state re-read with `plugin list --json` and `marketplace list --json`; outcome `installed` or `updated` (decisions 2, 3) | — (no `current` outcome) | C-PLG, C-CMD "Managing plugins non-interactively", C-CFG, C-LOG 1.0.55, 1.0.62, 1.0.85, 1.0.108, 1.0.110 (read 2026-11-15) |
| Subagents, parallel | Supported | `shade-kit-research` starts one subagent per focus with `task`; `shade-kit-review` runs in a subagent; skill texts name `task` (decision 7) | — | C-CMD tools table, "Custom agents reference", subagent limits (read 2026-11-15) |
| Structured question tool | Supported | Skill texts name `ask_user` (decision 7); option and question limits **Unverified** | — | C-CMD tools table, `--no-ask-user`; C-LOG 0.0.387, 1.0.71, 1.0.79 (read 2026-11-15) |
| Call a skill by name | Supported | `/shade-kit-<x>`; managed section and `shade-kit-next-steps` use this form (decision 6) | — | C-SKL, C-CMD "Skills reference" (read 2026-11-15) |
| Keep the agent from loading a skill on its own | Supported with a different format (`disable-model-invocation`), reported broken for skill-to-skill loads | Convention in the skill texts, as in Claude Code; flag not used (decision 8) | — (v1 relies on the convention on every agent) | C-CMD, C-LOG 0.0.412, 1.0.74; https://github.com/github/copilot-cli/issues/4438, https://github.com/github/copilot-cli/issues/4451 (read 2026-11-15) |
| Instruction file | Supported (`AGENTS.md`, also `CLAUDE.md`) | No new file; managed section loaded twice, accepted (decision 5) | — | C-CMD "Custom instructions locations" (read 2026-11-15) |
| Frontmatter visibility | Frontmatter not shown to the model | Version stated in the handshake text (06-2, unchanged) | — | C-LOG 1.0.48 (read 2026-11-15) |
| CLI on Windows | Supported with a different format (`.exe`, or `.cmd` npm shim **Unverified**) | `.exe`, then `.cmd` through `%ComSpec%` with argument refusal (decision 4) | — | C-NPM, C-INS (read 2026-11-15) |

## 4. OpenAI Codex CLI

### 4.1 Findings

**SKILL.md format and plugin layout**

- A skill is a folder with a required `SKILL.md` and optional `scripts/`, `references/`, `assets/`, `agents/openai.yaml`; required frontmatter `name` and `description`. **FACT** (X-SKL).
- The parser reads only `name`, `description`, and `metadata.short-description`; it has no `deny_unknown_fields`, so other keys, `metadata.version` included, are ignored. **FACT** (X-SRC `codex-rs/skills/src/parser.rs`, `SkillFrontmatter` and `SkillFrontmatterMetadata`). `description` has no length limit in the parser; it is shortened only when the rendered skill list exceeds its budget (up to 2 % of the context window, `skills.max_context_tokens` up to 10 000). **FACT** (X-SRC `codex-rs/skills/src/parser_tests.rs` `preserves_overlong_descriptions_and_short_descriptions`; X-SKL). Whether the 19 shade-kit descriptions fit the budget unshortened: **Unverified**.
- If the YAML is invalid, Codex re-quotes plain scalars that contain `: ` and parses again (`repair_frontmatter_scalar_fields`); a ` #` still cuts a value. **FACT** (X-SRC `codex-rs/skills/src/parser.rs`). shade-kit already quotes such descriptions (06-2), so the repair is not needed.
- User-level skill directories: `~/.agents/skills` (documented) and `$CODEX_HOME/skills` (default `~/.codex/skills`, deprecated). Repository level: `.agents/skills` in every directory from the project root (marker `.git`) down to the current directory. **FACT** (X-SKL; X-SRC `codex-rs/ext/skills/src/host_roots.rs`). Not used, since shade-kit installs a plugin (decision 2).
- Plugin manifest lookup: root `plugin.json` only with an agent-plugins.org `$schema`, then `.codex-plugin/plugin.json`, `.claude-plugin/plugin.json`, `.cursor-plugin/plugin.json`. **FACT** (X-SRC `codex-rs/utils/plugins/src/plugin_namespace.rs`). Marketplace lookup: `.agents/plugins/marketplace.json`, `.agents/plugins/api_marketplace.json`, `.claude-plugin/marketplace.json`, `.cursor-plugin/marketplace.json`. **FACT** (X-SRC `codex-rs/core-plugins/src/marketplace.rs`). The documentation mentions "Claude-compatible manifests". **FACT** (X-PLG). This repository has no `.agents/` and no `.codex-plugin/` and no root `plugin.json`. **FACT** (`ls` on 2026-11-15). So Codex reads the `.claude-plugin/` manifests.
- A marketplace entry needs `name` and `source`; unknown fields are tolerated; a local path source must start with `./` and stay inside the root, and `"."` and `"./"` resolve to the marketplace root. **FACT** (X-SRC `codex-rs/core-plugins/src/marketplace.rs`, the `"." | "./"` branch). That shade-kit's `marketplace.json` loads end to end: **Unverified** (not run).
- Plugin skills are named `<plugin manifest name>:<skill name>`, so the shade-kit skills are `shade-kit:shade-kit-<x>`. **FACT** (X-SRC `codex-rs/ext/skills/src/loader/namespace.rs`).
- The installed copy goes to `$CODEX_HOME/plugins/cache/<marketplace>/<plugin>/<version>/`, where the version is the manifest `version` or `local`; shade-kit's `plugin.json` has no `version`, so it installs as `local`. **FACT** (X-SRC `codex-rs/core-plugin-common/src/installed.rs`, `codex-rs/core-plugins/src/store.rs`). The kit version comes from the marketplace ref, as for Claude Code. Because the plugin root is the repository root, the copy holds the whole tree; its size: **Unverified**.

**Plugin mechanism and user-level install**

- The `plugins` feature is stable and on by default. **FACT** (X-SRC `codex-rs/features/src/lib.rs`). Interactive `/plugins` in a session; changes need a new session. **FACT** (X-PLG).
- Non-interactive subcommands, all with `--json`: `codex plugin add <plugin[@marketplace]> [-m NAME]`, `codex plugin list [-m NAME] [--available]` (`--available` needs `--json`), `codex plugin remove <plugin[@marketplace]>`, `codex plugin marketplace add <source> [--ref REF] [--sparse PATH]...`, `codex plugin marketplace list`, `codex plugin marketplace upgrade [name]` ("Refresh configured Git marketplace snapshots"), `codex plugin marketplace remove <name>`. There is no `plugin update` and no enable or disable subcommand. **FACT** (X-CMD; X-SRC `codex-rs/cli/src/plugin_cmd.rs`, `codex-rs/cli/src/marketplace_cmd.rs`).
- JSON shapes: `plugin add` → `{pluginId, name, marketplaceName, version, installedPath, authPolicy}`; `plugin list` → `{installed, available}` with entries `{pluginId, name, marketplaceName, version, installed, enabled, source, marketplaceSource?, installPolicy, authPolicy}`; `marketplace add` → `{marketplaceName, installedRoot, alreadyAdded}`; `marketplace list` → `{marketplaces: [{name, root, marketplaceSource?}]}`; `marketplace upgrade` → `{selectedMarketplaces, upgradedRoots, errors}`. **FACT** (X-SRC same files). The fields inside `marketplaceSource` (needed to compare a kept marketplace with the wanted source and ref) were read in the Codex task: `{sourceType, source}`, without the ref (**FACT**, X-SRC `codex-rs/cli/src/plugin_cmd.rs` `JsonMarketplaceSource`); hence D5 (X-SRC `codex-rs/core-plugins/src/installed_marketplaces.rs`, `codex-rs/config/src/marketplace_edit.rs`).
- `marketplace upgrade --json` with any upgrade error writes the errors to stderr and ends with an error ("N upgrade failure(s) occurred."), and a named marketplace that is not a Git marketplace is an error. **FACT** (X-SRC `codex-rs/cli/src/marketplace_cmd.rs` `print_upgrade_outcome_json`, `codex-rs/core-plugins/src/manager.rs`). The exit code of such an error is not 0 (**Unverified**, not run). When the upgrade changed a root, Codex reinstalls the configured plugins of it. **FACT** (X-SRC `codex-rs/core-plugins/src/manager.rs`, `refresh_non_curated_plugin_cache_force_reinstall_detailed`). So the `plugin add` after the upgrade in decision 3 is needed for a plugin that is not installed yet and harmless otherwise.
- Marketplace sources: GitHub shorthand `owner/repo[@ref]`, HTTPS or SSH Git URLs, a local directory; `--ref` is "Git ref to fetch for Git marketplace sources". **FACT** (X-CMD; X-SRC `codex-rs/cli/src/marketplace_cmd.rs`). A tag as `--ref`: **Unverified**.
- State in `config.toml`: `[marketplaces.<name>]` with `source_type` (`git` or `local`), `source`, optional `ref`, `sparse_paths`; `[plugins."<plugin>@<marketplace>"] enabled = true`; Git clones below `$CODEX_HOME/.tmp/marketplaces/`. **FACT** (X-SRC `codex-rs/config/src/marketplace_edit.rs`, `codex-rs/core-plugins/src/installed_marketplaces.rs`). The engine does not read `config.toml` (**Decided** on 2026-11-15, package 08, Gate 2); it reads `plugin list --json` and `marketplace list --json`.
- Whether `codex plugin list --json` works without a signed-in account: **Unverified**. `marketplace upgrade` loads the CLI auth manager (X-SRC `codex-rs/cli/src/marketplace_cmd.rs` `run_upgrade`); whether it needs a login: **Unverified**.

**Subagents and parallelism**

- Subagents are on by default and run in parallel; Codex "waits until all requested results are available". **FACT** (X-SUB). `agents.enabled` defaults to `true`; the tool is `spawn_agent` (also `send_message`, `followup_task`). **FACT** (X-SRC `codex-rs/core/src/config/mod.rs`, `codex-rs/core/src/tools/router.rs`). The default thread limit per session is 6 (`DEFAULT_AGENT_MAX_THREADS`, configurable as `agents.max_concurrent_threads_per_session`, alias `max_threads`). **FACT** (X-SRC `codex-rs/core/src/config/mod.rs`; X-SUB).
- Local Codex starts subagents only "after a direct request or applicable project or skill instruction". **FACT** (X-SUB). The research skill's text is such a skill instruction. That the model follows it: **Unverified**.
- The system prompt tells the main agent not to delegate reading or interpreting skill instructions to a subagent; subagents may do task work when the skill allows it. **FACT** (X-SRC `codex-rs/ext/skills/src/catalog_prompt.rs`). The research and review briefs are task work, not skill instructions, so this fits.
- Custom agents are TOML files in `~/.codex/agents/` or `.codex/agents/`. **FACT** (X-SUB; X-SRC `codex-rs/agent-roles/src/discovery.rs`). Not needed: the research skill uses no specialised agent definition.

**Structured question tool**

- `request_user_input`: each question has `id`, `header` ("12 or fewer chars"), `question`, and `options` `{label, description}`. The tool schema tells the model "Prefer 1 and do not exceed 3" questions and "Provide 2-3 mutually exclusive choices. Put the recommended option first and suffix its label with "(Recommended)". Do not include an "Other" option …; the client will add a free-form "Other" option automatically." **FACT** (X-SRC `codex-rs/core/src/tools/handlers/request_user_input_spec.rs`). These are instructions in the schema descriptions; whether more questions or options are rejected: **Unverified**. That is fewer than Claude Code's 2 to 4 options and 4 questions; the interview's "proposal first, at most three alternatives" becomes at most two alternatives (for the skill-text task). The `(Recommended)` marker is the same as in the interview skill.
- Available only in plan mode by default: `allows_request_user_input` matches only `ModeKind::Plan`; the under-development feature `default_mode_request_user_input` (off) would allow it in the default mode; otherwise the tool answers "request_user_input is unavailable in {mode} mode". **FACT** (X-SRC `codex-rs/protocol/src/config_types.rs`, `codex-rs/tools/src/tool_config.rs`). There is no official documentation page for the tool; community reports: https://github.com/pingdotgg/t3code/issues/5282, https://community.openai.com/t/feature-request-allow-request-user-input-in-default-mode/1395392 (read 2026-11-15). So in a normal session the skills ask plain questions (decision 7).

**Calling a skill by name; keeping the agent from loading it on its own**

- The user mentions `$skill-name` or picks it with `/skills`. **FACT** (X-SKL). Mention names may contain letters, digits, `_`, `-`, and `:`, so `$shade-kit:shade-kit-next-phase` is a valid mention. **FACT** (X-SRC `codex-rs/skills/src/mentions.rs`). Call form `$shade-kit:shade-kit-<x>` (decision 6).
- On a mention, Codex injects the whole `SKILL.md` as `<skill><name>…</name><path>…</path> {contents}</skill>`. **FACT** (X-SRC `codex-rs/ext/skills/src/host_prompt.rs` `load_skill_prompts`, `codex-rs/ext/skills/src/fragments.rs`). For a skill the model picks itself, the system prompt tells it to open and read the listed `SKILL.md` completely. **FACT** (X-SRC `codex-rs/ext/skills/src/catalog_prompt.rs`). That a skill text saying "load `shade-kit-next-steps`" makes the model read the listed `shade-kit:shade-kit-next-steps`: **Unverified** (check by hand).
- A skill can be kept from implicit use with `agents/openai.yaml` `policy.allow_implicit_invocation: false` (default `true`; an explicit `$skill` still works); such a skill is hidden from the skill list in the prompt. **FACT** (X-SKL; X-SRC `codex-rs/skills/src/model.rs`, `codex-rs/ext/skills/src/provider/host.rs`). SKILL.md has no `disable-model-invocation`. Not used in v1 (decision 8). Note: a hidden skill is also missing from the list the model uses to find a skill another skill names; whether it can still be loaded by its path: **Unverified**.

**Instruction file**

- Global `$CODEX_HOME/AGENTS.override.md`, else `AGENTS.md`; in the project, from the root (marker `.git`) down to the current directory, at most one file per directory: `AGENTS.override.md`, `AGENTS.md`, then the fallback names; concatenated from the root down; `project_doc_max_bytes` default 32 KiB. **FACT** (X-AGM; X-SRC `codex-rs/core/src/agents_md.rs`).
- `CLAUDE.md` is not read by default: `project_doc_fallback_filenames` is empty unless configured. **FACT** (X-SRC `codex-rs/core/src/config/mod.rs`, `codex-rs/core/src/agents_md.rs`). So Codex loads the managed section once, from `AGENTS.md`.

**Frontmatter visibility**

- The model **does** see the frontmatter: on a mention the whole file is injected, frontmatter included (see above). **FACT** (X-SRC). So `metadata.version` is visible as text; the handshake step still states the version in its text (06-2), which is what all three agents have in common.
- Codex uses `serde_yaml` 0.9. **FACT** (X-SRC `codex-rs/Cargo.toml`). `metadata.version` is ignored by the parser, so its type reaches no code path (**FACT**, parser above). That `serde_yaml` 0.9 reads `0.8.0` as a string: **Unverified** (not run; YAML 1.2 core schema says string). That a `metadata` value that is not a mapping would fail the skill: **Unverified**; shade-kit's is a mapping.
- The bundled `skill-creator/scripts/quick_validate.py` allows only `name`, `description`, `license`, `allowed-tools`, `metadata`. **FACT** (X-SRC `codex-rs/skills/src/assets/samples/skill-creator/scripts/quick_validate.py`). Only a validator, not the loader; shade-kit uses only `name`, `description`, `metadata`.

**CLI detection**

- Binary `codex`. **FACT** (X-SRC `codex-rs/cli/Cargo.toml`). npm `@openai/codex` with bin `codex` (`bin/codex.js`, Node 16 or later). **FACT** (X-SRC `codex-cli/package.json`). Install script target `${CODEX_INSTALL_DIR:-$HOME/.local/bin}/codex`. **FACT** (X-SRC `scripts/install/install.sh`). Homebrew `brew install --cask codex`.
- Windows: native `codex.exe` at `%LOCALAPPDATA%\Programs\OpenAI\Codex\bin\codex.exe` (or `CODEX_INSTALL_DIR`). **FACT** (X-SRC `scripts/install/install.ps1`). Whether that directory is put on the `PATH`: **Unverified**. npm shims `codex.cmd` and `codex.ps1`: **Unverified** (standard npm behaviour).
- `codex --version` exists (clap); the output (likely `codex-cli 0.169.0`): **Unverified**. The engine does not need it.

### 4.2 Table

| Capability | Status | Chosen handling | What is lost | Sources |
|---|---|---|---|---|
| `SKILL.md` format | Supported | Same skill files (decision 1); `metadata.version` ignored by the parser | — | X-SKL; X-SRC `codex-rs/skills/src/parser.rs` (read 2026-11-15) |
| Plugin layout (`.claude-plugin/` manifests, `skills/`) | Supported | Same manifests and tree (decision 1); skills namespaced `shade-kit:` | — | X-PLG; X-SRC `codex-rs/utils/plugins/src/plugin_namespace.rs`, `codex-rs/core-plugins/src/marketplace.rs`, `codex-rs/ext/skills/src/loader/namespace.rs` (read 2026-11-15) |
| Plugin mechanism, user-level install | Supported with a different format (no `plugin update`) | `marketplace add --ref` / `plugin add`; keep: `marketplace upgrade` + `plugin add`; switch: `plugin remove`, `marketplace remove`, `marketplace add`, `plugin add`; outcome `installed` or `updated` (decisions 2, 3); kept directory marketplace D1, ref check D5 | — (no `current` outcome) | X-CMD, X-PLG; X-SRC `codex-rs/cli/src/plugin_cmd.rs`, `codex-rs/cli/src/marketplace_cmd.rs`, `codex-rs/core-plugins/src/manager.rs`, `codex-rs/core-plugins/src/store.rs` (read 2026-11-15) |
| Subagents, parallel | Supported | `shade-kit-research` one subagent per focus with `spawn_agent`; `shade-kit-review` in a subagent; skill texts name `spawn_agent` (decision 7) | — | X-SUB; X-SRC `codex-rs/core/src/config/mod.rs`, `codex-rs/core/src/tools/router.rs` (read 2026-11-15) |
| Structured question tool | Supported with a different format (plan mode only by default; at most 3 questions and 2–3 options per the schema text) | Skill texts name `request_user_input`; outside plan mode plain questions (decision 7) | — (questions keep their content and order; see section 6) | X-SRC `codex-rs/core/src/tools/handlers/request_user_input_spec.rs`, `codex-rs/protocol/src/config_types.rs`, `codex-rs/tools/src/tool_config.rs` (read 2026-11-15) |
| Call a skill by name | Supported with a different format (`$name`) | `$shade-kit:shade-kit-<x>`; managed section and `shade-kit-next-steps` use this form (decision 6) | — | X-SKL; X-SRC `codex-rs/skills/src/mentions.rs` (read 2026-11-15) |
| Keep the agent from loading a skill on its own | Supported with a different format (`agents/openai.yaml`) | Convention in the skill texts; no agent-specific file (decision 8) | — (v1 relies on the convention on every agent) | X-SKL; X-SRC `codex-rs/skills/src/model.rs` (read 2026-11-15) |
| Instruction file | Supported (`AGENTS.md`; `CLAUDE.md` only by opt-in) | No new file (decision 5) | — | X-AGM; X-SRC `codex-rs/core/src/agents_md.rs`, `codex-rs/core/src/config/mod.rs` (read 2026-11-15) |
| Frontmatter visibility | Frontmatter shown to the model on a mention | Version stated in the handshake text (06-2, unchanged) | — | X-SRC `codex-rs/ext/skills/src/host_prompt.rs`, `codex-rs/ext/skills/src/fragments.rs` (read 2026-11-15) |
| CLI on Windows | Supported (`codex.exe`; `.cmd` npm shim **Unverified**) | `.exe`, then `.cmd` through `%ComSpec%` with argument refusal (decision 4) | — | X-SRC `scripts/install/install.ps1`, `codex-cli/package.json` (read 2026-11-15) |

## 5. Skills that depend on agent capabilities

**FACT** (`skills/*/SKILL.md` on 2026-11-15):

- **Subagents:** `shade-kit-research` (one general-purpose subagent per focus in parallel, otherwise one after the other in the main session from the same brief; names the Claude Code `Agent` tool), `shade-kit-review` (independent context: "a subagent where the agent supports it"), `shade-kit-implement` (tasks marked "Parallel: yes" may run in parallel subagents where the agent supports them).
- **Structured question tool** (each names `AskUserQuestion`; the fallback to a plain question is written in the text unless noted): `shade-kit-interview`, `shade-kit-phase-entry`, `shade-kit-gate-check`, `shade-kit-finish` (`report: ask`), `shade-kit-drop` (confirmation; "the structured question tool", no fallback written), `shade-kit-next-phase` (feature pick; "when a question tool allows fewer options than there are features, list the features in the question text").
- **Loading a skill by name:** every phase skill loads `shade-kit-phase-entry`, which loads `shade-kit-gate-check`; every skill that ends loads `shade-kit-next-steps`; `refine`, `map`, `diagnose` load `shade-kit-research`; `discuss` and the phase skills that interview load `shade-kit-interview`; `implement` and `quick` load `shade-kit-review`; `finish` loads `shade-kit-report`.
- **Call forms written into the output:** `shade-kit-next-steps`, `shade-kit-next-phase`, `shade-kit-phase-entry`, and the closing lines of several phase skills write `/shade-kit-<x>`. Decision 6 changes `shade-kit-next-steps` (and the texts that name a call form) to the running agent's form.

## 6. Degraded behaviour

Package 08 Step 3: where a capability is missing, the skill keeps its function and changes only how it runs.

| Agent | Function | How it runs differently | Function kept? |
|---|---|---|---|
| Copilot | Research (`shade-kit-research`) | Parallel subagents with `task`; the concurrency limit (2 to 32, section 3.1) can make more focuses run in batches (**Unverified**) | Yes |
| Copilot | Questions (interview, phase entry, gate check, drop, finish, next phase) | `ask_user`; its option limits are undocumented, so the skill texts' Claude Code limits are kept | Yes |
| Copilot | Managed section | Loaded twice (`CLAUDE.md` and `AGENTS.md`), accepted (decision 5) | Yes |
| Copilot | Human approval of a phase transition | Unchanged in a normal session. In autopilot mode `ask_user` is answered automatically (**FACT**, C-LOG), and with `--no-ask-user` the tool is gone; then an approval question could be answered by the agent. This is the known weak point of concept §3.2 (nothing technically stops an agent from approving on its own), not a new loss; the skill texts ask for the human's answer in the conversation. | Yes (same weak point as on Claude Code) |
| Codex | Questions | Outside plan mode (the default) plain questions, one topic per message, as the skill texts already describe for an agent without the tool; in plan mode `request_user_input` with at most 3 options (proposal plus two alternatives) and 3 questions per call | Yes |
| Codex | Research | Parallel subagents with `spawn_agent` (default limit 6 threads) | Yes |
| Codex | Call form in the closing message | `$shade-kit:shade-kit-<x>` instead of `/shade-kit-<x>` (decision 6) | Yes |
| Codex | Update outcome | Always `updated`, never `current` (decision 3); the same holds for Copilot | Yes (the report is less precise, no function lost) |
| Both | Phase skills only by the user's call | Convention, as on Claude Code (decision 8) | Yes (same weak point as on Claude Code) |

No function is lost on either agent in its default configuration. One case needs a human decision: if a user switches subagents off (Codex `agents.enabled = false`), the review of `implement` and `quick` would lose its independence; see the **Open** fallback in [section 2](#2-decisions). Research does not lose a function there, because `shade-kit-research` already runs the focuses one after the other.

## 7. Agent-neutral options

Concept §1 constraint and `CLAUDE.md`: a capability is built for one agent only when an agent-neutral version would lose function or precision.

- Skill source and manifests: agent-neutral (decision 1), nothing lost.
- Instruction file `AGENTS.md`: agent-neutral for Copilot and Codex; `CLAUDE.md` stays for Claude Code.
- Call forms and tool names: cannot be agent-neutral without losing precision (a wrong call form does not start the skill), so the texts name each agent's form (decisions 6, 7).
- User-only skills: an agent-specific mechanism exists for each agent, but none would be agent-neutral or reliable today, so v1 keeps the convention (decision 8).
- Install sequences: one per agent, because the CLIs differ; the marketplace decision (add, keep, switch, conflict) is shared by the agents in the engine (**Decided** on 2026-11-15, package 08, Gate 2).

## 8. Checks by hand for FP08-AC2 and FP08-AC3

Neither CLI may be installed on the development machine (user decision), so the session part of FP08-AC2 and FP08-AC3 is checked by hand by a person who has the CLI installed and signed in. The install part is proven by tests with a faked CLI.

### Preparation (both agents)

1. Record the operating system, `copilot --version` or `codex --version`, and `shade-kit version --json` (engine version). The skills expect kit version `0.8.0`, so the engine's major.minor must be `0.8`; with another version the handshake fails with exit code 50, which is a valid result of the handshake but not the check.
2. Use the engine of the working tree: `dotnet run --project src/CreativeCoders.ShadeKit.Cli -- <command>` (below written as `shade-kit <command>`), or an installed tool of the version under test.
3. Developer override: to install the working tree instead of the tag, prefix the install with `SHADE_KIT_MARKETPLACE_SOURCE=<absolute path of the working tree>` (or `owner/repo#ref`). Without it the engine installs from `CreativeCodersTeam/shade-kit` at `v<engine version>`, or `main` for a prerelease engine or a missing tag. Record which source was used.
4. Create a scratch repository: `mkdir <scratch> && cd <scratch> && git init && shade-kit init`.

### Copilot (FP08-AC2)

1. `shade-kit integrations install --agent copilot --json`. Record the whole envelope. Expected: `ok: true`, agent `copilot`, outcome `installed`, `pluginId` `shade-kit@creativecoders-shade-kit`.
2. Record the raw output of `copilot plugin list --json` and `copilot plugin marketplace list --json` (needed to confirm the JSON readers). Expected: an entry `shade-kit` from marketplace `creativecoders-shade-kit`, enabled.
3. `shade-kit version --json`: record `integrations`; expected an entry for `copilot` with the kit version (`null` kit version and ref for a directory source).
4. In the scratch repository start `copilot` (interactive). Run `/skills` and record whether the 19 shade-kit skills are listed and under which names.
5. Type `/shade-kit-next-phase`. Allow the shell command when asked. Check and record:
   - the first command the agent runs is `shade-kit version --json --expect 0.8.0` (the handshake);
   - it then runs `shade-kit feature current --json`, and, with no feature, ends with "No current feature, and no active feature to pick." and a closing message, which shows that it loaded `shade-kit-next-steps` by name (record whether the `skill` tool was called and with which name);
   - the calls in the closing message use the form `/shade-kit-<x>`.
6. Optional: type "Use the /shade-kit-next-phase skill" instead, and record whether it loads.
7. Run `shade-kit integrations update --json` and record the envelope (expected outcome `updated`).
8. Windows only, with an npm install: record whether `copilot.exe` or only `copilot.cmd` is on the `PATH`, and repeat step 1.

### Codex (FP08-AC3)

1. `shade-kit integrations install --agent codex --json`. Record the envelope. Expected: `ok: true`, agent `codex`, outcome `installed`, `pluginId` `shade-kit@creativecoders-shade-kit`.
2. Record the raw output of `codex plugin list --json` and `codex plugin marketplace list --json`, the `marketplaceSource` object in particular.
3. `shade-kit version --json`: record `integrations`; expected an entry for `codex`.
4. In the scratch repository start `codex` (interactive, default mode). Run `/skills` and record whether the shade-kit skills are listed as `shade-kit:shade-kit-<x>`.
5. Type `$shade-kit:shade-kit-next-phase`. Allow the command. Check and record the same points as for Copilot step 5; the calls in the closing message use the form `$shade-kit:shade-kit-<x>`. Record whether the model read `shade-kit:shade-kit-next-steps` (its `SKILL.md` path) when the text said to load it.
6. Optional, for the question fallback: with an active feature that has two candidates, or in `shade-kit-drop`, record that a question is asked as plain text in default mode; repeat in plan mode if the CLI offers it and record whether `request_user_input` is used.
7. Run `shade-kit integrations update --json` and record the envelope (expected outcome `updated`). With the developer override (a directory), record that the engine runs only `codex plugin add` for the kept marketplace, no `marketplace upgrade` (D1), and that the content is refreshed.
8. Windows only, with an npm install: record whether `codex.exe` or only `codex.cmd` is on the `PATH`, and repeat step 1.

### What to record

For each step: the command or input, the full output (JSON verbatim), pass or fail against the expectation, and the agent and engine versions. FP08-AC2 or FP08-AC3 passes when step 5 runs the handshake `shade-kit version --json --expect 0.8.0` after a marketplace install by step 1. Attach the record to package 08.

### Unverified points the run should also confirm

1. `"source": "./"` in `.claude-plugin/marketplace.json` resolves to the repository root (Copilot; Codex from the source but not run).
2. A tag works as the ref: Copilot `owner/repo#v0.8.0`, Codex `marketplace add owner/repo --ref v0.8.0`.
3. `copilot plugin list --json`, `copilot plugin marketplace list --json`, `codex plugin list --json`, `codex plugin marketplace list --json` work without a signed-in account and print only one JSON value to stdout.
4. The shape of `copilot plugin marketplace list --json`, and the fields of Codex `marketplaceSource`.
5. Copilot `plugin update` and `plugin enable` take `shade-kit@creativecoders-shade-kit` (or only the bare name `shade-kit`).
6. Copilot `marketplace update` followed by `plugin update` brings new commits of the ref; Codex `marketplace upgrade` followed by `plugin add` does.
7. Codex `marketplace upgrade` of a directory marketplace fails, and its exit code (the engine does not run it, D1).
8. Exit code 0 of every successful change command, and non-zero on failure.
9. On Windows: the npm shims `copilot.cmd` and `codex.cmd`, and that `%ComSpec% /d /s /c "<path> <args>"` runs them with the engine's arguments.
10. A skill text that says "load `shade-kit-next-steps`" loads that skill on both agents.
11. Copilot shows no frontmatter to the model; the skill list holds name and description.
12. Copilot auto-update does not change the `copilot plugin` behaviour during a run.

## 9. Not verified / open

- **Unverified:** everything that needs a running CLI: the end-to-end install from the marketplace, loading `shade-kit-next-phase` in a session, the version handshake there (FP08-AC2, FP08-AC3), and every point of the list in section 8.
- **Unverified:** the Copilot `ask_user` parameter schema (options and questions per call, header length).
- **Unverified:** which of the two Copilot subagent limit statements applies (section 3.1).
- **Unverified:** the YAML library of Copilot, and how `serde_yaml` 0.9 types `0.8.0`; both ignore the field.
- **Unverified:** Copilot reading nested `AGENTS.md` files, and whether a linked `AGENTS.md` → `CLAUDE.md` is loaded once.
- **Unverified:** whether the shade-kit descriptions fit Codex's skill-list budget unshortened.
- **Unverified:** the size of the whole-tree copy in the Codex plugin cache (the plugin root is the repository root); the same question is open for Claude Code (package 07).
- **Unverified:** whether `%LOCALAPPDATA%\Programs\OpenAI\Codex\bin` is put on the `PATH` by the Codex install script, and the file name in the Copilot Windows archives.
- Decided on 2026-11-15: the handling of a kept Codex directory marketplace (D1), the Copilot switch order (D2), keep or switch without a reported Codex ref (D5), the Copilot conflict hint (D6) (section 2).
- **Open:** the fallback of the independent review when subagents are switched off (D3, section 2; needs a human decision).
- **Open:** Unicode lookalikes in a `.cmd` shim path or argument (decision 4).
- Not researched: hooks and MCP configuration (out of scope of package 08, as in package 07).
