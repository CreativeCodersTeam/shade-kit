# Versioning tooling for a two-component repository (.NET CLI + Markdown skills)

Research date: 2026-11-21. Repository facts in this file were read from the shade-kit working tree and are labelled **FACT (repo)**. Web facts carry their source URL. Anything not verified is labelled as inference or listed under Gaps.

Baseline in the repository:
- **FACT (repo):** `Directory.Packages.props` references `GitVersion.MsBuild` `6.8.2` as a `GlobalPackageReference`; `GitVersion.yml` uses the v6 flat format with `mode: ContinuousDelivery`, `tag-prefix: 'v'`, `next-version: v0.9.0`, `main` with `label: ''`, `increment: Patch`, and `feature` with `label: '{BranchName}'`, `increment: Inherit`.
- **FACT (repo):** 19 entries under `skills/` and 26 occurrences of the literal `0.9.0` under `skills/` (`grep -rn 0.9.0 skills | wc -l`).
- **FACT (repo):** `SkillKitVersionTests` parses the frontmatter line with `^  version:(?:[ \t]+(?<value>.*?))?[ \t]*$` and accepts it only if `SemanticVersion.TryParse(value)` succeeds, the version is not a prerelease, and `version.ToString() == value`. A trailing YAML comment on that line (for example a release-please marker) would therefore be part of `value` and fail the guard. It also scans `--expect <v>` and the "is the kit version of this skill" sentence.

## GitVersion: multiple components, tag prefixes, path filters, modes, feature-branch builds, pain points

### Takeaway
GitVersion can serve two components only by running it twice with different configs (different `tag-prefix` plus `ignore.paths` regexes); it computes a version but never writes it into SKILL.md, so it cannot by itself remove the hand edits of the skills. GitVersion 7.0 (released Nov 2026) moves the config into nested `calculation:`/`output:` sections, so the repo's v6 flat `GitVersion.yml` needs a migration on upgrade.

