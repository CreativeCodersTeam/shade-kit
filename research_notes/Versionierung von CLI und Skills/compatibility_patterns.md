# Version compatibility contracts between a host/engine and independently released plugins/clients

Scope: prior art for how a host (engine, daemon, editor, CLI) and its plugins/clients agree on compatibility, applied to shade-kit (a .NET CLI "engine" plus Markdown SKILL.md files shipped as a Claude Code / Copilot CLI / Codex CLI plugin from the same repo; skills call the CLI with `--json`). Research date: 2026-11-21.

Baseline (from the repository, not the web): today skills hardcode the kit version and run `shade-kit version --json --expect x.y.z`; the CLI exits 50 if major or minor differ, and a prerelease engine only warns. The skills' `metadata.version` must equal the engine build's major.minor, enforced by the `SkillKitVersionTests` guard — [CLAUDE.md](/Users/chris/src/github/shade-kit/CLAUDE.md)

## 1. What compatibility patterns exist, with real-world examples?

### Takeaway
There are five main families. (1) **Lockstep**: one version line for everything (Lerna fixed mode). (2) **Declared minimum or range of the counterpart**: the plugin declares which host versions it needs (VS Code `engines.vscode`, npm `engines`/`peerDependencies`, Terraform `required_version`, Helm `kubeVersion`, and now Claude Code's own `engines.claude-code`). (3) **A separate protocol/API version independent of the product version**: Terraform plugin protocol v5/v6, Docker Engine API 1.x, MCP date-versioned `protocolVersion`. (4) **Capability negotiation instead of version numbers**: LSP has no version handshake at all, and MCP has capabilities on top of its protocol version. (5) **Skew windows**: a supported window of N minors in each direction (Kubernetes). Mature systems combine (3) and (4). The product version is for humans, and a small, rarely bumped contract number decides compatibility.

### Cited Findings

**Lockstep (fixed) vs independent versioning**
- Lerna fixed mode: "Fixed mode Lerna projects operate on a single version line", with the version kept in `lerna.json`. "Use this if you want to automatically tie all package versions together." — [Lerna: Version and Publish](https://lerna.js.org/docs/features/version-and-publish)
- Lerna's stated drawback of fixed mode: "One issue with this approach is that a major change in any package will result in all packages having a new major version." Only packages that changed since the last release get the new version, which can cause drift, so `--force-publish` is suggested to keep versions aligned. — [Lerna: Version and Publish](https://lerna.js.org/docs/features/version-and-publish)
- Lerna independent mode: you set `"version": "independent"` in `lerna.json`, and it "allows you to more specifically update versions for each package". The docs say pairing it with semantic-release makes it "less painful". — [Lerna: Version and Publish](https://lerna.js.org/docs/features/version-and-publish)

**Declared minimum or range of the host (plugin → host)**
- VS Code: an extension declares `engines.vscode` in `package.json`, for example `"^1.8.0"`. `1.8.0` without a caret "means that your extension is compatible only with VS Code `1.8.0`", and `^1.8.0` means "1.8.0 and onwards". The field can "ensure the extension only gets installed for clients that contain the API you depend on." — [VS Code: Publishing Extensions](https://code.visualstudio.com/api/working-with-extensions/publishing-extension)
- npm `engines` is "advisory only and will only produce warnings when your package is installed as a dependency" unless the user sets the `engine-strict` config flag. — [npm package.json docs](https://docs.npmjs.com/cli/v11/configuring-npm/package-json)
- npm `peerDependencies` (the plugin→host pattern for npm): "As of npm v7, peerDependencies are installed by default". A conflict "may cause an error if the tree cannot be resolved correctly". The docs recommend making the requirement "as broad as possible" rather than locking it to specific patch versions. `peerDependenciesMeta` can mark a peer as optional. — [npm package.json docs](https://docs.npmjs.com/cli/v11/configuring-npm/package-json)
- Terraform `required_version` in the `terraform` block. Root and child modules' constraints are treated as equal, and Terraform "only proceeds if all are met". If the installed version does not satisfy them, it halts before plan, apply, or state operations. — [Terraform: Version Constraints](https://developer.hashicorp.com/terraform/language/expressions/version-constraints)
- Helm `Chart.yaml` `kubeVersion` is an optional SemVer constraint, and Helm will "fail if the cluster runs an unsupported Kubernetes version". `apiVersion: v2` marks charts that "require at least Helm 3", and `v1` charts "are still installable by Helm 3". This is a format/schema version distinct from the chart `version`. `appVersion` "is not related to the `version` field" and is "informational". — [Helm: Charts](https://helm.sh/docs/topics/charts/)
- **Claude Code plugins (directly relevant):** `plugin.json` has an `engines` object: "The Claude Code versions that the plugin works on, as a semver range under the `claude-code` key, such as `"engines": { "claude-code": ">=2.1.400" }`. Requires Claude Code v2.1.323 or later. To keep the plugin working on v2.0.15 through v2.1.75, omit `engines`. Those versions reject the manifest with an `Unrecognized key` error when it has the field." — [Claude Code: Plugin manifest reference](https://code.claude.com/docs/en/plugins-reference)
- Claude Code plugin `dependencies` entries can carry a `version` (`{ "name": ..., "marketplace": ..., "version": ... }`). The plugin `version` field "is not checked against semver", and setting it "keeps users on that version until you change it". — [Claude Code: Plugin manifest reference](https://code.claude.com/docs/en/plugins-reference)
- GitHub CLI extensions: the search found no built-in way for an extension to declare a minimum `gh` version. Extensions are just `gh-<name>` repos with an executable. — [gh extension manual](https://cli.github.com/manual/gh_extension) (absence of a mechanism is an inference from the docs, see Gaps)

**A protocol/API version separate from the product version**
- Terraform plugin protocol: "a versioned interface between Terraform CLI and Terraform Plugins". "Protocol version 6 is compatible with Terraform CLI version 1.0 and later." "Protocol version 5 is compatible with Terraform CLI version 0.12 and later." "Major versions of the protocol delineate Terraform CLI and Terraform Plugin compatibility." "Minor versions of the protocol are additive." The Registry "uses the protocol version as additional compatibility metadata when deciding which plugin versions Terraform CLI can select" (declared in the Registry manifest file). — [Terraform Plugin Protocol](https://developer.hashicorp.com/terraform/plugin/terraform-plugin-protocol)
- Docker Engine API: "A new version of the API is released when new features are added". The API is backward-compatible, and a client can pin a version in the URL (`/v1.57/containers/`). — [Docker Engine API docs](https://docs.docker.com/reference/api/engine/)
- MCP: `protocolVersion` is a date string (for example `"2025-06-18"`), separate from `clientInfo.version` / `serverInfo.version` (product versions such as `"1.0.0"`), which travel in the same `initialize` message. — [MCP Lifecycle 2025-06-18](https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle)

**Capability negotiation**
- LSP: "Not every language server can support all features defined by the protocol. LSP therefore provides 'capabilities'". They are exchanged in `initialize`. "Features in the spec are kept compatible using so called capability flags." There is no protocol-version field in `InitializeParams`/`InitializeResult`, and the deprecated `unknownProtocolVersion` error code notes "There is no version handshake in version 3.0x". — [LSP 3.17 specification](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/)
- LSP tolerance rules: "Clients should ignore server capabilities they don't understand (e.g. the initialize request shouldn't fail in this case)" and "Servers receiving a ClientCapabilities object literal with unknown properties should ignore these properties." — [LSP 3.17 specification](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/)
- MCP capabilities ("Client and server capabilities establish which optional protocol features will be available during the session") include an `experimental` slot on both sides. Both parties "MUST ... Only use capabilities that were successfully negotiated". — [MCP Lifecycle 2025-06-18](https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle)

**Skew windows**
- Kubernetes: "`kubectl` is supported within one minor version (older or newer) of `kube-apiserver`". "`kubelet` must not be newer than `kube-apiserver`" and "may be up to three minor versions older". In HA, apiservers "must be within one minor version". — [Kubernetes Version Skew Policy](https://kubernetes.io/releases/version-skew-policy/)

### Inferences
- shade-kit today is lockstep plus an exact major.minor match. That is stricter than Lerna fixed mode, because a major.minor bump on either side forces a matching bump on the other even when the CLI contract did not change.
- The Claude Code `engines.claude-code` field shows that the host agent ecosystem itself has adopted the "plugin declares a minimum host range" pattern. It constrains the agent version, not the shade-kit CLI, so it does not replace a skills↔CLI check. It is the same design shape, though.
- The CLI's `--json` envelope already acts like a protocol. It could get its own contract number (like Docker API 1.x or Terraform protocol v5/v6), independent of the product SemVer.

### Gaps
- Chrome DevTools Protocol versioning (stable "1.3" vs tip-of-tree) was not researched in this pass. No citations.
- Homebrew's `depends_on macos:` / minimum version mechanisms were not researched. No citations.

## 2. For each pattern: who declares what, where is the check, what happens on mismatch, and how are dev/prerelease builds treated?

### Takeaway
In the "declared minimum" family, the **plugin declares** and the **host checks at install or load time**, usually with a hard fail (Terraform, Helm, Claude Code engines) or only a warning (npm engines by default). In the negotiation family, **both sides advertise** and the check happens **at connection time**, usually by **degrading** to the common subset (Docker, LSP, MCP capabilities) and only failing when no overlap exists (MCP: the client "SHOULD disconnect"; Docker: "client version is too old"). Prerelease handling is the most common source of surprises. SemVer ranks prereleases *below* their release, and Terraform's range operators ignore prereleases entirely.

### Cited Findings

**VS Code**
- Declared by the extension in `package.json`. The check limits who gets the extension or update: with `^1.9.0`, Stable users "will only get the update when the Stable release reaches version `1.9.0`", while Insiders get it first. — [VS Code: Publishing Extensions](https://code.visualstudio.com/api/working-with-extensions/publishing-extension)
- Dev/Insiders: to target an Insiders build by date, you add a `YYYYMMDD` tag, such as `^1.56.0-20210428`. "VS Code 1.57 and later enforce the date tag", while "Earlier versions evaluate only the numeric portion of the version." — [VS Code: Publishing Extensions](https://code.visualstudio.com/api/working-with-extensions/publishing-extension)
- Pre-release extension channel: "Pre-release extensions are supported after VS Code version `1.63.0`." Pre-release and release extension versions must differ, and the docs suggest even minors for releases and odd minors for pre-releases (instead of SemVer prerelease labels). — [VS Code: Publishing Extensions](https://code.visualstudio.com/api/working-with-extensions/publishing-extension)

**Terraform**
- Declared by the module/root (`required_version`, `required_providers`). Checked by the CLI before plan/apply, as a hard stop. — [Terraform: Version Constraints](https://developer.hashicorp.com/terraform/language/expressions/version-constraints)
- Prerelease: "Terraform does not match pre-release versions on `>`, `>=`, `<`, `<=`, or `~>` operators." To select one: "set the exact version number using the `=` operator." — [Terraform: Version Constraints](https://developer.hashicorp.com/terraform/language/expressions/version-constraints)
- `~>` "Allows only the right-most version component to increment" (`~> 1.1` allows `1.10` but not `2.0`). — [Terraform: Version Constraints](https://developer.hashicorp.com/terraform/language/expressions/version-constraints)

**npm**
- Declared by the package. `engines` produces warnings only, unless `engine-strict` is set. `peerDependencies` can fail tree resolution. — [npm package.json docs](https://docs.npmjs.com/cli/v11/configuring-npm/package-json)

**Helm**
- Declared by the chart (`kubeVersion`, `apiVersion`). Checked at install, and Helm fails on an unsupported cluster version. — [Helm: Charts](https://helm.sh/docs/topics/charts/)

**Claude Code plugins**
- Declared by the plugin (`engines.claude-code`), with a semver range in node-semver syntax. — [Claude Code: Plugin manifest reference](https://code.claude.com/docs/en/plugins-reference)
- Unknown top-level manifest fields are normally stripped with a validator warning. Strict sub-objects (`userConfig` options and others) fail on unknown keys. — [Claude Code: Plugin manifest reference](https://code.claude.com/docs/en/plugins-reference)

**Docker Engine API**
- Both sides know their API range. "The Docker Engine API server and client support API-version negotiation". A client talking to an older daemon settles on the highest version both support, and newer features are turned off. Compatibility is "best effort". — [Docker Engine API docs](https://docs.docker.com/reference/api/engine/)
- Escape hatch: `DOCKER_API_VERSION` forces a specific version, and "This environment variable disables API version negotiation". — [Docker Engine API docs](https://docs.docker.com/reference/api/engine/)
- Server-side minimum: current 29.x lists 1.40 as the minimum API (29.0–29.2 listed 1.44), and 28.x and earlier list 1.24 or lower. "API versions before v1.40 are deprecated and no longer supported". — [Docker Engine API docs](https://docs.docker.com/reference/api/engine/)
- Mismatch below the minimum is a hard error from the daemon: `Error response from daemon: client version 1.43 is too old. Minimum supported API version is 1.44, please upgrade your client to a newer version`. The operator override is `DOCKER_MIN_API_VERSION=1.24 dockerd` or `"min-api-version": "1.24"` in `daemon.json`. — [Docker blog: Engine v29](https://www.docker.com/blog/docker-engine-version-29/)

**MCP**
- The client sends the latest `protocolVersion` it supports. If the server supports it, it "MUST respond with the same version". Otherwise it responds with another version it supports (the latest). "If the client does not support the version in the server's response, it SHOULD disconnect." Over HTTP, the client must send `MCP-Protocol-Version` on later requests. Example error: code `-32602` "Unsupported protocol version" with `data.supported` / `data.requested`. — [MCP Lifecycle 2025-06-18](https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle)

**LSP**
- No version check. Each side advertises capabilities, and unknown ones are ignored (degraded mode by design). — [LSP 3.17 specification](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/)

**Kubernetes**
- The policy is documented, not a handshake. It is enforced by upgrade order: apiserver first, then controllers, then optionally kubelets. The apiserver must "not skip minor versions when upgrading". — [Kubernetes Version Skew Policy](https://kubernetes.io/releases/version-skew-policy/)

**SemVer prerelease ordering (applies to all range checks)**
- "Pre-release versions have a lower precedence than the associated normal version." "Build metadata MUST be ignored when determining version precedence." — [SemVer 2.0.0](https://semver.org/)

### Inferences
- A "min version" check that treats `1.4.0-alpha.3` as satisfying `>=1.4.0` is wrong under SemVer: the prerelease is lower. A dev build of the next minor therefore fails a minimum that equals that minor. Terraform avoids the trap by ignoring prereleases in ranges unless pinned exactly. VS Code avoids it with a date tag and with odd/even minors instead of prerelease labels. Today shade-kit sidesteps it by only warning for a prerelease engine.
- Docker and MCP both pair the check with a structured error that carries supported vs requested versions. That matches shade-kit's envelope model (`error.code` plus message), so a skill could react to it deterministically.

### Gaps
- What VS Code shows on an `engines` mismatch (marketplace hides the version vs install error text) is not stated on the fetched page.
- Whether Claude Code hard-fails or warns when `engines.claude-code` is unsatisfied is not stated on the fetched page. Nor does it say how Claude Code v2.1.76–v2.1.322 treat the field (presumably stripped as an unknown top-level key, but unverified).
- How Copilot CLI and Codex CLI treat an `engines` key in `plugin.json` was not researched.

## 3. Bidirectional checks: where does each side declare a minimum of the other, and how is a breaking change signalled?

### Takeaway
Real bidirectional schemes need **two numbers per side**: "the newest I speak" and "the oldest I still accept". Docker is the clearest example. The client knows its max API, the daemon has a max and a configurable **min API version**, and negotiation picks the highest overlap. Kubernetes puts the same idea in a written skew window. Terraform does it through protocol majors that both CLI and provider declare. A breaking change is signalled by bumping the *contract's* major (Terraform protocol v5→v6), by raising the *server's minimum* (Docker 29: min API 1.24→1.44), or by publishing a new dated protocol revision (MCP). It is not signalled by the product version.

### Cited Findings
- Docker: the daemon-side minimum is an explicit, operator-overridable setting (`DOCKER_MIN_API_VERSION`, `min-api-version`). The client side negotiates down or is forced with `DOCKER_API_VERSION`. — [Docker blog: Engine v29](https://www.docker.com/blog/docker-engine-version-29/); [Docker Engine API docs](https://docs.docker.com/reference/api/engine/)
- Docker on asymmetric skew: when the daemon is newer, the client "doesn't know about new features or deprecated API endpoints". When the client is newer, it "can request API endpoints that the daemon doesn't know about." — [Docker Engine API docs](https://docs.docker.com/reference/api/engine/)
- Docker 29's raised minimum broke real clients. Traefik's Docker provider kept using API 1.24 and was rejected ("client version 1.24 is too old"). Reported workarounds were downgrading Docker to 28.x or lowering `min-api-version`. — [Docker forum thread](https://forums.docker.com/t/docker-29-increased-minimum-api-version-breaks-traefik-reverse-proxy/150384); [Traefik community thread](https://community.traefik.io/t/traefik-stops-working-it-uses-old-api-version-1-24/29019)
- Kubernetes declares the window from the server's point of view, in both directions for kubectl (±1 minor) and one direction for kubelet (never newer, up to 3 older). — [Kubernetes Version Skew Policy](https://kubernetes.io/releases/version-skew-policy/)
- Terraform: protocol majors "delineate Terraform CLI and Terraform Plugin compatibility", and minors "are additive". Each protocol major also maps to a minimum CLI (v5 ≥ 0.12, v6 ≥ 1.0). — [Terraform Plugin Protocol](https://developer.hashicorp.com/terraform/plugin/terraform-plugin-protocol)
- MCP: each side picks from the versions it supports, and an unsupported version is surfaced with `supported` and `requested` lists. — [MCP Lifecycle 2025-06-18](https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle)
- SemVer FAQ on deprecation: "issue a new minor release with the deprecation in place", and "there should be at least one minor release that contains the deprecation" before removal in a major. — [SemVer 2.0.0](https://semver.org/)

### Inferences
- For shade-kit the two directions are different in kind:
  - **skills → CLI**: "I need CLI contract ≥ N" (a skill uses a command, option, or envelope field added in N). The skill declares it, and the CLI or the skill's handshake checks it.
  - **CLI → skills**: "I no longer support skills older than M" (the CLI removed or renamed a command or field). The CLI declares it.
  - With a single integer contract number (say `contract: 3`) plus a CLI-side `minSupportedContract`, both directions reduce to the Docker model: the skill passes the contract it was written for, and the CLI accepts if `minSupported ≤ requested ≤ current`.
- Additive changes (new command, new optional JSON field) need no bump in the "requires" direction if skills are written tolerant (ignore unknown fields, like LSP). Only removals or semantic changes need a contract bump.

### Gaps
- Git LFS client/server version negotiation was in the brief but not researched. No citations.
- No source was found on how Terraform's go-plugin handshake (magic cookie / protocol version list) is implemented in detail. The fetched page only states the compatibility matrix.

## 4. Which approach minimizes manual steps per release, and what pitfalls do maintainers report?

### Takeaway
The lowest-maintenance schemes are those where **most releases need no compatibility edit at all**. That means the compatibility number changes only when the contract changes (protocol version, capability flag), not on every product release. Lockstep with exact matching has the highest per-release cost, because every minor bump touches every consumer. Documented pitfalls include raising a minimum and breaking long-lived clients (Docker 29 / Traefik), prerelease ordering and matching surprises (SemVer, Terraform), new manifest fields breaking old hosts (Claude Code `engines` rejected by v2.0.15–v2.1.75), and lockstep forcing spurious majors (Lerna).

### Cited Findings
- Lockstep cost: a major change anywhere bumps everything ("a major change in any package will result in all packages having a new major version"). — [Lerna: Version and Publish](https://lerna.js.org/docs/features/version-and-publish)
- Independent mode needs per-package bump decisions (prompted per changed package) and is easier with automation such as semantic-release. — [Lerna: Version and Publish](https://lerna.js.org/docs/features/version-and-publish)
- Terraform's advice to library-like consumers: "Reusable modules should constrain only their minimum allowed versions". Only root modules should use `~>` with an upper bound. — [Terraform: Version Constraints](https://developer.hashicorp.com/terraform/language/expressions/version-constraints)
- npm's advice: keep peer ranges "as broad as possible" rather than pinning. — [npm package.json docs](https://docs.npmjs.com/cli/v11/configuring-npm/package-json)
- Pitfall, raising the minimum: Docker 29's min-API bump to 1.44 broke Traefik deployments, and users had to downgrade or override. — [Docker blog: Engine v29](https://www.docker.com/blog/docker-engine-version-29/); [Docker forum](https://forums.docker.com/t/docker-29-increased-minimum-api-version-breaks-traefik-reverse-proxy/150384)
- Pitfall, a new compatibility field rejected by old hosts: Claude Code versions v2.0.15 through v2.1.75 reject a manifest that contains `engines` with an `Unrecognized key` error. — [Claude Code: Plugin manifest reference](https://code.claude.com/docs/en/plugins-reference)
- Pitfall, prereleases: SemVer ranks prereleases below the release, and Terraform's range operators never match prereleases. — [SemVer 2.0.0](https://semver.org/); [Terraform: Version Constraints](https://developer.hashicorp.com/terraform/language/expressions/version-constraints)
- Pitfall, escape hatches that disable negotiation: `DOCKER_API_VERSION` "disables API version negotiation" and should be used only when needed or for debugging. — [Docker Engine API docs](https://docs.docker.com/reference/api/engine/)
- Pitfall, old-version tooling: VS Code before 1.57 ignores the date tag and evaluates only the numeric part. — [VS Code: Publishing Extensions](https://code.visualstudio.com/api/working-with-extensions/publishing-extension)

### Inferences
- **"Forgetting to raise the minimum"** is the main risk of min-version schemes: a skill starts using a new CLI option but still declares the old minimum. Guard tests can mechanize this, just as shade-kit already has `SkillKitVersionTests`. Examples: a test that maps each CLI command/option to the contract version that introduced it, or a test that fails if the CLI's JSON contract snapshot changes without a contract bump (an API-approval or snapshot test on the `--json` schemas).
- **Too-strict checks blocking local testing**: shade-kit's dev override (`SHADE_KIT_MARKETPLACE_SOURCE`) and prerelease-only-warns rule already cover part of this. A contract-integer scheme removes most of the problem, because a local dev build carries the same contract number as the release it precedes.
- Expected per-release manual steps, ranked:
  - exact lockstep match: every minor touches every skill
  - min product version: only when a skill needs a new feature, but easy to forget
  - contract integer plus min-supported: only on a contract change, and a snapshot test can enforce it
  - pure capability negotiation: no version edits, but skills must probe capabilities, which is costly in Markdown instructions

### Gaps
- No maintainer post-mortems were found specifically on "forgot to raise minimum" (the inference rests on mechanism, not a cited report).
- Babel/Angular lockstep rationale posts were not fetched in this pass. No citations.

## 5. Recommendation criteria for a small team: CLI + Markdown-skill plugin in one repo, released together but possibly on different cadences

### Takeaway
For one repo and one team, the evidence favours **decoupling product versions from a small, explicit contract version**. Keep the CLI's and the plugin's SemVer for humans and release notes. Version the `--json` command contract separately, as an integer or a protocol-style major.minor (Terraform protocol / Docker API style). Skills declare the contract they require, and the CLI declares the range it serves. Make additive changes non-breaking through tolerant parsing (LSP rule). Prereleases and dev builds should never fail the check, and a snapshot guard test should force the contract bump.

### Cited Findings
- Contract majors signal breakage, and contract minors are additive. — [Terraform Plugin Protocol](https://developer.hashicorp.com/terraform/plugin/terraform-plugin-protocol)
- Negotiate to the highest common version and expose a structured "unsupported, here is supported vs requested" error. — [MCP Lifecycle 2025-06-18](https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle); [Docker Engine API docs](https://docs.docker.com/reference/api/engine/)
- Ignore unknown fields and capabilities rather than failing. — [LSP 3.17 specification](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/)
- Consumers should declare minimums only, not upper bounds. — [Terraform: Version Constraints](https://developer.hashicorp.com/terraform/language/expressions/version-constraints); [npm package.json docs](https://docs.npmjs.com/cli/v11/configuring-npm/package-json)
- Publish an explicit skew window so users know which combinations are supported. — [Kubernetes Version Skew Policy](https://kubernetes.io/releases/version-skew-policy/)
- The host agent can additionally gate the plugin on agent version via `engines.claude-code`, but adding it breaks Claude Code v2.0.15–v2.1.75. — [Claude Code: Plugin manifest reference](https://code.claude.com/docs/en/plugins-reference)

### Inferences
Criteria to score options against:
1. **Edits per ordinary release.** Target zero. Only contract changes should touch compatibility data.
2. **Where the knowledge lives.** Skills (Markdown, agent-read) should carry one number. All comparison logic belongs in the CLI (`version --expect`-style), because Markdown cannot reliably implement range logic, and the agent is the one executing it.
3. **Both directions covered.** A skill-required contract (skills → CLI) plus a CLI `minSupportedContract` (CLI → skills), as in the Docker model.
4. **Mismatch behaviour is graded.** Too-old CLI: hard fail with an actionable message and error code (Docker "please upgrade your client"). Too-old skills: hard fail only below `minSupported`, otherwise OK. Prerelease or dev CLI: warn, never fail.
5. **Dev/local builds.** The check must not depend on SemVer prerelease ordering. A contract integer avoids it entirely. Otherwise compare on major.minor.patch with the prerelease stripped, or treat prereleases as satisfying (unlike Terraform).
6. **Enforced by tests, not discipline.** A snapshot/approval test of the `--json` contract (command list, options, envelope fields) fails when the contract changes without a contract bump. The existing guard pattern (`SkillKitVersionTests`) extends to "every skill declares the same required contract ≤ the CLI's current contract".
7. **Agent-agnostic.** A CLI-side check works identically for Claude Code, Copilot CLI, and Codex CLI. `engines.claude-code` is Claude-only and breaks old Claude Code versions, so at most it is an optional, separate gate.
8. **Upgrade order documented.** Like Kubernetes ("apiserver first"): update the CLI first, then the plugin, because a newer CLI serves older skills within the window.

Likely fit: Option (3) from section 4, a contract integer plus min-supported, as Docker does it in miniature. It keeps independent SemVer for the CLI and the plugin, which can then release on different cadences, and most releases touch no compatibility data.

### Gaps
- No source compares these patterns for Markdown/LLM-executed clients specifically. The fit judgement for skills is an inference.
- Whether Copilot CLI and Codex CLI plugin manifests offer an equivalent of `engines` was not checked.
