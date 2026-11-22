# Plugin and skill versioning in Claude Code, GitHub Copilot CLI, and OpenAI Codex CLI, and how a skill finds its own version or bundled data at runtime

Research date: 2026-11-21. Local agent versions checked on this machine: Claude Code 2.1.330 (`claude --version`), GitHub Copilot CLI 1.0.114 (`copilot --version`). Codex CLI is not installed locally; Codex facts come from the docs and the `openai/codex` source on `main` (latest release `rust-v0.171.0`, 2026-11-20, from `gh api repos/openai/codex/releases/latest`).

Labels: **FACT** = checked against a primary source (official docs, the agent's source code or changelog, or the installed binary or files on this machine). **Unverified** = not confirmed.

Context: shade-kit's `.claude-plugin/marketplace.json` lists one plugin `shade-kit` with `"source": "./"` (the marketplace root). Neither the entry nor `plugin.json` sets a `version` (**FACT**, read from the repository file).

---

## Q1. Claude Code: what plugin.json `version` means, update detection, caching, `ref`/`sha`, and `claude plugin update`

### Takeaway
Claude Code computes one "version" per installed plugin and uses it both as the update check and as the cache directory name. The lookup order is: `plugin.json` `version`, then the marketplace entry's `version`, then a value from the source type. For a relative-path plugin in a Git-hosted marketplace (shade-kit's case), the fallback is the commit SHA. A pinned `version` freezes users on the cached copy until the string changes. A marketplace added from a local path loads the plugin in place, so edits show up without any version change.

