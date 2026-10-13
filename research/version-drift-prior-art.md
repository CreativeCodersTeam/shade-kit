# Version drift: how Spec Kit, OpenSpec, and gsd-core handle it

Input for [ADR 0001](../docs/adr/0001-shadekit-version-drift.md). This note compares how three similar tools handle drift between three layers:

- **CLI**: the installed engine.
- **Agent files**: the skills, commands, agents, and hooks the tool installs for each AI agent.
- **Project data**: the config, state, and template files the tool writes into a repository.

## Method

| Tool | Repository | Version | Commit |
|---|---|---|---|
| Spec Kit | `github/spec-kit` | 1.1.4.dev0 | `f75d937` (2026-10-13) |
| OpenSpec | `Fission-AI/OpenSpec` | 1.14.1 | `9111a76` (2026-10-06) |
| gsd-core | `open-gsd/gsd-core` (npm `@opengsd/gsd-core`) | 1.16.0 | `e612933` (2026-10-13) |

How the analysis was done:

- Static reading of the source only. Nothing from the repositories was executed.
- Paths are relative to each repository's root at the commit above.
- For gsd-core, the analysis also read a real global install in `~/.claude` (read-only).

Every claim carries one of these tags:

| Tag | Meaning |
|---|---|
| **[code]** | Read in the source at the cited lines. |
| **[docs]** | Found only in docs, CHANGELOG, or comments. |
| **[prose]** | gsd-core only: an instruction in a workflow `.md` file that an LLM agent follows. No program enforces it. |
| **[inference]** | A conclusion drawn from verified code that was not observed at runtime. |

