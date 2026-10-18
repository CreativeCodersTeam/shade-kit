# Plan: {{feature-title}}

<!--
How to fill this template:
- Replace every {{placeholder}} and remove every guidance comment like this one.
- Describe HOW the spec is built and WHY this way. The order of work and the requirement → test mapping belong in tasks.md.
- Take facts from the code base and researches.md. Mark anything that could not be verified as "unverified" and list it under "Risks".
- Reference requirements by their REQ IDs from spec.md.
- Profile bugfix (short plan): fill only Summary (the chosen fix option and why, with a link to diagnosis.md), Test strategy, Risks, and Deviations. Every other section says "n/a in bugfix".
-->

Feature: `{{feature-id}}` · Spec: [spec.md](spec.md) · Research: [researches.md](researches.md) · Tasks: [tasks.md](tasks.md)

## Summary

<!-- Three to five sentences: what the feature does, in one line, and the chosen technical approach. -->

{{summary}}

## Technical context

<!--
The technical facts the plan relies on, taken from the code base. Write "n/a" where a line does not apply.
Example:
- Language and runtime: C# 14 on .NET 10
-->

- Language and runtime: {{language and version}}
- Frameworks and libraries: {{frameworks}}
- Storage: {{storage | n/a}}
- Test frameworks: {{test frameworks}}
- Target platforms: {{platforms}}
- Constraints: {{performance, compatibility, or platform constraints | n/a}}

## Approach

<!-- How the feature is built: the main idea, the data flow, and the key interfaces. Reference REQ IDs where a part of the approach serves a specific requirement. -->

{{approach}}

## Affected components

<!--
Every component, project, or path the change touches.
Change: new | changed | removed.
Example:
| `src/Auth/SsoClient.cs` | changed | REQ-001, REQ-002 |
-->

| Component or path | Change | Requirements |
|---|---|---|
| {{component or path}} | {{new \| changed \| removed}} | {{REQ-nnn}} |

## Design decisions

<!--
One subsection per decision that has a real alternative. ID D-nnn, stable.
List every rejected alternative with the reason it was rejected.
Example:
### D-001 – Refresh tokens in the client, not in middleware

- Context: Access tokens expire after 15 minutes; long requests must not fail.
- Decision: SsoClient refreshes the token before a call when it expires within 60 seconds.
- Rejected alternatives:
  - Retry in an HTTP middleware after a 401 – replays non-idempotent requests.
- Consequences: SsoClient needs a clock abstraction for tests.
-->

### D-001 – {{decision-title}}

- Context: {{context}}
- Decision: {{decision}}
- Rejected alternatives:
  - {{alternative}} – {{reason for rejection}}
- Consequences: {{consequences}}

## Test strategy

<!--
How the requirements are proven. The human approves the quality commands together with the plan; implement and verify run them and record what actually ran in review.md and verification.md.
Coverage is always measured. If config.json sets coverageThreshold, the value must reach it.
Criteria marked "(manual)" in spec.md are tested through playbook.md.
-->

| Level | Scope | Location |
|---|---|---|
| unit | {{what is tested at this level}} | {{test project or path}} |
| integration | {{what is tested at this level \| n/a}} | {{test project or path}} |
| manual | Criteria marked "(manual)" in spec.md | `playbook.md` |

Quality commands:

| Check | Command |
|---|---|
| Build | `{{build command}}` |
| Test | `{{test command}}` |
| Lint | `{{lint command}}` |
| Coverage | `{{coverage command}}` |

## Migration and rollout

<!--
Data or format migrations, breaking changes, feature flags, backwards compatibility, and how to roll back. Write "None." with the reason if the change needs none.
Example:
- `config.json` gains the field `sso`. Old files without it keep working; no migration needed.
-->

- {{migration or rollout step}}

## Risks

<!--
Technical risks, unverified assumptions, and dependencies on other teams or systems.
Example:
| The identity provider's token lifetime is unverified | Refresh logic may fire too late | Read the lifetime from the token, not from config |
-->

| Risk | Impact | Mitigation |
|---|---|---|
| {{risk}} | {{impact}} | {{mitigation}} |

## Deviations

<!--
Filled in by implement: one entry per deviation from this plan, with the date, what changed, and why. Keep "None." while planning.
Example:
- 2026-10-20 – D-001 changed: refresh window raised to 120 seconds, because the identity provider's clock skew is up to 90 seconds.
-->

- None.