### Cited Findings
- **FACT** `plugin.json` `version`: "A version string, not checked against semver. Setting it pins the plugin to that version until you change it". The pin doesn't apply to `command`-source plugins, to plugins from a marketplace hosted on claude.ai, or to plugins loaded in place from a marketplace added from a local path. [Plugin manifest reference](https://code.claude.com/docs/en/plugins-reference)
- **FACT** "Claude Code computes a version for every plugin it installs, and that version is how it detects an update. `claude plugin update` and background auto-update compute the version again and don't replace the cached copy when it matches what `installed_plugins.json` records." "The version also names the plugin's cache directory." [Plugin loading reference: Versions and updates](https://code.claude.com/docs/en/plugins/loading.md)
- **FACT** Resolution order, quoted: "1. The `version` field in the plugin's manifest comes first 2. Then the `version` field in the plugin's marketplace entry 3. When neither is set, the version comes from the source type". The source-type fallbacks are:
  - `github`, `url`, `git-subdir`: "The commit SHA of the source, shortened to 12 characters".
  - "Relative path inside a Git-hosted marketplace": "The commit SHA of the installed directory".
  - A local directory that is not a git repository: `unknown`.
  - `npm`: `unknown`.
  - `archive`: the SHA-256 digest, shortened to 12 characters.
  
  [Plugin loading reference](https://code.claude.com/docs/en/plugins/loading.md)
- **FACT** "a manifest that pins `"version": "1.0.0"` keeps every user on the cached copy until its author changes the string, however many commits they push. To let users track commits instead, leave `version` out of both the manifest and the entry." [Plugin loading reference](https://code.claude.com/docs/en/plugins/loading.md)
- **FACT** Manifest `version` takes precedence over the marketplace entry `version`. `claude plugin validate` warns `Entry declares version "x" but <path>/plugin.json says "y". At install time, plugin.json wins`. [Plugin manifest reference: Metadata precedence](https://code.claude.com/docs/en/plugins-reference); [Marketplace reference](https://code.claude.com/docs/en/plugins/marketplace-reference.md)
- **FACT** `claude plugin validate` reports a missing `version` as a warning, and `--strict` turns warnings into failures. [Plugin manifest reference](https://code.claude.com/docs/en/plugins-reference)
- **FACT** Cache layout: `cache/<marketplace>/<plugin>/<version>/`, "`${CLAUDE_PLUGIN_ROOT}` points at this directory". `installed_plugins.json` records `scope`, `installPath`, and `version`. The plugins root is `~/.claude/plugins`, or `CLAUDE_CODE_PLUGIN_CACHE_DIR` when set. When a plugin is updated or uninstalled, the previous version directory gets an `.orphaned_at` marker and is removed 14 days later. [Plugin loading reference](https://code.claude.com/docs/en/plugins/loading.md)
- **FACT** (local, Claude Code 2.1.330) `~/.claude/plugins/installed_plugins.json` on this machine shows both schemes side by side:
  - Unversioned plugins are cached under a 12-character SHA, for example `cc-ai-dev@creativecoders-ai-store` → `cache/creativecoders-ai-store/cc-ai-dev/4972bfa14908`, and `github@claude-plugins-official` → `.../github/73c8cf3ea7e2`, with eight SHA directories accumulated.
  - Versioned plugins are cached under the version string, for example `superpowers` → `.../superpowers/7.0.2` and `security-guidance` → `.../2.0.17` … `2.0.21`.
- **FACT** Plugin sources `github`, `url`, and `git-subdir` take `ref` ("a branch or tag. Defaults to the repository's default branch") and `sha` ("a full 40-character lowercase commit SHA. When you set both `ref` and `sha`, Claude Code checks out `sha`"). [Marketplace reference: Plugin sources](https://code.claude.com/docs/en/plugins/marketplace-reference.md)
- **FACT** Marketplace sources: `claude plugin marketplace add` accepts `owner/repo`, `owner/repo@ref`, or `owner/repo#ref` for `github`, and `#ref` for `git`. A full commit SHA as a marketplace `ref` "requires Claude Code v2.1.324 or later". [Marketplace reference: Marketplace sources](https://code.claude.com/docs/en/plugins/marketplace-reference.md)
- **FACT** The marketplace file itself can carry a top-level `version` (or `metadata.version`) as the "Marketplace manifest version". Entry and plugin `metadata` are free-form, and "Claude Code doesn't read it". [Marketplace reference](https://code.claude.com/docs/en/plugins/marketplace-reference.md)
- **FACT** Changelog history:
  - 2.1.14 "Added support for pinning plugins to specific git commit SHAs".
  - 2.1.97 "Fixed `claude plugin update` reporting 'already at the latest version' for git-based marketplace plugins when the remote had newer commits".
  - 2.1.118 "Added `claude plugin tag` to create release git tags for plugins with version validation".
  - 2.1.323 "Added `engines["claude-code"]` to `plugin.json`".
  
  [Claude Code CHANGELOG](https://github.com/anthropics/claude-code/blob/main/CHANGELOG.md)
- **FACT** Before an install, Claude Code refreshes the named marketplace only for `claude plugin install name@marketplace`. The refresh is skipped for a marketplace added from a local `file`/`directory` source. Auto-update is off by default for third-party marketplaces. [Plugin loading reference](https://code.claude.com/docs/en/plugins/loading.md)
- **FACT** `engines: { "claude-code": "<semver range>" }` in `plugin.json` or in a marketplace entry: "plugins outside their stated Claude Code version range are not installed, updated or loaded". It requires 2.1.323+, and 2.0.15–2.1.75 reject a manifest that has the field. It constrains the agent's version, not an external CLI. [Plugin manifest reference](https://code.claude.com/docs/en/plugins-reference); [CHANGELOG 2.1.323](https://github.com/anthropics/claude-code/blob/main/CHANGELOG.md)
- **FACT** Plugin-to-plugin `dependencies` with semver ranges resolve against git tags named `<plugin-name>--v<version>`. `claude plugin tag` creates such a tag after checking that `plugin.json` and the marketplace entry agree on the version. "Constraint checks use the tag's version rather than the `version` in `plugin.json`". A tag-resolved dependency shows as e.g. `2.1.0-8713c5b11005`. [Plugin dependencies](https://code.claude.com/docs/en/plugins/dependencies.md)

### Inferences
- shade-kit today (relative path `./`, no `version`) gets the commit SHA as its Claude Code version. Every commit on the installed ref counts as an update. That is the documented "track commits" mode, and it fits installs from a tag because the tag's commit is fixed.
- If shade-kit set `plugin.json` `version`, Claude Code would stop treating new commits on `main` as updates until the string changed. That matters for the `main` fallback ref the engine uses for prereleases.
- No Claude Code mechanism pins a dependency on an external, non-plugin binary such as the `shade-kit` CLI. `engines` covers only `claude-code`, and `dependencies` covers only plugins.

### Gaps
- I found no documentation on whether `claude plugin list --json` exposes the marketplace `ref`. The engine already relies on that output, so check it against the existing engine code.

---

## Q2. Claude Code: what the model sees when a skill loads (base directory, frontmatter, substitutions), bundled files, and local development

### Takeaway
Claude Code offers the richest runtime hooks of the three agents. It substitutes `${CLAUDE_SKILL_DIR}`, `${CLAUDE_PLUGIN_ROOT}`, `${CLAUDE_PLUGIN_DATA}`, `${CLAUDE_PROJECT_DIR}`, `${CLAUDE_SESSION_ID}`, `$ARGUMENTS`, and `${user_config.KEY}` in the skill body. It runs `` !`command` `` placeholders before the model sees the content. It also starts injected skill content with a "Base directory for this skill: <path>" line. The frontmatter (including `metadata`) is not acted on, and the shade-kit team observed that the model doesn't receive it.

### Cited Findings
- **FACT** String substitutions in `SKILL.md`, quoted from the docs:
  - `${CLAUDE_SKILL_DIR}`: "The directory containing the skill's `SKILL.md` file. For plugin skills, this is the skill's subdirectory within the plugin, not the plugin root."
  - `${CLAUDE_PLUGIN_ROOT}`: "The plugin's installation directory. Substituted only in plugin skills."
  - `${CLAUDE_PLUGIN_DATA}`: "The plugin's persistent data directory, which survives plugin updates."
  - Also `${CLAUDE_PROJECT_DIR}`, `${CLAUDE_SESSION_ID}`, `${CLAUDE_EFFORT}`, `$ARGUMENTS`, `$ARGUMENTS[N]`/`$N`, and `$name`.
  
  [Claude Code skills docs](https://code.claude.com/docs/en/skills)
- **FACT** `${CLAUDE_SKILL_DIR}` was "Added ... for skills to reference their own directory in SKILL.md content" in Claude Code 2.1.69. [CHANGELOG 2.1.69](https://github.com/anthropics/claude-code/blob/main/CHANGELOG.md)
- **FACT** Path variables in "Skill, command, and agent content" resolve "Anywhere in the Markdown body". "The variables aren't present in the environment of commands Claude runs through the Bash tool ... write the `${...}` reference in the Markdown body instead, and Claude Code substitutes the path inline when it loads the content." [Plugin manifest reference: Environment variables](https://code.claude.com/docs/en/plugins-reference)
- **FACT** `${user_config.KEY}` is substituted in "skill and agent content" (non-sensitive values only). [Plugin manifest reference](https://code.claude.com/docs/en/plugins-reference)
- **FACT** Dynamic context injection: "The `` !`<command>` `` syntax runs shell commands before the skill content is sent to Claude. The command output replaces the placeholder". Further rules from the same page:
  - "A failed command aborts the entire skill invocation".
  - It can be turned off by policy with `"disableSkillShellExecution": true`.
  - A fenced ```` ```! ```` block is the multi-line form.
  
  [Claude Code skills docs](https://code.claude.com/docs/en/skills)
- **FACT** Officially recommended pattern for bundled scripts: ``Run `${CLAUDE_SKILL_DIR}/scripts/render.sh <csv-file>` ``, with `allowed-tools: Bash(${CLAUDE_SKILL_DIR}/scripts/render.sh *)`. Supporting docs are linked relatively (`[reference.md](reference.md)`). [Claude Code skills docs](https://code.claude.com/docs/en/skills)
- **FACT** `metadata` frontmatter is a "Free-form YAML map for your own key-value data ... read by your own tooling from `SKILL.md`. Claude Code doesn't act on its contents". The docs list no `version` frontmatter field. [Claude Code skills docs](https://code.claude.com/docs/en/skills)
- **FACT** (local binary, Claude Code 2.1.330) The binary contains the literal `Base directory for this skill: `. It also contains a function that treats content starting with `"Base directory for this skill: "`, `"ARGUMENTS: "`, `"Skill arguments: "`, or `"<system-reminder>"` as injected skill prefixes. Found with `strings`/`grep -a` on `/opt/homebrew/Caskroom/claude-code@latest/2.1.330/claude`. The exact template that builds the line was not extracted.
- **FACT** (third party, consistent with the binary) Issue trackers describe the "Base directory for this skill" line that Claude Code adds when a skill loads, and models deriving script paths from it when `$CLAUDE_PLUGIN_ROOT` is empty in Bash. [anthropics/claude-code#12541](https://github.com/anthropics/claude-code/issues/12541); [anthropics/claude-code#19922](https://github.com/anthropics/claude-code/issues/19922); [pandas-studio/agent-team-plugins#208](https://github.com/pandas-studio/agent-team-plugins/issues/208); [bjcoombs/ai-native-toolkit#329](https://github.com/bjcoombs/ai-native-toolkit/issues/329)
- **FACT** (project observation, already recorded in shade-kit CLAUDE.md / FP 06-2) Claude Code 2.1.x doesn't show the frontmatter to the model for project-level skills.
- **FACT** Local plugin development:
  - "`--plugin-dir` and skills-directory plugins: the directory loads in place and is never copied".
  - "Relative-path plugins in a marketplace you added from a local path: the plugin loads in place ... Your edits to the source directory take effect at the next session start or `/reload-plugins`, and you don't need to increase the version."
  - A `--plugin-dir` plugin with the same manifest name replaces an installed marketplace plugin (id `<name>@inline`).
  
  [Plugin loading reference](https://code.claude.com/docs/en/plugins/loading.md)
- **FACT** Skill files under `~/.claude/skills/` and `.claude/skills/` are watched live. Plugin `agents/`, `.mcp.json`, and `hooks/` need `/reload-plugins`. [Claude Code skills docs](https://code.claude.com/docs/en/skills)
- **FACT** `bin/` at the plugin root: "Files here are on the Bash tool's `PATH` while the plugin is enabled". claude.ai/Cowork don't install a plugin that has this directory. [Plugin manifest reference: Standard layout](https://code.claude.com/docs/en/plugins-reference)

### Inferences
- In Claude Code, a skill can reach a bundled file without hardcoding anything. Examples: ``run `shade-kit version --json --expect-file ${CLAUDE_PLUGIN_ROOT}/VERSION` `` or `${CLAUDE_SKILL_DIR}/../../VERSION`. With `` !`...` ``, the handshake could even run before the model reads the skill. Both forms are Claude-only syntax (see Q5 and Q6).
- `` !`...` `` aborts the whole skill on a non-zero exit. A handshake that returns exit code 50 on a mismatch would abort the skill instead of letting the skill explain the mismatch, unless the command always exits 0.
- `${CLAUDE_PLUGIN_ROOT}` changes with every version, because it points at the version directory. It is correct for reading bundled files and must not be used for state.

### Gaps
- The exact wording and position of the "Base directory" line (always the first line? absolute path? also for plugin skills?) were not captured from a live session. A skill-scenario run could confirm it.
- No official Claude Code doc page states that the frontmatter is stripped before injection. That rests on the project observation.

---

## Q3. GitHub Copilot CLI: plugin format, manifests, versions, skill loading, path exposure, local development

### Takeaway
Copilot CLI reads `.claude-plugin/plugin.json` and `.claude-plugin/marketplace.json` as legacy-compatible manifests. It supports `ref`/`sha` pins, installs to `~/.copilot/installed-plugins/<marketplace>/<plugin>`, and loads local directory marketplaces live. It strips the frontmatter from injected skill content (since 1.0.48). It documents no variable substitution in `SKILL.md`; `PLUGIN_ROOT`/`CLAUDE_PLUGIN_ROOT` reach only hooks, MCP, LSP, and agent `mcp-servers`. Whether the model is told the skill's directory is only implied by the docs.

### Cited Findings
- **FACT** Manifest lookup: Agent Plugins 1.0 "requires the manifest at the plugin root". For legacy plugins it checks "`.github/plugin/plugin.json`, or `.claude-plugin/plugin.json` (checked in this order)". Marketplaces: "`.github/plugin/marketplace.json`, or `.claude-plugin/marketplace.json` (checked in this order)". [Copilot CLI plugin reference](https://docs.github.com/en/copilot/reference/cli-plugin-reference)
- **FACT** `version`: for legacy manifests a "Semantic version (e.g., `1.0.0`)", for Agent Plugins 1.0 "Version string. Semantic Versioning is recommended." Optional in both. `/plugin` "flags an installed plugin or marketplace when a newer version is available upstream". The docs don't say whether this compares version strings or commit SHAs. [Copilot CLI plugin reference](https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-plugin-reference)
- **FACT** Marketplace pins: `copilot plugin marketplace add` accepts `owner/repo#ref`. Entry sources `github`/`url` accept `ref` and `sha` ("must be a full 40-character commit SHA", "immune to force-pushes or tag/branch moves"). [Copilot CLI plugin reference](https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-plugin-reference)
- **FACT** `copilot plugin list --json` rows have the shape `{ name, marketplace?, version?, enabled, source, installedFrom? }`. 1.0.85 changed the output to a flat array. [Copilot CLI plugin reference](https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-plugin-reference); [Copilot CLI changelog](https://github.com/github/copilot-cli/blob/main/changelog.md)
- **FACT** Paths:
  - Installs: `~/.copilot/installed-plugins/MARKETPLACE/PLUGIN-NAME` (no version segment) and `~/.copilot/installed-plugins/_direct/SOURCE-ID/`.
  - Marketplace cache: `~/Library/Caches/copilot/marketplaces/` (macOS) or `~/.cache/copilot/marketplaces/` (Linux), overridable with `COPILOT_CACHE_HOME`.
  
  [Copilot CLI plugin reference](https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-plugin-reference)
- **FACT** Update behaviour:
  - `copilot plugin update NAME` or `--all`.
  - "Path-sourced plugins in a local (directory-source) marketplace now load live from their real directory, so editing one takes effect on `/restart` or a new session — no `/plugin update`" (1.0.81).
  - Opt-in `autoUpdate: true` on `extraKnownMarketplaces` (1.0.79).
  
  [Copilot CLI changelog](https://github.com/github/copilot-cli/blob/main/changelog.md); [plugin reference](https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-plugin-reference)
- **FACT** Frontmatter: "Skill content injected to the model no longer includes YAML frontmatter metadata" (Copilot CLI 1.0.48). [Copilot CLI changelog](https://github.com/github/copilot-cli/blob/main/changelog.md)
- **FACT** Variables:
  - `${PLUGIN_ROOT}`/`${PLUGIN_DATA}` (aliases `CLAUDE_PLUGIN_DATA`, `COPILOT_PLUGIN_DATA`) are expanded in stdio MCP `args`/`env`/`cwd`, LSP config, and plugin-shipped agent `mcp-servers` frontmatter.
  - 1.0.26: "Plugin hooks receive PLUGIN_ROOT, COPILOT_PLUGIN_ROOT, and CLAUDE_PLUGIN_ROOT env vars". 1.0.111: "Expand ${COPILOT_PLUGIN_ROOT} in plugin hook working directories and environment values".
  - The docs describe no substitution in `SKILL.md` content.
  
  [Copilot CLI plugin reference](https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-plugin-reference); [Copilot CLI changelog](https://github.com/github/copilot-cli/blob/main/changelog.md)
- **FACT** Skill docs:
  - Bundled resources: "include scripts or other resources in the skill directory and reference them in your `SKILL.md` instructions". The example instruction is "run the `convert-svg-to-png.sh` script from this skill's base directory".
  - When a skill is invoked, Copilot "automatically discovers all of the files in the skill's directory and makes them available alongside the skill's instructions".
  - Locations: `.github/skills`, `.claude/skills`, `.agents/skills` (project), `~/.copilot/skills`, `~/.agents/skills` (personal).
  - Reload with `/skills reload`.
  
  [Copilot CLI: create skills](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/create-skills)
- **FACT** Agent Plugins 1.0 is "an open standard that packages agent skills and MCP servers into one installable plugin". It is governed independently of any one vendor (AWS, Anysphere, Microsoft, OpenAI, Vercel, Google) and supported in VS Code, Copilot CLI, the Copilot SDK, and the Copilot app. Existing plugins need no migration. Spec: https://github.com/agentplugins/agent-plugins-spec/blob/main/spec/1.0.0.md. [GitHub changelog 2026-08-12](https://github.blog/changelog/2026-08-12-agent-plugins-1-0-in-vs-code-copilot-cli-and-the-copilot-app)

### Inferences
- `${CLAUDE_SKILL_DIR}` and `${CLAUDE_PLUGIN_ROOT}` in a skill body most likely reach the Copilot model as literal text. The docs document no SKILL.md substitution; not tested.
- The install path has no version segment, so on Copilot the plugin's on-disk location stays stable across updates, unlike Claude Code.

### Gaps
- **Unverified**: whether Copilot's skill injection includes an explicit absolute skill-directory path. The Copilot 1.0.114 binary is a compressed Mach-O and plain string search found nothing. The npm package `@github/copilot@1.0.114` contains only a loader. The docs' "this skill's base directory" wording suggests it does.
- **Unverified**: how Copilot detects "a newer version upstream" for a plugin without a `version` (commit SHA or nothing).
- I didn't read the Agent Plugins 1.0 spec text itself (version semantics, variables).

---

## Q4. OpenAI Codex CLI: plugins, marketplaces, versions, reported refs, how skills reach the model, local folders

### Takeaway
Codex lists every skill to the model with its file path. The model reads `SKILL.md` itself and is told to resolve relative paths against that file's directory. This makes "the skill's own directory" portable on Codex, and the model also sees the raw frontmatter. Codex plugins take `version` from the manifest. When it is missing, the cache segment is the fixed string `local` (legacy) or `1.0.0` (Agent Plugins), never a commit SHA. Codex documents no SKILL.md variable substitution.

### Cited Findings
- **FACT** Codex's skill catalog prompt (source `codex-rs/ext/skills/src/catalog_prompt.rs`, last changed 2026-10-27, commit `2edc8dc`), quoted:
  - "Each entry includes a name, description, and source locator. `file` locators are on the host filesystem".
  - Usage rule 1: "After deciding to use a skill, the main agent must read its `SKILL.md` completely before taking task actions. For a `file` entry, open the listed path."
  - The host-alias variant says: "When `SKILL.md` references relative paths (e.g., `scripts/foo.py`), resolve them relative to the directory containing that expanded `SKILL.md` first", and "If `scripts/` exist, prefer running or patching them".
  
  [openai/codex catalog_prompt.rs](https://github.com/openai/codex/blob/main/codex-rs/ext/skills/src/catalog_prompt.rs)
- **FACT** Docs: Codex starts with "each skill's name and description" and its initial list also includes "each skill's file path". The list budget is 2% of the context window or 8,000 characters, and `skills.max_context_tokens` raises it. Skill locations: `.agents/skills` from the working directory up to the repository root, `$HOME/.agents/skills`, `/etc/codex/skills`, and bundled system skills. "Codex detects skill changes automatically", restart if not. No variable substitution is described. [Codex: build skills](https://learn.chatgpt.com/docs/build-skills) (redirect target of developers.openai.com/codex/skills)
- **FACT** Codex parses only `name`, `description`, and `metadata.short-description` from the frontmatter (`struct SkillFrontmatter` / `SkillFrontmatterMetadata`). [openai/codex skills/src/parser.rs](https://github.com/openai/codex/blob/main/codex-rs/skills/src/parser.rs)
- **FACT** Manifests:
  - The portable entry point is a root `plugin.json`, with "Existing `.codex-plugin/plugin.json` files remain supported as a compatibility fallback". OpenAI "also accepts legacy and Claude-compatible manifests". The source tests reference `.claude-plugin/plugin.json` as `ALTERNATE_PLUGIN_MANIFEST_RELATIVE_PATH`.
  - Marketplaces: `$REPO_ROOT/.agents/plugins/marketplace.json`, `~/.agents/plugins/marketplace.json`, and the legacy-compatible `$REPO_ROOT/.claude-plugin/marketplace.json`.
  
  [Codex: build plugins](https://developers.openai.com/codex/plugins/build); [openai/codex core-plugins/src/manifest.rs](https://github.com/openai/codex/blob/main/codex-rs/core-plugins/src/manifest.rs)
- **FACT** Cache: `~/.codex/plugins/cache/$MARKETPLACE_NAME/$PLUGIN_NAME/$VERSION/`, "For local plugins, `$VERSION` is `local`". `codex plugin marketplace upgrade` refreshes configured marketplaces. [Codex: build plugins](https://developers.openai.com/codex/plugins/build)
- **FACT** Source: `plugin_version_for_install_manifest` uses the manifest `version` when present. Otherwise it uses `DEFAULT_AGENT_PLUGIN_VERSION = "1.0.0"` for Agent Plugins or `DEFAULT_PLUGIN_VERSION = "local"` for others. An invalid version string on an Agent Plugin becomes `agent-plugins-<sha256 prefix>`. [openai/codex core-plugins/src/store.rs](https://github.com/openai/codex/blob/main/codex-rs/core-plugins/src/store.rs); [core-plugin-common/src/installed.rs](https://github.com/openai/codex/blob/main/codex-rs/core-plugin-common/src/installed.rs)
- **FACT** (project, CLAUDE.md) Codex reports no marketplace ref, has no `plugin update` (`codex plugin add` installs or replaces), and shade-kit's `version` reports Codex `kitVersion`/`ref` as `null`.

### Inferences
- On Codex the model opens `SKILL.md` with a file read, so it sees the whole file, frontmatter included. A `metadata.version` in the frontmatter is therefore visible to the Codex model, unlike in Claude Code and Copilot. This follows from the "open the listed path ... read its SKILL.md completely" rule; not tested in a live session.
- Because the model knows the absolute `SKILL.md` path, a Codex skill can run "the `VERSION` file two directories above this skill's `SKILL.md`" or "this skill's `scripts/x`" without any substitution.
- Without `version`, a Codex update replaces the files in the same `local` directory. A `version` in `plugin.json` would give per-version cache directories on Codex.

### Gaps
- I didn't check whether `codex plugin list --json` shows the manifest `version`.
- No live Codex session was run to see the exact rendered skill entry (absolute path or alias plus roots table).

---

## Q5. Agent Skills standard (agentskills.io): `metadata`, `version`, `compatibility`, bundled resources

### Takeaway
The spec has no top-level `version` field. `metadata` is a free string-to-string map, and the spec's own example puts `version: "1.0"` there, quoted. `compatibility` is free text (at most 500 characters) for environment requirements and is not machine-checkable. Bundled files are referenced by paths relative to the skill root, kept one level deep.

### Cited Findings
- **FACT** Frontmatter fields: `name` (required, must match the directory name), `description` (required), `license`, `compatibility`, `metadata`, and `allowed-tools` (experimental). [Agent Skills specification](https://agentskills.io/specification)
- **FACT** `metadata`: "A map from string keys to string values. Clients can use this to store additional properties not defined by the Agent Skills spec. We recommend making your key names reasonably unique". The example is `metadata: { author: example-org, version: "1.0" }`. [Agent Skills specification](https://agentskills.io/specification)
- **FACT** `compatibility`: "Max 500 characters. Indicates environment requirements (intended product, system packages, network access, etc.)", with examples "Requires git, docker, jq, and access to the internet" and "Requires Python 3.14+ and uv". "Most skills do not need the `compatibility` field." [Agent Skills specification](https://agentskills.io/specification)
- **FACT** File references: "use relative paths from the skill root: `See [the reference guide](references/REFERENCE.md)` ... `scripts/extract.py`". "Keep file references one level deep from `SKILL.md`." Optional directories: `scripts/`, `references/`, `assets/` (templates, data files, schemas). [Agent Skills specification](https://agentskills.io/specification)
- **FACT** Claude Code rejects non-spec fields "Outside Claude Code" with the error "Unexpected key(s) in SKILL.md frontmatter ... Allowed properties are: allowed-tools, compatibility, description, license, metadata, name". [Claude Code skills docs](https://code.claude.com/docs/en/skills)

### Inferences
- shade-kit writes `metadata.version: 0.9.0` unquoted. The spec says the values are strings, so a strict validator could read the value as a YAML string anyway (`0.9.0` is not a valid YAML number). Strings like `1.0` would need quotes.
- `compatibility` could carry a human-readable "Requires shade-kit CLI >= 0.9" note, but no agent checks it, and the model doesn't see the frontmatter in Claude Code or Copilot.

### Gaps
- None material.

---

## Q6. Across all three: the most portable way for a skill to get a version or compatibility value at runtime without hardcoding it in the body

### Takeaway
No substitution syntax works in all three agents. `${CLAUDE_SKILL_DIR}`, `${CLAUDE_PLUGIN_ROOT}`, and `` !`...` `` are Claude Code only. The common ground is that each agent shows the model where the skill lives:
- Claude Code: the "Base directory for this skill:" line, plus substitution.
- Codex: the listed `SKILL.md` path and its relative-path rule.
- Copilot: implied by "this skill's base directory" in its docs.

The most portable pattern is to tell the model, in plain words, to pass "this skill's directory" (or a file relative to it) to the CLI. That is the same wording superpowers uses for its bundled scripts. A Claude-only `${CLAUDE_SKILL_DIR}` form would add precision but would arrive as literal text in the other agents.

### Cited Findings
- **FACT** Claude Code: `${CLAUDE_SKILL_DIR}` and `${CLAUDE_PLUGIN_ROOT}` are substituted in the skill body ([skills docs](https://code.claude.com/docs/en/skills)), and the binary carries the "Base directory for this skill: " prefix (local 2.1.330).
- **FACT** Codex: the model gets each skill's file path and is told to "resolve them relative to the directory containing that expanded `SKILL.md`". [catalog_prompt.rs](https://github.com/openai/codex/blob/main/codex-rs/ext/skills/src/catalog_prompt.rs)
- **FACT** Copilot: the docs use "run the ... script from this skill's base directory" and say the skill directory's files are made "available alongside the skill's instructions". No SKILL.md substitution is documented. [Copilot create skills](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/create-skills)
- **FACT** Frontmatter: Claude Code doesn't act on `metadata` ([skills docs](https://code.claude.com/docs/en/skills)), Copilot strips frontmatter from injected content since 1.0.48 ([changelog](https://github.com/github/copilot-cli/blob/main/changelog.md)), and Codex parses only `name`/`description`/`metadata.short-description` but has the model read the raw file ([parser.rs](https://github.com/openai/codex/blob/main/codex-rs/skills/src/parser.rs)).
- **FACT** Claude Code's `${...}` path variables are not exported to Bash-tool processes ([plugins reference](https://code.claude.com/docs/en/plugins-reference)). The CLI cannot read `CLAUDE_PLUGIN_ROOT` from its environment when the model runs it; the path has to be in the command line.
- **FACT** (local plugin cache) superpowers 7.0.2 writes agent-neutral instructions: "Run this skill's `scripts/task-start PLAN_FILE N` by its absolute path, from the working tree you're executing in", and refers to sibling skills via `../subagent-driven-development/scripts/...`. Read from `~/.claude/plugins/cache/superpowers-dev/superpowers/7.0.2/skills/executing-plans/SKILL.md`.

### Inferences
Candidate patterns for shade-kit, judged only against the facts above:
1. **Plugin-level file read by the CLI** (e.g. `compat.json` or `VERSION` at the plugin root, `shade-kit version --expect-file <path>`).
   - On Claude Code, the path is exact through `${CLAUDE_PLUGIN_ROOT}/VERSION`.
   - On Codex and Copilot, the model must derive it from the skill path ("two levels above this skill's directory").
   - The value lives in one file and changes in one place. A guard test can still pin it to the GitVersion minor.
2. **Pass the skill directory** (`shade-kit version --skill-dir <dir>`): the CLI finds `SKILL.md` or the plugin root by itself. One instruction wording covers all three agents. The CLI can read `metadata.version` from the frontmatter, so the frontmatter stays the only source and the body needs no version text.
   - This works in Claude Code and Copilot even though their models never see the frontmatter, because the CLI, not the model, reads it.
3. **Claude-only `` !`shade-kit version ...` ``** pre-injection: deterministic, but Claude-only syntax. It aborts the skill on a non-zero exit, and Copilot/Codex would show the raw placeholder text.
4. **`plugin.json` `version`**: no agent puts it in the model context, so it is not readable by the skill at runtime either. On Claude Code it also changes update semantics: it stops SHA-based updates on the `main` fallback. Codex uses it as its cache directory segment, and Copilot shows it in `plugin list --json`.
- Local testing: a local directory marketplace (`claude plugin marketplace add <path>`, `copilot plugin marketplace add <path>`, `codex plugin marketplace add <path>`) or `claude --plugin-dir` loads files in place in Claude Code and Copilot. In those modes the skill directory is the working copy, so patterns 1 and 2 read the checked-out file without any version bump.
  - Claude Code: a local-path marketplace loads in place, and a non-git directory gets version `unknown`.
  - Codex: `local` version segment, files copied into the cache; in-place loading not verified.

### Gaps
- **Unverified**: the literal text that Copilot CLI and Codex give the model for the skill directory, and whether the path is absolute. A scenario run in each agent is needed before relying on "this skill's directory" wording.
- **Unverified**: whether a literal `${CLAUDE_SKILL_DIR}` left in the text confuses Copilot or Codex models, or whether they guess its meaning.

---

## Q7. How other plugin and skill authors version their skills and pin CLI dependencies

### Takeaway
Two schemes are common, and both show up in this machine's cache. Some plugins pin a SemVer in `plugin.json`, kept in sync across agent manifests by a bump script (superpowers, Anthropic plugins such as `security-guidance` and `feature-dev`). Others omit `version` and let Claude Code use the commit SHA (Anthropic's `github` and `skill-creator` plugins, CreativeCoders' ai-store). I found no established convention for a skill declaring a minimum external CLI version.

### Cited Findings
- **FACT** (local cache) superpowers 7.0.2 sets `"version": "7.0.2"` in `.claude-plugin/plugin.json` and `.codex-plugin/plugin.json`. Its `.version-bump.json` lists 12 files kept in sync, among them `package.json`, `.claude-plugin/plugin.json`, `.codex-plugin/plugin.json`, `.cursor-plugin/plugin.json`, `.claude-plugin/marketplace.json` (`plugins.0.version`), and `gemini-extension.json`. It ships agent-specific manifests side by side (`.claude-plugin`, `.codex-plugin`, `.cursor-plugin`, `.opencode`, and more). Its skills don't use `${CLAUDE_SKILL_DIR}`; they say "this skill's `scripts/...` by its absolute path". Read from `~/.claude/plugins/cache/superpowers-dev/superpowers/7.0.2/`.
- **FACT** (local cache) `claude-plugins-official` plugins:
  - Pinned SemVer: `security-guidance` (2.0.17–2.0.21 cached), `feature-dev` 1.0.1, `ralph-loop` 1.0.1, `code-simplifier` 1.0.0, `mattpocock-skills` 1.3.1.
  - No version (12-character SHA directories): `github` (eight SHA directories) and `skill-creator`.
  
  From `~/.claude/plugins/installed_plugins.json` and `cache/*/*/*/.claude-plugin/plugin.json`.
- **FACT** (local cache) `context-mode` 2.1.7 bundles its own runtime (`cli.mjs`, `server.mjs`, `SHA256SUMS`) and per-agent folders (`codex/`, `copilot/`, `opencode/`) inside the plugin. Its skills contain no `CLAUDE_PLUGIN_ROOT`/`CLAUDE_SKILL_DIR` references.
- **FACT** Claude Code's own tooling for SemVer plugins: `claude plugin tag` creates `<plugin>--v<version>` tags after checking that `plugin.json` and the marketplace entry agree, and `dependencies` ranges resolve against those tags. [Plugin dependencies](https://code.claude.com/docs/en/plugins/dependencies.md)
- **FACT** Agent version gate: `engines["claude-code"]` (Claude Code 2.1.323+). [CHANGELOG](https://github.com/anthropics/claude-code/blob/main/CHANGELOG.md)

### Inferences
- superpowers' multi-manifest approach goes against shade-kit's single-source rule (no agent-specific manifests, guarded by `SingleSkillSourceTests`). Its version-bump-file pattern is still a precedent for keeping a version in several files with tooling.
- Shipping the runtime inside the plugin (context-mode) removes the CLI-version handshake, but shade-kit ships its engine separately (dotnet tool or binaries).

### Gaps
- I didn't look for published guidance from Anthropic, GitHub, or OpenAI on a skill pinning an external CLI version. None appeared in the pages read. A community survey beyond this machine's cache was not done.