The central claims were re-checked a second time against the source (see [Re-checked claims](#re-checked-claims)).

## Summary

None of the three tools versions its project data the way ADR 0001 plans to, that is, with a format version in every file and a hard stop when the version is too new.

| Tool | How the CLI and the agent files stay in sync | How the CLI and the project data stay in sync |
|---|---|---|
| Spec Kit | Templates ship inside the CLI package. A SHA-256 manifest protects files the user has edited. | Spec Kit writes its version into four files and never reads it back. One integer schema version in `integration.json` gives the only forward-compatibility guard. |
| OpenSpec | Agent files are regenerated from templates on `openspec update` and compared by content. A version stamp is written into each `SKILL.md`. | Project data is barely versioned. Config is parsed leniently, one field at a time. |
| gsd-core | It installs into the agent's config directory with a per-file hash manifest and a framework of installer migrations. Update checks only produce warnings. | Project data has no version. Legacy config keys are rewritten whenever the config is read. |

| Aspect | Spec Kit | OpenSpec | gsd-core |
|---|---|---|---|
| Where agent files live | in the project (`.claude/skills`, `.github/agents`, …) | in the project (plus one tool in the home directory) | in the agent's config directory (global `~/.claude` or local `<repo>/.claude`) |
| Versions recorded | CLI version in 4 project files; `integration_state_schema: 1` | `generatedBy: <cli version>` in each `SKILL.md`; none in command files | `VERSION`, a manifest (`manifestVersion: 2` plus a version), install state (`schemaVersion: 1`) for each config directory |
| Is the recorded version ever read? | Only the schema integer | Yes, but compared with `!==` and only in `update` | Only for the update notice and the stale-hook check |
| CLI newer than the agent files | Not detected | Detected in `update` only | Shown as an advisory in the statusline or a banner |
| Agent files or data newer than the CLI | Hard stop only through `integration_state_schema` | Not detected; `update` rewrites the files with the older templates | Not detected |
| Skills check the CLI version at runtime | No | No | No (the identity check matches the package name only) |
| User edits to agent files | SHA-256 manifest; upgrade aborts unless `--force` | No protection; files are overwritten | Hash manifest, backup to `gsd-local-patches/`, three-way merge by the LLM plus a deterministic check |
| Atomic upgrade | No (only for external packages) | No | Partly (install state yes; manifest and `VERSION` no) |
| Per-project version pin | No (`requires.speckit_version` applies to extensions only) | No | No (`engines.gsd` applies to capabilities only) |
| Unknown fields in config/state | Dropped from `integration.json` and manifests | Kept in global config; ignored in `config.yaml` | Kept in `config.json`; dropped from install state |

## Spec Kit

### What gets installed and where

**Packaging.** `pyproject.toml:30-58` force-includes the templates, command templates, scripts, bundled extensions, presets, and workflows into `specify_cli/core_pack/` in the wheel. `init` and `integration upgrade` render the agent files locally from that copy. Per-agent release zips no longer exist: `release.yml` only publishes release notes, and the CHANGELOG records the change as #1803. So the CLI version and the kit version are the same thing. **[code]** (zip history **[docs]**)

**Agent files.** They belong to the tool and are tracked in `.specify/integrations/<key>.manifest.json`. **[code]**

- Claude: `.claude/skills/speckit-*/SKILL.md` (`integrations/claude/__init__.py:46-55`)
- Copilot: `.github/agents/*.agent.md` and `.github/prompts/*.prompt.md`
- Codex: `.agents/skills`
- Gemini: `.gemini/commands/*.toml`

**Shared files.** `.specify/scripts/` and `.specify/templates/` are tracked in `.specify/integrations/speckit.manifest.json` (`shared_infra.py:467-833`). **[code]**

**State files.** **[code]**

| File | Contents | Reference |
|---|---|---|
| `.specify/init-options.json` | `ai`, `integration`, `script`, `speckit_version`, … | `command_init.py:850-866` |
| `.specify/integration.json` | `version`, `integration_state_schema`, `installed_integrations`, `default_integration` | `integration_state.py:240-252` |
| `<key>.manifest.json` | `version`, `installed_at`, `files{path: sha256}`, `ownership_modes` (whole/partial/shared), `recovered_files` | `integrations/manifest.py:484-518` |

**User-owned files.** `.specify/memory/constitution.md` is created only if it is missing and is not tracked (`command_init.py:211-229`). **[code]**

**Git.** `.specify/.gitignore` excludes only the local parts (`feature.json`, `local-config.yml`, `integrations/packages/`). Everything else in `.specify/` is meant to be committed (`shared_infra.py:24-38`). **[code]**

### Version records

The CLI version is written in four places: `init-options.json` (`speckit_version`), `integration.json` (`version`), every `<key>.manifest.json`, and `speckit.manifest.json`. **[code]**

**None of these values is compared with the running CLI.** **[code]**

- `speckit_version` is only written (`command_init.py:857`). All other hits for the name are the getter `get_speckit_version()` and the `requires` checks for extensions.
- `manifest.version` is only read to be copied forward (`integrations/_lifecycle.py:502`).
- The `SKILL.md` frontmatter carries no version (`integrations/base.py:1901-1910`).

### Drift detection

**Integration state written by a newer Spec Kit.** This is the only guard. **[code]**

```python
# src/specify_cli/integration_state.py:63-69
schema = data.get("integration_state_schema")
if (isinstance(schema, int) and not isinstance(schema, bool)
        and schema > INTEGRATION_STATE_SCHEMA):
    return None, IntegrationReadError(kind="schema_too_new", schema=schema)
```

Integration commands then stop with "upgrade Spec Kit" (`integrations/_helpers.py:68-73`). **[code]**

**CLI newer than the project.** Not detected. `specify self upgrade` does not refresh project files and does not mention them either. **[code]**

**Recorded CLI version newer than the running CLI.** Not detected. `integration upgrade` re-renders the files with the older bundled templates without any notice. **[inference]**, from `command_upgrade.py:74-87,223-242`.

**Agent files the user changed.** Detected by hash. **[code]**

- `IntegrationManifest.check_modified()` compares SHA-256 hashes (`integrations/manifest.py:340-364`).
- Symlinks and unreadable files count as modified.

**Runtime checks.** No template or script calls `specify version`. The scripts read `integration.json` leniently and fall back to defaults (`scripts/bash/common.sh:252-280`). **[code]**

**CLI self-update.** Only on request: `specify self check` or `self upgrade` query the GitHub API (`_version.py:107-143`). **[code]**

### Upgrade

**`specify integration upgrade [key] [--force]`** (`integrations/command_upgrade.py:30-361`). **[code]**

1. Loads the old manifest. If any tracked file was modified and `--force` is not set, it exits with code 1 (`:80-87`).
2. Runs a first pass over the shared files.
3. Creates a new manifest stamped with the current CLI version and runs `setup()`, which overwrites the files.
4. Runs a second pass with `refresh_managed` for the default integration only.
5. Removes stale files, that is, files in the old manifest but not in the new one.

Rollback and limits:

- If the upgrade fails, it is not rolled back; the command only reports the failure (`:285-291`).
- Journaled rollback exists only when external packages are involved (`_lifecycle.py:468`).
- It handles one integration per call. There is no `--dry-run` and no check that the git tree is clean.

**`specify init --here [--force]`** is the documented fallback ("escape hatch" in `docs/upgrade.md` **[docs]**). **[code]**

- With `--force`, it overwrites every shared file with no hash check.
- Even without `--force`, it rewrites the agent files with a fresh manifest and never loads the old one.
- It rewrites `integration.json` with `installed_integrations=[selected]` (`command_init.py:766-780`). Other installed agents drop out of the state while their files stay on disk. **[inference]**, from this line.

**Config migration.** There are no versioned migration steps. Old shapes are normalized when read, for example the legacy `integration` key becomes `default_integration` and `installed_integrations` (`integration_state.py:166-194`). **[code]**

**Extension updates.** **[code]**

- They are transactional: backup and restore in `_command_update_transaction.py:74-626`.
- Compatibility is checked against `requires.speckit_version`.
- An update to a bundled extension is blocked with "upgrade spec-kit" when the catalog version is newer than the copy in the wheel.

### User edits

**[code]**

- Agent files are protected by the hash manifest. Uninstall only removes files whose hash still matches.
- Shared templates and scripts are refreshed only if their hash still matches. Otherwise the CLI prints "Preserved customized…" (`shared_infra.py:498-537`).
- Files that existed before installation and were not written by the tool are recorded as `recovered` and never overwritten automatically (`shared_infra.py:600-617`).
- Stale scripts are deleted only if their hash still matches (`shared_infra.py:770-830`).
- There are no `.new` files, no merge, and no lasting backup. A customized template stays frozen and never receives upstream fixes.

### Compatibility of config and state

**[code]**

- `init-options.json` keeps unknown fields: on write, the file is merged with `{**previous, …}`.
- `integration.json` and the manifests are written with a fixed set of keys (`integration_state.py:240-252`, `integrations/manifest.py:487-499`). **An older CLI therefore drops fields that a newer CLI added.**
- The extension and preset registries store a `schema_version` that is never checked. A corrupt registry is silently reset to empty (`extensions/__init__.py:763-793`), and these writes are not atomic.
- Bundle records accept the same major version and reject everything else as "written by a newer version or is corrupt" (`bundles/records.py:145-166`).

### Version constraints on components

**[code]**

- Extensions, presets, bundles, and external integration packages declare `requires.speckit_version` as a PEP 440 range. It is checked on install and update.
- External integration packages are also checked on every load (`integrations/installer.py:615-628,834`).
- For workflows, `requires` is advisory only (`workflows/engine.py:141-148`).
- Catalogs are fetched from `main`, not from the CLI's release tag (`extensions/__init__.py:4504-4506`).

### Multiple agents

**[code]**, consequences **[inference]**

- Each agent has its own manifest with its own recorded version, and upgrades run one agent per call. Agents can end up on different kit versions without anyone noticing.
- Shared templates are rendered with the command syntax of the **default** agent (`command_upgrade.py:195-218`).

### Git merges

**[inference]**

- The manifests, `integration.json`, and `init-options.json` are committed. Two branches with different kit versions therefore conflict in `version`, `installed_at`, and the hash maps.
- After a manual merge, hashes and file contents can disagree. Files then count as "modified", and the upgrade is blocked until `--force`, which also destroys real user edits.
- The recorded versions differ between branches, but nothing reads them, so nothing warns.

## OpenSpec

### What gets installed and where

**Skills.** `<tool.skillsDir>/skills/openspec-*/SKILL.md` (`src/core/shared/skill-paths.ts:23-38`). **[code]**

- An entire `openspec-*` folder counts as owned by the tool. When the user deselects a workflow, the whole folder is deleted recursively, including files the user added there (`src/core/update.ts:863-888`).
- One tool, `minimax-code`, writes into the home directory instead (`~/.minimax/skills`).

**Commands.** 35 adapters in `src/core/command-generation/adapters/`. For Claude, the output is `.claude/commands/opsx/<id>.md` with no version in the frontmatter (`adapters/claude.ts:20-34`). **[code]**

**Shared skills folders.** When several tools share one skills folder, `skills/.openspec-target` records which tool owns it (`src/core/shared-skill-target.ts:7-12`). **[code]**

**Project data.** **[code]**

- `openspec/{specs,changes,changes/archive}`.
- `openspec/config.yaml`: written only if it does not exist yet (`src/core/init.ts:1150-1176`). After that it belongs to the user. Later changes go through `yaml.parseDocument`, which keeps comments.
- Project-local workflow schemas in `openspec/schemas/<name>/schema.yaml`.

**User level.** **[code]**

- `$XDG_CONFIG_HOME/openspec/config.json` (`src/core/global-config.ts:56-77`).
- `$XDG_DATA_HOME/openspec/` for schema overrides, the store registry, and worksets.

**Legacy files.** The pre-1.0 marker blocks (`<!-- OPENSPEC:START/END -->`) in `AGENTS.md`, `CLAUDE.md`, and similar files are only detected and removed (`src/core/legacy-cleanup.ts`). **[code]**

### Version records

**[code]**

```ts
// src/core/shared/skill-generation.ts:181-191
metadata:
  author: ${template.metadata?.author || 'openspec'}
  version: "${template.metadata?.version || '1.0'}"
  generatedBy: "${generatedByVersion}"
```

- `generatedBy` holds the CLI version and is written into each `SKILL.md`.
- `metadata.version` is hard-coded to `"1.0"` in all templates, so it tells nothing.
- `compatibility:` is free text (`'Requires openspec CLI.'`).
- Command files, `config.yaml`, and the global `config.json` carry no version.
- Every `schema.yaml` must have an integer `version` (`src/core/artifact-graph/types.ts:49-64`), but nothing ever reads it: `getVersion()` has no callers.
- The store and workset files use `version: z.literal(1)` with `.strict()` (`src/core/store/foundation.ts:151-163`).
- Of the CLI's JSON outputs, only `version --json` (`schemaVersion: 1`) and `validate --json` (`version: '1.0'`) carry a version.

### Drift detection

**Only in `openspec update`.** **[code]**

- `getToolVersionStatus` has exactly one caller, `src/core/update.ts:235`.
- `openspec config profile` additionally warns when the profile has drifted (`src/commands/config.ts:326-342`).
- No other command checks anything, and the skills never check the CLI version. The `onboard` skill checks only that the CLI is installed.

**Three signals are combined** (`src/core/update.ts:231-268`). **[code]**

1. **Version stamp.** `generatedBy` is read from the first `SKILL.md` found (`tool-detection.ts:336-347`). The comparison is plain inequality:

   ```ts
   // src/core/shared/tool-detection.ts:399-403
   const needsUpdate =
     configured &&
     (generatedByVersion === null ||
       generatedByVersion !== currentVersion ||
       commandsDrifted);
   ```

2. **Profile or delivery drift.** Files are missing for selected workflows, or files exist for deselected ones (`src/core/profile-sync-drift.ts:65-176`).
3. **Content drift.** Each `SKILL.md` and command file is regenerated in memory and compared byte by byte, after line endings (CRLF) and BOM are normalized (`update.ts:608-642`, `tool-detection.ts:159-226`).

Consequences:

- The comparison has no direction. `compareVersions` exists (`src/core/version-check.ts:127-149`) but is not used here. **[code]**
- An older CLI therefore overwrites skills generated by a newer CLI with its own older templates, without warning. **[inference]**

**CLI self-update.** **[code]**

- Runs in `update` and `version --check`.
- Asks the npm registry for `latest` with a 1.5-second timeout.
- For a global npm install it can upgrade itself, check the new binary's `--version`, and then re-run `update` with the new binary (`src/cli/index.ts:332-373`, `version-check.ts:835-927`). The code comment gives the reason: the running process writes files from its own templates, so upgrading afterwards would leave the old ones in place.
- Skipped in CI, with `DO_NOT_TRACK`, or with `OPENSPEC_NO_UPDATE_CHECK`.

### Upgrade

**[code]**

- `update` regenerates the files from the built-in templates and overwrites them.
- Writes go through a plain `fs.writeFile` and are not atomic (`src/utils/file-system.ts:312-316`). A helper for atomic writes exists (`file-state.ts:110-130`) but is only used for store and workset state.
- There is no backup, no dry-run, and no transaction: tools are processed one after another, failures are collected, and the command throws at the end.
- Tool folders that were renamed upstream are migrated, for example `.windsurf` → `.devin` (with the user's consent). Files whose content differs materially stay where they are (`src/core/migration.ts:44-108`).
- There is no migration of the specs or changes formats. The v0→v1 upgrade consists of legacy cleanup plus a hint to move `project.md` into `config.yaml` by hand.

### User edits

**[code]**

- Skill and command files have no protection. An edit is only reported as the reason for the overwrite (`update.ts:670-674`).
- Copilot cloud files are the exception: they are rewritten only if their content exactly matches a known generated version (`src/core/github-copilot/cloud-agent.ts:304-345`).
- Legacy files are deleted only if they still carry the OpenSpec marker. The marker is checked again with `O_NOFOLLOW` (no symlinks followed) just before deleting (`legacy-cleanup.ts:459-479`).
- `config.yaml` is never overwritten.
- If the global `config.json` cannot be parsed, the CLI uses the defaults and refuses to write the file (`global-config.ts:141-250`).

### Compatibility of config and schemas

**[code]**

- `config.yaml` is validated one field at a time with zod `safeParse`. An invalid field produces a warning and is dropped. Unknown keys at the top level are ignored without a warning (`src/core/project-config.ts:317-453`).
- The global `config.json` is loaded as `{...DEFAULT_CONFIG, ...parsed}` and validated with zod `.passthrough()`, so unknown keys survive a save.
- Workflow schemas are resolved in this order: project, then user, then built-in. The first match wins (`src/core/artifact-graph/resolver.ts:120-172`). A project-local fork replaces the built-in schema with no warning and never picks up upstream changes.

### Multiple agents

**[code]**

- No list of configured tools is stored. Each run rediscovers them from the files on disk (`tool-detection.ts:417-450`).
- The profile and delivery mode come from the user's **global** config, not from the project (`update.ts:178-185`). Two developers with different profiles therefore produce different file sets in the same repository. **[inference]**

### Git merges

**[inference]**

- Each `SKILL.md` contains a `generatedBy:` line. Two branches with different CLI versions therefore conflict in every skill.
- The "from" version that `update` reports comes from the first file found, so in a mixed tree it is arbitrary.
- The content comparison still catches every file that differs. Whoever runs `update` next decides the result with their CLI, which can mean a downgrade.
- The project pins no CLI version, so CI cannot enforce one.

## gsd-core

### What gets installed and where

gsd-core installs into the agent's **config directory**, either the global one (`~/.claude`) or a local one in `<repo>/<runtimeDir>`. The scope is `isGlobal ? 'global' : 'local'` (`bin/install.js:10837`). **[code]**

**[code]**

| Artifact | Path | Owner |
|---|---|---|
| Engine, workflows, templates, `bin/gsd-tools.cjs` | `gsd-core/**` | tool |
| Kit version | `gsd-core/VERSION` | tool |
| Skills, commands, agents, hooks, scripts | `skills/gsd-*`, `commands/gsd-*`, `agents/gsd-*`, `hooks/` | tool |
| Hook registrations | `settings.json`, `hooks.json`, `config.toml` (depending on the agent) | shared |
| File manifest | `gsd-file-manifest.json` | tool |
| Install state | `gsd-install-state.json` | tool |
| Migration journal | `gsd-migration-journal/<runId>.json` plus `-rollback/` and `-backups/` | tool (the backups hold user bytes) |
| Local patches | `gsd-local-patches/**` plus `backup-meta.json` | user data held by the tool |
| Pristine baselines | `gsd-pristine/**` | tool |
| User-owned | `gsd-core/USER-PROFILE.md` (`src/install-engine.cts:250`) | user |

Outside the config directories:

- `~/.cache/gsd/…` holds the update cache, shared by all agents and scopes.
- `~/.gsd/defaults.json`.
- Project data: `.planning/config.json`, `STATE.md`, `ROADMAP.md`.

gsd-core supports about 15 agents (Claude, Codex, Gemini, Copilot, OpenCode, Cursor, …) (`src/installer-migrations/000-first-time-baseline.cts:18-34`). **[code]**

### Version records

**[code]**

| Record | Contents | Reference |
|---|---|---|
| `gsd-core/VERSION` | `pkg.version` | `bin/install.js:12091` |
| `gsd-file-manifest.json` | `manifestVersion: 2` (the manifest's own schema version, separate from the package version), plus `version`, `mode`, `runtime`, `scope`, and `files{rel: sha256}` | `bin/install.js:10086-10105, 10291` |
| `gsd-install-state.json` | `schemaVersion: 1`, `appliedMigrations[{id, appliedAt, journal, checksum}]` | `src/installer-migrations.cts:343-390` |
| Hook header | `// gsd-hook-version: {{GSD_VERSION}}` | `hooks/gsd-check-update.js:2` |
| `backup-meta.json` | `from_version`, `pristine_hashes`, … | `bin/install.js:10555-10565` |
| `.planning/config.json` | **no version** | `src/config.cts:327-400` |
| `STATE.md` | `gsd_state_version: '1.0'`, a constant that nothing compares | `src/state.cts:3342`, `src/state-transition.cts:2521` |

Readers (`src/installer-migrations.cts`):

- Manifest reader (`:284-335`): lenient. A missing `manifestVersion` is read as 1, and a newer value is reported as it is.
- Install-state reader (`:357-371`): strict. Corrupt JSON is a hard error, and any numeric `schemaVersion` is accepted with no upper bound.

The real install on this machine matches: `manifestVersion: 2`, `version: "1.16.0"`, 951 hashed files, and only the baseline migration recorded in the install state.

### Drift detection

**Installed version older than npm `latest`.** Advisory only. **[code]**

- A SessionStart hook starts a worker in the background (`hooks/gsd-check-update.js`).
- The worker runs `npm view @opengsd/gsd-core version` and writes `update_available: latest && isSemverNewer(latest, installed)` atomically to the cache (`hooks/gsd-check-update-worker.js:158, 177-180`).
- The statusline and the banner show "GSD update available: X → Y" (`hooks/gsd-statusline.js:1065-1082`, `hooks/gsd-update-banner.js:63-79`). Nothing blocks.
- Stale hooks are detected when the installed version is higher than the version in a hook's header (`gsd-check-update-worker.js:117-145`).

**Skills and tool versions.** Not checked by version. **[code]**

- The launcher block in each workflow (`gsd-core/workflows/quick.md:42`) looks for `gsd-tools.cjs` in this order: project-local first, then about 20 global home directories, then `PATH`.
- The identity check only tests whether `runtime-identity --raw` starts with `{"packageName":"@opengsd/gsd-core"`. The version, which the same payload contains (`src/runtime-identity.cts:262-266`), is ignored.
- `cli-skew-check` (`src/cli-skew-check.cts:22-41`) is a path heuristic that warns on stderr when a project-local tool exists but a different one is running.
- Workflows never read `VERSION`; only `update.md` does.

**Project data written by a newer or older GSD.** Not detected. **[code]**

- Nothing compares `gsd_state_version`, and `config.json` has no version.
- Unknown keys at the top level of the config produce a warning once (`src/config-loader.cts:873-882`).

### Upgrade

**`/gsd-update`** is a workflow carried out by the LLM. **[prose]**, `gsd-core/workflows/update.md`

1. Resolves the target with `gsd-tools update-context --json` (local before global).
2. Fetches the latest version and compares. If they are equal, or the installed version is newer ("dev install"), it stops.
3. Shows the changelog and asks the user to confirm.
4. Backs up files the user added that the manifest does not list (`detect-custom-files`).
5. Runs `npx … gsd-core --<rt> --local|--global` for exactly one agent and one scope.
6. Restores the user's files and points to `--reapply` if local patches exist.

**Installer order.** `bin/install.js`, `install()` at line 10763. **[code]**

1. `saveLocalPatches`: files whose hash differs from the old manifest are copied to `gsd-local-patches/`.
2. `runInstallerMigrations` (with `baselineScan`). Open prompts stop the run with a hard error.
3. For each managed directory: `rmSync(destDir, {recursive: true})`, then a fresh copy (`:7968-7971`).
4. Writes `VERSION` (`:12091`), then the manifest (`:12470`). Neither write is atomic.

**Migration framework.** `src/installer-migrations.cts`. **[code]**

- **Order:** compiled migration files sorted by filename and filtered by agent and scope (`:530-555`). `introducedIn` plays no part in the ordering.
- **Checksum:** SHA-256 over the migration's metadata and `plan.toString()`. If the checksum of an applied migration changes, the stored value is silently overwritten and the migration does not run again (`:466-528`).
- **Classification by manifest hash:** missing, managed-missing, unknown, managed-pristine, or managed-modified (`:431-446`). A removal of a modified file becomes `backup-and-remove`, and a removal of an unknown file becomes `preserve-user` (`:872-878`).
- **Apply:** writes a journal, takes rollback snapshots and backups (symlinks preserved), and restores in reverse order on failure (`:1004-1178`). Rollback works only inside the running process.
- **State:** written atomically via a temporary file and rename (`:374-390`).
- **Lock:** `gsd-install-migration.lock` with `wx`, reclaimed from dead PIDs, 30-second timeout (`:609-700`).
- **Bookkeeping gap:** migrations without actions are recorded as applied only if no migration in the run produced actions.

**Project config migration.** **[code]**

- `normalizeLegacyKeys` (`src/configuration.cts:265-310`) hard-codes five key renames, for example `depth` → `granularity`.
- It runs on **every read** and writes the result back unless `persist: false` (`src/config-loader.cts:691, 771, 838`).
- `gsd-tools migrate-config` also exists as an explicit command.
- There is no numbered chain and no schema version.

### User edits

- **Local patches:** `saveLocalPatches` (`bin/install.js:10471-10545`) backs up modified files and stores the pristine hashes. **[code]**
- **Reapply:** a three-way merge done by the LLM (pristine baseline, the user's backup, the new file). **[prose]**, `workflows/reapply-patches.md`
- **Verification:** `gsd-core/bin/verify-reapply-patches.cjs` is deterministic. `--classify` runs before the merge. After the merge it exits with code 1 if any line the user added is missing. **[code]**
- **Files the user added** to managed directories are removed by a direct re-install (because of the `rm -rf`). Only the `/gsd-update` workflow backs them up. **[inference]**

### Compatibility of config and state

**[code]**

- Unknown keys in `config.json` survive on disk: on write-back the whole object is serialized. In memory they are ignored. `config-set` rejects unknown keys.
- Defaults are filled in at read time. `new-project` writes a **full snapshot of the defaults** (`src/config.cts:327-400`), so later default changes never reach those keys. **[inference]**
- `writeInstallState` always writes `schemaVersion: 1` and keeps only `appliedMigrations` and `hostGeneration` (`src/installer-migrations.cts:1139-1143`). **An older installer drops newer fields.**

### Git merges

**[inference]**

- `.planning/` has no version stamp, so there is nothing that could conflict or be checked.
- Rewrites triggered on read (legacy keys) produce conflicting edits to `config.json` when another branch uses an older GSD.
- If the local install (`.claude/**`, manifest, install state) is committed, merging different kit versions leads to false "local patches" and an `appliedMigrations` list that does not match the disk.

## Comparison by drift axis

### CLI ↔ agent files

| | Spec Kit | OpenSpec | gsd-core |
|---|---|---|---|
| Source of truth | templates in the wheel | templates in the npm package | files in the npm package |
| How drift is detected | hash only (did the user change it?), never the version | version stamp `!==` plus content comparison | npm `latest` compared with `VERSION` (advisory) |
| When | `integration upgrade` / `status` | `update` | each session (SessionStart hook) |
| Direction-aware | no | no (`!==`) | yes (`isSemverNewer`), but advisory only |
| Version in each agent file | no | `SKILL.md` only | hook header only |

**Finding:** in all three tools, skills and commands depend on CLI flags and JSON output that are not versioned. They stay compatible only because both sides come out of the same release. None of the skills checks the CLI version at runtime.

### CLI ↔ project data

| | Spec Kit | OpenSpec | gsd-core |
|---|---|---|---|
| Format version in project data | one integer in `integration.json` | none (project) | none |
| Guard against data newer than the CLI | yes, for `integration.json` only | strict `literal(1)` on store state only | no |
| Migration of project data | normalized on read | none (legacy cleanup only) | hard-coded key renames on every read, written back |
| Migration written only on explicit command | yes (no migrations) | — | **no** |

**Finding:** none of the three migrates project data with a chain of format versions. All three rely on additive changes and lenient reading. In each tool, the central integer version, where it exists, is the only thing that protects against a newer format.

### User edits

| Mechanism | Spec Kit | OpenSpec | gsd-core |
|---|---|---|---|
| Hash at install time | yes, per file, with ownership modes | no | yes, per file |
| Behavior on update | abort without `--force`; templates frozen | overwrite | backup plus LLM merge plus deterministic check |
| Files the tool did not write | `recovered` flag, never overwritten | known-content allowlist (Copilot only) | `preserve-user` in migrations |
| `.new` / diff offer | no | no | no (merge instead) |

## Implications for ADR 0001

### Where the ADR goes further than all three

1. **A format version in every file.** None of the three does this. Two of them use a central version (Spec Kit's `integration_state_schema`, gsd-core's install-state `schemaVersion`), and both have the merge problem the ADR describes. gsd-core has no version for project data at all. No tool uses a version file per directory either; the idea assessed earlier has no precedent.
2. **A hard stop when data is newer than the CLI.** Spec Kit has it for one file. OpenSpec and gsd-core lack it for project data and agent files. OpenSpec even downgrades silently because of the `!==` comparison.
3. **Write only during `upgrade`.** gsd-core does the opposite: it rewrites the config on read, which causes noise in the working tree and merge conflicts. This confirms the ADR's decision.
4. **Skills check `shade-kit version`.** None of the three does this. gsd-core comes closest (an identity check without a version).
5. **Unknown fields are preserved.** Spec Kit (`integration.json`, manifests) and gsd-core (install state) drop them with an older version. This confirms ADR decision 4 and makes it worth a test.

### Worth adopting

| Pattern | Source | Where it fits in ShadeKit |
|---|---|---|
| A hash manifest with ownership modes (whole/partial/shared) and a `recovered` flag for files the tool did not write | Spec Kit | `templates.lock.json`: a `recovered` state for templates that existed before `init` |
| Hash-gated deletion of files that are no longer shipped | Spec Kit, gsd-core | `upgrade`: delete only templates the user has not changed |
| Lenient reading of older and newer **manifest** formats with a separate `manifestVersion` | gsd-core | `templates.lock.json` gets its own `schemaVersion` |
| Upgrade the CLI first, then regenerate with the **new** binary | OpenSpec | relevant if `shade-kit upgrade` can also update the engine |
| Refuse to save a config file that cannot be parsed | OpenSpec | `config.json`: do not overwrite with defaults when parsing fails |
| A migration lock with stale-PID reclaim, and atomic state writes | gsd-core | `upgrade`: complements the ADR's temp-file-plus-rename plan |
| A deterministic check after a merge instead of trusting the agent | gsd-core | if a skill helps the user adopt template diffs, verify the result in the CLI |
| A version-range gate for extensions (`requires.speckit_version`, `engines.gsd`) | Spec Kit, gsd-core | only when ShadeKit gets extensions (YAGNI for v1) |
| A CI check that forces a version bump when shipped content changes | Spec Kit (`extension-version-guard.yml`) | CI: a template change without a template version bump fails |

### Worth avoiding

| Anti-pattern | Source | Consequence |
|---|---|---|
| Recorded versions that are never read | Spec Kit | false sense of safety, no drift warning |
| Comparing versions with `!=` instead of ordering them | OpenSpec | silent downgrade by an older CLI |
| Fixed key sets on write | Spec Kit, gsd-core | an older CLI destroys newer fields |
| Config migration on read, with write-back | gsd-core | dirty working trees, merge conflicts |
| Writing a full snapshot of the defaults into the project config | gsd-core | new defaults never take effect; relevant to the "sparse config" note in ADR 0001 |
| Upgrades that are not atomic | all three | mixed states after a crash |
| `--force` as the only way past a hash conflict | Spec Kit | real user edits get destroyed; the ADR's `.new` approach avoids this |
| A version stamp in every generated file | OpenSpec | merge conflicts in every file on every version change. A content hash in the lock file is enough. |

### Open question for the ADR

**Should the project pin an engine version?** None of the three tools pins an engine version per project (V4 in the ADR); each relies on an advisory check or none at all. The ADR is consistent with that in deferring V4. One gap remains in all three tools and in the ADR: CI has no way to enforce a version. A `shade-kit doctor --check` mode with an exit code would close it cheaply.

## Re-checked claims

These claims were checked a second time directly against the source:

- Spec Kit:
  - `integration_state.py:63-69`: the schema-too-new guard.
  - `integration_state.py:240-252`: a fixed key set on write.
  - `integrations/manifest.py:340-364`: `check_modified` by hash.
  - `integrations/command_upgrade.py:80-87`: the upgrade aborts without `--force`.
  - `pyproject.toml:30-58`: the core pack ships in the wheel.
  - `command_init.py:857`: `speckit_version` is only written. A grep finds no comparison.
- OpenSpec:
  - `tool-detection.ts:399-403`: the `!==` comparison.
  - `skill-generation.ts:181-191`: the frontmatter with `generatedBy`.
  - `getToolVersionStatus`: the only caller is `update.ts:235`.
  - `file-system.ts:312-316`: a plain, non-atomic `fs.writeFile`.
- gsd-core:
  - `installer-migrations.cts:1138-1143`: always `schemaVersion: 1`.
  - `installer-migrations.cts:530-538`: migrations are filtered by agent and scope.
  - `gsd-check-update-worker.js:158`: `isSemverNewer`.
  - `state.cts:3342`: the constant `gsd_state_version`.
  - `quick.md:42`: the identity check matches the package name only.
  - `install.js:12091` and `10291`: `VERSION` and the manifest are written with plain `writeFileSync`.