### Cited Findings
- Latest releases: 7.0.1 (20 Nov, marked "Latest"), 7.0.0 (17 Nov), 7.0.0-beta.1/.2 (16/17 Nov), latest 6.x is 6.8.2 (10 Jul). The release page shows no year. 7.0.1 fixes issue #5501 "TrunkBased workflow + ignore.paths loses feature increment when an ignored merge is the merge base"; 7.0.0 fixed #5245 "Version calculation performs one revision walk per version tag (O(tags x commits))" — [GitVersion releases](https://github.com/GitTools/GitVersion/releases)
- The gitversion.net configuration and modes pages now redirect ("Continue to 7.0") to `/7.0/docs/...` — [gitversion.net configuration](https://gitversion.net/docs/reference/configuration)
- v7 breaking changes stated on the config page: calculation settings move under `calculation`, output settings under `output`; `workflow` stays at the root; the v6 flat layout works in 7.0 only with `GITVERSION_CONFIGURATION_VERSION=v6` and produces a warning; `--override-config` uses nested keys and rejects flat v6 keys — [GitVersion 7.0 configuration](https://gitversion.net/7.0/docs/reference/configuration)
- `tag-prefix` is "a regular expression which is used to trim Git tags before processing"; default `[vV]?`; the page says nothing about per-component prefixes such as `app-v` — [GitVersion 7.0 configuration](https://gitversion.net/7.0/docs/reference/configuration)
- `next-version` "is not a permanent replacement for `git tag` and should only be used intermittently"; if behaviour is odd, remove it and tag an appropriate historical commit instead. The 7.0 page does not state that `next-version` is ignored once a tag at or above it exists — [GitVersion 7.0 configuration](https://gitversion.net/7.0/docs/reference/configuration)
- `ignore` has `sha`, `commits-before`, `paths`, `tags`, `branches`. `paths` is a list of regexes; a commit is ignored only if all changed paths match; matching is case-insensitive unless `(?-i)`. Monorepo examples verbatim: `calculation: ignore: paths: - '^ProjectB/.*'` and `- '^(?!ProjectA/|LibraryC/).*'` ("filter only those commits that belong to a specific project in a monorepo"). Caveat: ignored commits are excluded only when searching for a version source; other parts such as build metadata still use them — [GitVersion 7.0 configuration](https://gitversion.net/7.0/docs/reference/configuration)
- Config file selection: the CLI supports `--config` and `--override-config`; the page does not mention GitVersion.MsBuild per-project config options — [GitVersion 7.0 configuration](https://gitversion.net/7.0/docs/reference/configuration)
- Modes (one-line summaries only): ManualDeployment "keeps the calculated pre-release version and adds build metadata; intermediate commits can share a semantic version"; ContinuousDelivery "requires a numbered pre-release version and incorporates the commit count into its pre-release number"; ContinuousDeployment "removes the pre-release label from the calculated version"; "Tags and increment rules determine when the released version changes" — [GitVersion 7.0 modes](https://gitversion.net/7.0/docs/reference/modes/)
- Built-in defaults: GitFlow/v1 (default when `workflow` is unset) and GitHubFlow/v1 use `ContinuousDelivery` globally and `ManualDeployment` for `release`, `feature`, `unknown`; TrunkBased/preview1 (experimental) uses `ContinuousDeployment` on `main` — [GitVersion 7.0 configuration](https://gitversion.net/7.0/docs/reference/configuration)
- Feature-branch label defaults to `{BranchName}` from the branch regex; `{Sha}`, `{ShortSha}`, `{env:VARIABLE_NAME}` with `??` fallbacks are available — [GitVersion 7.0 configuration](https://gitversion.net/7.0/docs/reference/configuration)
- `commit-message-incrementing` defaults to `Enabled`; patterns `+semver: breaking|major`, `feature|minor`, `fix|patch`, `none|skip`; `=semver:` resets earlier increments — [GitVersion 7.0 configuration](https://gitversion.net/7.0/docs/reference/configuration)
- GitVersion 6.0.0 renamed `tag` to `label` and removed the init wizard (breaking changes listed in BREAKING_CHANGES.md) — [newreleases: GitVersion 6.0.0](https://newreleases.io/project/github/GitTools/GitVersion/release/6.0.0)

### Inferences
- Independent versions with GitVersion = two invocations: CLI with `tag-prefix: 'cli-v'` and `ignore.paths: ['^skills/.*']`; skills with `tag-prefix: 'skills-v'` and `ignore.paths: ['^(?!skills/).*']`. GitVersion.MsBuild would compute only the CLI version; the skills version would come from the GitVersion CLI (Cake via `GitVersion.Tool`) with `--config`/`--override-config`.
- GitVersion has no file-writing step for Markdown; the skills literal still has to be written by something else and committed before the tag. So GitVersion alone does not remove the manual SKILL.md edits; it can only remove `next-version` if minors are raised via `+semver: minor` commits or tags.
- Under `ContinuousDelivery` on a feature branch (current repo config), a local build yields `<next>-<branch>.<n>`; that prerelease engine is what makes local skill testing awkward (the guard compares major.minor, `version --expect` only warns for a prerelease engine per the repo's CLAUDE.md).
- Upgrading GitVersion.MsBuild 6.8.2 -> 7.x requires migrating the flat `GitVersion.yml` to nested `calculation:` or setting `GITVERSION_CONFIGURATION_VERSION=v6` temporarily.

### Gaps
- Whether a `tag-prefix` of `cli-v` makes GitVersion skip tags of the other component (`skills-v0.4.0`) cleanly or tries to parse them — not documented on the pages fetched.
- Exact example version strings per mode on a feature branch (the per-mode subpages were not fetched).
- Year of the 7.0.0 release is inferred (Nov 2026, after 6.8.2 of 10 Jul); the release page shows no year.
- Documented statement that `next-version` is ignored once a tag >= exists was not found in the 7.0 docs (the repo's own `GitVersion.yml` comment says "not yet observed").

## Nerdbank.GitVersioning (NBGV): version.json per directory, pathFilters, height, set-version, prepare-release, cloud builds, local builds, non-.NET folders

### Takeaway
NBGV is the most monorepo-capable of the git-height tools: one `version.json` per directory with `pathFilters` gives each component its own version height. Unlike GitVersion, the major.minor lives in a committed file (`version.json`), and `nbgv set-version` / `prepare-release` edit it — but it still does not rewrite SKILL.md text.

### Cited Findings
- Current NuGet package: Nerdbank.GitVersioning 3.10.94 — [NuGet](https://www.nuget.org/packages/nerdbank.gitversioning)
- `pathFilters` default is `[":/"]` (whole repo counts toward height). A commit affects a project only if it changes at least one matched path; Foo/Bar/Quux example with `"pathFilters": ["."]` per project: a change to `/README.md` affects no project. Syntax: relative to the `version.json` directory, absolute with `:/`, exclusions with `:!` or `:^`, wildcards `*`, `?`, `**`; matching follows `core.ignorecase` — [NBGV path filters](https://dotnet.github.io/Nerdbank.GitVersioning/docs/path-filters.html)
- `nbgv path-filters check` (non-zero exit on drift, suited to CI) and `update` derive filters from the MSBuild project graph (`.csproj`/`.vbproj`, `--ext` for others); a `version.json` without associated projects is skipped — [NBGV path filters](https://dotnet.github.io/Nerdbank.GitVersioning/docs/path-filters.html)
- `version.json` can live in the project directory or any ancestor; `"inherit": true` lets a subdirectory file override settings, e.g. `"prerelease": "beta"` turns inherited `1.2` into `1.2-beta`. Fields: `version`, `pathFilters`, `publicReleaseRefSpec`, `cloudBuild.setVersionVariables`, `cloudBuild.buildNumber`, `release.branchName` (`v{version}`), `release.versionIncrement`, `release.firstUnstableTag`, `versionHeightOffset` (+`versionHeightOffsetAppliesTo`), `nugetPackageVersion.semVer` (default 1), `gitCommitIdShortFixedLength` — [NBGV version.json](https://dotnet.github.io/Nerdbank.GitVersioning/docs/versionJson.html)
- Builds from refs matching `publicReleaseRefSpec` drop the `-g<sha>` commit-id suffix; other refs keep it. Changes to `version.json` take effect once committed (push not required); `gitCommitIdIncludeDirty` can append `-dirty` — [NBGV version.json](https://dotnet.github.io/Nerdbank.GitVersioning/docs/versionJson.html)
- `nbgv prepare-release` creates a release branch (`release/v1.2` from `1.2-beta`), removes the prerelease tag there (or sets a new one, e.g. `nbgv prepare-release rc`), increments main's version (default: minor, prerelease `alpha`), merges back resolving the `version.json` conflict (`--no-merge` to skip); `--versionIncrement Major`. `nbgv tag` creates a tag per `release.tagName` (default `v{version}`). `nbgv cloud` sets cloud build numbers/variables. There is a Rust CLI (Windows/Linux/macOS) with `get-version`, `set-version`, `tag`, `get-commits`, `cloud`, `prepare-release`, but without `install`/`path-filters` and without MSBuild integration — [NBGV nbgv CLI](https://dotnet.github.io/Nerdbank.GitVersioning/docs/nbgv-cli.html)
- A 3.x regression (v3.4.194) made version height always zero with path filters, linked to path-filter code not converted for the managed git implementation — [NBGV issue 587 (via dependabot redirect)](https://github-redirect.dependabot.com/dotnet/Nerdbank.GitVersioning/issues/587)

### Inferences
- A `skills/version.json` with `"pathFilters": ["."]` and a `src/version.json` (or root with exclusion `":!/skills"`) would give independent heights. Since `path-filters update` skips a `version.json` without projects, the skills filters would be hand-written (they are static, so low cost).
- NBGV's version is "major.minor in a file + git height for patch". That fits the skills' rule (literal major.minor in git) better than GitVersion's tag-derived model: the release action for a minor raise is a committed edit of `version.json`, not a tag. But the patch is height-derived, so `metadata.version` (x.y.z) in SKILL.md would still diverge from the computed patch unless the skills only carry major.minor or the patch is fixed at `.0`.
- NBGV is tag-optional (versions come from `version.json` + height), which differs from the repo's current "release = tag `v<SemVer>` on main" rule; `release.tagName`/`nbgv tag` can keep tags, but the CI tag check would need to match.

### Gaps
- Whether `nbgv get-version` accepts a path argument for a non-.NET folder (documented flag not confirmed on the fetched page).
- Current exact version-string format for non-public builds with `nugetPackageVersion.semVer: 2` (e.g. `1.2.3-g<sha>` vs `1.2.3-beta.g<sha>`) — the public/stable doc page returned 404.

## MinVer: MinVerTagPrefix for multiple components, local build behaviour

### Takeaway
MinVer is the simplest tag-only tool and supports multiple components via `MinVerTagPrefix`, but has no path filtering: height counts all commits unless `MinVerIgnoreHeight` is set. Like GitVersion it computes but never writes files.

### Cited Findings
- Algorithm: a tagged commit uses the tag; otherwise latest tag in history; an RTM tag `1.0.0` yields `1.0.1-alpha.0` + height; no tag -> `0.0.0-alpha.0` + height; example `1.0.0-beta.1` at height 42 -> `1.0.0-beta.1.42` — [MinVer README](https://raw.githubusercontent.com/adamralph/minver/main/README.md)
- Multiple components: give each project its own `MinVerTagPrefix` (e.g. `main-`, `ext-`); every commit affects height so changes in one project change the other's version; `MinVerIgnoreHeight` prevents this — [MinVer README](https://raw.githubusercontent.com/adamralph/minver/main/README.md)
- `MinVerMinimumMajorMinor` sets the MAJOR.MINOR floor for interim builds before the first tag; `MinVerDefaultPreReleaseIdentifiers` (default `alpha.0`, e.g. `preview.0`); `MinVerBuildMetadata` (by default only in the informational version); `MinVerSkip` (example: disable for Debug); `minver-cli` prints the version for non-SDK outputs; `MINVERVERSIONOVERRIDE` env var to share a version — [MinVer README](https://raw.githubusercontent.com/adamralph/minver/main/README.md)
- Shallow clones in CI can hide tags and fall back to the default version; set sufficient fetch depth — [MinVer README](https://raw.githubusercontent.com/adamralph/minver/main/README.md)

### Inferences
- `MinVerMinimumMajorMinor` is the MinVer analogue of GitVersion's `next-version`; it would still be a hand edit per minor raise.
- For the skills component, `minver-cli --tag-prefix skills-v` could compute a version, but MinVer has no notion of "only commits under skills/", so the skills prerelease height would move on CLI-only commits.

### Gaps
- Current MinVer version number (README shows only badges; not fetched from NuGet).

## release-please: manifest mode, component tags, extra-files/generic updater in Markdown, .NET support, Conventional Commits, release PRs

### Takeaway
release-please is the only evaluated tool that both (a) keeps independent versions and tags per path in one repo and (b) writes the new version into arbitrary text files and commits it via a release PR before the tag — which is exactly what the "files must already contain the right text in git" constraint requires. It works on Markdown via the generic updater's markers, but the markers must sit on the version lines, which clashes with the repo's current frontmatter guard regex and adds marker noise to SKILL.md text.

### Cited Findings
- Manifest mode: `release-please-config.json` (settings) + `.release-please-manifest.json` (current version per package; must exist at the tip of the target branch). Each key under `packages` is a repo-relative path; non-node types need a `package-name`. Default tag `<component-name>-v<release-version>`; `include-component-in-tag: false` -> `v<release-version>`. `separate-pull-requests` default `false` (one combined Release PR). Commits are assigned to packages by path; `exclude-paths`. `bootstrap-sha`, `last-release-sha`, `release-as` (forces a version, "ignoring conventional commits"; reset to `""`). Prerelease: `prerelease-type`, `prerelease: true`, "prerelease" versioning strategy. `bump-minor-pre-major`, `bump-patch-for-minor-pre-major`. `linked-versions` plugin moves all group members to the highest version when one is released. A root package `"."` triggers on any change; use `exclude-paths` on `"."` to keep sub-package changes out — [release-please manifest-releaser.md](https://raw.githubusercontent.com/googleapis/release-please/main/docs/manifest-releaser.md)
- The manifest doc's action example still shows `googleapis/release-please-action@v2` with `command: manifest` (stale relative to current action versions) — [release-please manifest-releaser.md](https://raw.githubusercontent.com/googleapis/release-please/main/docs/manifest-releaser.md)
- Schema: `include-v-in-tag` ("Defaults to `true`"), `tag-separator` ("separator between the component and version in the GitHub tag"), `include-component-in-tag` (default `true`), `version-file` ("Used by `ruby` and `simple` strategies"), `exclude-paths` ("If all files from commit belong to one of the paths it will be skipped"), `separate-pull-requests`, `initial-version`; `release-type` is a free-form string — [release-please config schema](https://raw.githubusercontent.com/googleapis/release-please/main/schemas/config.json)
- `simple` release type: "A repository with a version.txt and a CHANGELOG.md" — [release-please customizing.md](https://raw.githubusercontent.com/googleapis/release-please/main/docs/customizing.md)
- `extra-files`: default updaters chosen by extension (`.json` -> `version` property or `jsonpath`; `.yaml/.yml` with `jsonpath`; `.toml` with `jsonpath`; `.xml` with `xpath`); force generic with `{"type": "generic", "path": "..."}`. Generic markers: inline `x-release-please-version` / `-major` / `-minor` / `-patch` (only that line), block `x-release-please-start-version` ... `x-release-please-end`; annotations "usually in comments". The doc's own example is `"extra-files": ["path/to/file.md"]` — [release-please customizing.md](https://raw.githubusercontent.com/googleapis/release-please/main/docs/customizing.md)
- Generic updater source: `VERSION_REGEX = /(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(-(?<preRelease>[\w.]+))?(\+(?<build>[-\w.]+))?/` applied with non-global `line.replace` — only the first semver on a marked line is replaced; block start line is kept unchanged; no file-type check, content split with `/\r?\n/`; scopes also include `version-date` and `date` — [release-please generic.ts](https://raw.githubusercontent.com/googleapis/release-please/main/src/updaters/generic.ts)
- An XML example with an HTML comment `<!-- x-release-please-version -->` appears in a release-please-action PR (search snippet) — [release-please-action PR 410](https://github-redirect.dependabot.com/googleapis/release-please-action/pull/410)

### Inferences
- Configuration sketch for (B) independent versions: packages `"src"` (release-type `simple`, component `cli`, tags `cli-v1.2.0`) and `"skills"` (release-type `simple`, component `skills`, tags `skills-v0.4.0`, `extra-files` = every `SKILL.md` with type `generic`). For (A) one shared version: one package `"."` or the `linked-versions` plugin over both components.
- Markdown placement: frontmatter line `  version: 0.9.0 # x-release-please-version` is valid YAML, but the repo's `MetadataVersionRegex` would capture `0.9.0 # x-release-please-version` and fail `version.ToString() == value` (**FACT (repo)**: see baseline), so the guard would need a change. Body text: either an inline HTML comment on the handshake line (visible to the model as raw text) or a block `<!-- x-release-please-start-version -->` ... `<!-- x-release-please-end -->` around the handshake paragraph only; wrapping a whole file is risky because the first semver on every line would be rewritten (e.g. other tool versions like "Claude Code 2.1.318").
- `extra-files` cannot be globbed in the docs fetched; 19 skills -> 19 explicit entries to maintain (a new skill must be added to the config; a guard test could check that).
- Conventional Commits drive bumps (repo already uses them per CLAUDE.md). For the .NET CLI, release-please would own the tag; the build version would then have to come from the tag (GitVersion/MinVer with `tag-prefix cli-v`) or from `version.txt` read by the Cake build. Mixing release-please's own version with GitVersion's computed version is a source-of-truth conflict to resolve.
- Release flow: release-please opens/updates a Release PR; merging it commits the bumped files and creates the tag and GitHub release; the existing `release` workflow (tag `v<SemVer>`) would need to accept component tags or keep `include-component-in-tag: false` for the CLI.
- Pre-1.0 bump semantics: `bump-minor-pre-major` maps breaking changes to minor, matching the repo's current "breaking change raises the minor below 1.0.0" rule in `GitVersion.yml`.

### Gaps
- Whether `extra-files` paths are resolved relative to the package path or the repo root (schema says only "The path to the file"); not verified.
- Whether the generic updater preserves CRLF/trailing newline (source splits on `/\r?\n/`; join behaviour not read) — relevant to the repo's line-ending rules.
- Current release-please / release-please-action version numbers not verified in this session.

## changesets, semantic-release (monorepo), Knope, cocogitto — fit for .NET + Markdown

### Takeaway
Knope (regex `versioned_files`) and cocogitto (bump hooks running any command) can update Markdown and support multiple packages; changesets and semantic-release are npm-workspace oriented and a poor fit for a .NET + Markdown repo.

### Cited Findings
- Knope `versioned_files`: named formats Cargo.toml, Cargo.lock, gleam.toml, go.mod, package.json, package-lock.json, deno.json(c), deno.lock, pyproject.toml, pom.xml, pubspec.yaml, tauri.conf.json; any other file via `{ path = "README.md", regex = "version: (?<version>\\d+\\.\\d+\\.\\d+)" }` with a named `version` group; `.csproj` not listed; multiple packages via `[packages."<name>"]`; conventional-commit `scopes` per package — [Knope packages config](https://knope.tech/reference/config-file/packages/)
- cocogitto monorepo: `[packages]` in `cog.toml` with `path`; `cog bump --auto` tags each changed package plus a global monorepo tag; `cog bump --package=<p> --auto`; `public_api = false`; `bump_order`; `pre_bump_hooks`/`post_bump_hooks`, `pre_package_bump_hooks`/`post_package_bump_hooks` (example `cargo set-version {{version}}`, `{{package}}` variable); per-package CHANGELOG.md — [cocogitto monorepo guide](https://docs.cocogitto.io/guide/monorepo.html)
- semantic-release has no native monorepo mode; `semantic-release-monorepo` (listed at 6.1.1) runs semantic-release per package via `-e`/`extends`; `multi-semantic-release` (anolilab fork maintained, requires Node.js ^22.14.0 || >=24.10.0) discovers packages through npm/yarn/pnpm/bolt workspaces — [semantic-release-monorepo on npm](https://www.npmjs.com/package/semantic-release-monorepo); [anolilab multi-semantic-release](https://github.com/anolilab/multi-semantic-release); [multi-semantic-release README](https://cdn.jsdelivr.net/npm/@anolilab/multi-semantic-release@4.4.5/README.md)
- changesets: contributors declare how changes should be released; the tool updates versions, changelogs and publishing; aimed at monorepos — [changesets README](https://raw.githubusercontent.com/changesets/changesets/main/README.md)

### Inferences
- Knope's regex entries could match `^  version: (?<version>...)` in frontmatter and the `--expect (?<version>...)` text without adding markers to SKILL.md — cleaner than release-please markers for this repo, but each regex replaces one pattern per file entry (unverified whether multiple matches per file are replaced).
- cocogitto's hooks can call a Cake task or script that rewrites SKILL.md, then commits as part of `cog bump`; it creates tags locally (no release-PR model by default).
- changesets/semantic-release would introduce a Node toolchain and package.json files purely for versioning; not recommended here.

### Gaps
- changesets' requirement of `package.json` per package was not confirmed from the README fetched (only high-level text).
- Knope tag format for multiple packages (`<package>/v1.2.3`?) and whether one `regex` entry replaces all matches in a file — not on the fetched page.
- Knope and cocogitto current version numbers not verified.

## Alternative: no version literal in SKILL.md, or a rewrite step plus guard

### Takeaway
Because skills are installed straight from git and Claude Code does not show frontmatter to the model (repo CLAUDE.md), the body literal can only disappear if the handshake reads the version from somewhere the agent can see at run time; the lowest-risk automation is a single source file plus a Cake/CI "stamp" task that rewrites all literals and a guard that checks them — tool-agnostic and compatible with any of the tools above.

### Cited Findings
- **FACT (repo, CLAUDE.md):** Claude Code does not show the frontmatter to the model (checked with Claude Code 2.1.318), so every skill that calls `version --expect` states the version in the handshake text; when the version is raised, frontmatter and handshake texts change together; `SkillKitVersionTests` enforces equality with the engine build's major.minor.
- **FACT (repo, CLAUDE.md):** skills are installed from the tag `v<engine>` or `main`, and there is no install-time transformation (`SingleSkillSourceTests`).
- release-please `simple` type uses a `version.txt` / `version-file` as the single source and can propagate to extra files — [release-please customizing.md](https://raw.githubusercontent.com/googleapis/release-please/main/docs/customizing.md); [release-please config schema](https://raw.githubusercontent.com/googleapis/release-please/main/schemas/config.json)
- cocogitto `pre_bump_hooks` can run an arbitrary command such as a rewrite script with `{{version}}` — [cocogitto monorepo guide](https://docs.cocogitto.io/guide/monorepo.html)

### Inferences
- Option 1 — single source file (e.g. `skills/VERSION` or `.claude-plugin/plugin.json` field) + stamp task: `./build.sh -t stamp-skills-version` (Cake) rewrites frontmatter and handshake lines from the source file; guard test stays. Pros: one manual edit (or none if a tool writes the source file), no markers in SKILL.md, works for (A) and (B). Cons: still a commit before the tag; the stamp must run before merge (CI check fails otherwise).
- Option 2 — handshake without literal: the skill tells the agent to read a version file shipped in the plugin (e.g. `${CLAUDE_PLUGIN_ROOT}`-relative), then call `--expect`. Pros: zero literals. Cons: agent-specific path resolution across Claude Code/Copilot/Codex (violates "agent-agnostic without loss" unless all three expose the plugin root — unverified), one more tool call per handshake, and a misread file silently defeats the check.
- Option 3 — engine owns compatibility: `--expect` replaced by a compatibility range file (e.g. skills declare `engine: ">=0.9 <0.10"`). Pros: decouples (B) versions. Cons: still a literal in skills, and a range file is another thing to keep in sync.
- Option 4 — pre-commit hook rewriting literals: fragile (hooks are per clone, not enforced in CI); a CI check is still needed.

### Gaps
- Whether Copilot CLI and Codex CLI expose a plugin-root variable/path the skill text could use to read a shipped version file — not researched here.

## Local development: dev-build conventions, relaxing compatibility checks, CI vs local detection

### Takeaway
None of the tools makes local feature-branch builds "release-shaped": GitVersion yields `<next>-<branch>.<n>`, MinVer `x.y.z-alpha.0.<height>`, NBGV `x.y.z-g<sha>` for non-public refs. The practical fix is in the engine/handshake (treat prerelease/dev engines as compatible on major.minor or with an env-var override) plus making the skills' major.minor equal to the branch's computed major.minor.

### Cited Findings
- NBGV: refs not matching `publicReleaseRefSpec` keep the `-g<sha>` suffix; `gitCommitIdIncludeDirty` adds `-dirty`; `cloudBuild.setVersionVariables` / `nbgv cloud` integrate with CI — [NBGV version.json](https://dotnet.github.io/Nerdbank.GitVersioning/docs/versionJson.html); [NBGV nbgv CLI](https://dotnet.github.io/Nerdbank.GitVersioning/docs/nbgv-cli.html)
- MinVer: no tag -> `0.0.0-alpha.0` + height; `MinVerMinimumMajorMinor` raises the floor; `MinVerBuildMetadata`; `MinVerSkip` for Debug — [MinVer README](https://raw.githubusercontent.com/adamralph/minver/main/README.md)
- GitVersion: feature label `{BranchName}`, `{env:VARIABLE_NAME}` with `??` fallback in labels; ManualDeployment keeps the prerelease version and adds build metadata — [GitVersion 7.0 configuration](https://gitversion.net/7.0/docs/reference/configuration); [GitVersion 7.0 modes](https://gitversion.net/7.0/docs/reference/modes/)
- **FACT (repo, CLAUDE.md):** `version --expect` exits 50 if major or minor differ; a prerelease engine only warns. `SHADE_KIT_MARKETPLACE_SOURCE` is the developer override for the marketplace source (local path or `owner/repo#ref`).

### Inferences
- A label like `label: '{env:SHADEKIT_LOCAL_LABEL ?? {BranchName}}'` (syntax per GitVersion `??` fallback; exact nesting unverified) could mark local builds, e.g. `-local`.
- Common conventions: `0.0.0-dev` for builds without tags, `+local`/`+<sha>` build metadata (ignored for precedence by SemVer). Distinguishing CI from local typically uses the `CI`/`GITHUB_ACTIONS` env vars (Cake's build system detection) — not tool-specific.
- For local skill testing, `SHADE_KIT_MARKETPLACE_SOURCE=$PWD` + the existing "prerelease engine only warns" rule already relax the check; the remaining pain is the guard's major.minor equality when `next-version` lags — automated minor raising (release-please/Knope writing the files) removes that.

### Gaps
- No primary source found for an industry-standard `IsLocalBuild` property; Cake's `BuildSystem.IsLocalBuild` was not verified in this session.

## Combination with the fewest manual steps for GitHub Actions + Cake + tag-based nuget.org releases

### Takeaway
Fewest manual steps: release-please (manifest mode) as the single release driver that rewrites SKILL.md literals via a release PR and creates tags, with the CLI build version read from the tag (GitVersion or MinVer with the matching prefix); or, tool-agnostic, a single version source file + Cake stamp task + guard. Shared version (A) is simpler and keeps the current `--expect` semantics; independent versions (B) need a compatibility contract between engine and skills.

### Cited Findings
- release-please creates one Release PR per run (or per component), bumps manifest + extra files, and tags `<component>-v<version>` or `v<version>`; `linked-versions` keeps components in lockstep — [release-please manifest-releaser.md](https://raw.githubusercontent.com/googleapis/release-please/main/docs/manifest-releaser.md)
- Generic updater rewrites the first semver on marked lines in any text file — [release-please generic.ts](https://raw.githubusercontent.com/googleapis/release-please/main/src/updaters/generic.ts)
- GitVersion and MinVer only compute versions from tags/height; NBGV computes from committed `version.json` + height — [GitVersion 7.0 configuration](https://gitversion.net/7.0/docs/reference/configuration); [MinVer README](https://raw.githubusercontent.com/adamralph/minver/main/README.md); [NBGV version.json](https://dotnet.github.io/Nerdbank.GitVersioning/docs/versionJson.html)

### Inferences
- (A) shared version, minimal change: keep GitVersion for the build; add release-please with one package `"."`, `release-type: simple`, `include-component-in-tag: false` (tags stay `v<SemVer>`), `bump-minor-pre-major: true`, extra-files = all SKILL.md (generic). Release = merge the Release PR -> tag `v0.10.0` -> existing `release` workflow pushes to nuget.org. `next-version` can be dropped once tags exist. Manual steps per release: 1 (merge PR). Cost: markers in SKILL.md + guard regex change; release-please tag must be on `main` (it is, after the PR merge).
- (B) independent versions: release-please packages `src` (component `cli`) and `skills` (component `skills`); GitVersion `tag-prefix: 'cli-v'` + `ignore.paths` for `^skills/`; the release workflow triggers on `cli-v*`; skills need no build. The engine/skill handshake must change from "major.minor equal" to a compatibility rule (e.g. skills declare the engine range), and `integrations install` (which today derives the marketplace ref `v<engine>` from the engine version) must map engine version -> skills tag. That is the larger change.
- Tool-agnostic fallback: `skills/VERSION` + Cake `stamp` task + CI check; combine with GitVersion `+semver: minor` commits instead of `next-version`. Manual steps: edit one file (or run one task) per minor raise.
- NBGV is attractive if `version.json` per component is preferred over tags, but it diverges from the repo's tag-based release rule and still needs a stamp step for SKILL.md.

### Gaps
- No source found describing a real-world repo that uses release-please to version Markdown agent skills; the Markdown fit is inferred from the generic updater source.
- Interaction of a release-please-created tag with GitHub Actions `on: push: tags` (tags created with the default `GITHUB_TOKEN` do not trigger other workflows is a known GitHub rule, but not verified in this session) — likely requires a PAT/GitHub App token or chaining the release job in the same workflow.
