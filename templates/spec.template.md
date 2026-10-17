# Spec: {{feature-title}}

<!--
How to fill this template:
- Replace every {{placeholder}} and remove every guidance comment like this one.
- Describe WHAT is needed and WHY. HOW it is built belongs in plan.md.
- IDs (REQ, AC, A, Q) are stable. Never renumber them. A removed requirement stays in place with "Status: removed" and the reason, so references in tasks.md, tests, and the report stay valid.
- Mark an unclear statement inline with (open: Q-nnn) and add the question to "Open questions".
-->

Feature: `{{feature-id}}` · Request: [request.md](request.md) · Research: [researches.md](researches.md)

## Goal

<!-- One short paragraph: which problem the feature solves and for whom. -->

{{goal}}

## Glossary

<!--
Domain terms and key entities the requirements use. For an entity, name its key attributes and relations, without implementation details. Write "None." if the requirements need no definitions.
Example:
| Session | Entity | A signed-in user's access, valid until sign-out or expiry. Belongs to one user. |
-->

| Term | Kind | Meaning |
|---|---|---|
| {{term}} | {{term \| entity}} | {{meaning}} |

## Requirements

<!--
One subsection per requirement.
- ID: REQ-nnn, unique within the feature, starting at REQ-001.
- Priority: must | should | could. "won't" belongs in "Out of scope".
- Status: active | removed (with the reason).
- Acceptance criteria: at least one per requirement, ID AC-<requirement number>.<n>, for example AC-001.1.
  Use Given/When/Then for behaviour. Use one verifiable statement for non-functional criteria (performance, security, compatibility).
  Every criterion can be decided by an automated test, or is marked "(manual)" with the reason. Manual criteria go into playbook.md in verify.
Example:
### REQ-001 – Sign in with company SSO

- Priority: must
- Status: active
- Rationale: Users must not keep a separate password for the app.

A user with a company account can sign in through the company identity provider.

Acceptance criteria:

- **AC-001.1** Given a user with a valid company account, when they choose "Sign in with SSO", then they are signed in and see their dashboard.
- **AC-001.2** The SSO sign-in completes in under 2 seconds at p95.
-->

### REQ-001 – {{requirement-title}}

- Priority: {{must | should | could}}
- Status: active
- Rationale: {{why this is needed}}

{{requirement statement}}

Acceptance criteria:

- **AC-001.1** {{criterion}}

## Edge cases

<!--
Boundary conditions, error scenarios, and unusual inputs. Each edge case points to the acceptance criterion that covers it, or is listed in "Out of scope".
Example:
- The session expires while the user is signing in → AC-001.3
-->

- {{edge case}} → {{AC-nnn.n | out of scope}}

## Security considerations

<!--
Findings of the security research at requirement level (section "refine / security" in researches.md): data that needs protection, roles and permissions, abuse cases. Link each finding to the requirement or criterion that handles it. If there are none, write "None identified." with the reason.
Example:
- Tokens from the identity provider are secrets and must never be logged → REQ-004
-->

- {{finding}} → {{REQ-nnn | AC-nnn.n}}

## Out of scope

<!-- Stated explicitly, including "won't" requirements. Write "Nothing excluded." if nothing is. -->

- {{excluded item}}

## Assumptions

<!--
Defaults chosen where the request was silent. Before plan starts, each assumption is confirmed by a human or marked as a risk.
Status: unconfirmed | confirmed | risk.
Example:
- **A-001** Every user already has a company account. (confirmed)
-->

- **A-001** {{assumption}} ({{unconfirmed | confirmed | risk}})

## Open questions

<!--
Every unresolved question, with the requirements it affects. Must be empty before plan starts (Definition of Ready).
When a question is answered, move it to "Decisions" and remove its inline (open: Q-nnn) marker. Write "None." when empty.
Example:
- **Q-002** (REQ-003) Which identity providers must be supported in the first release?
-->

- **Q-001** ({{REQ-nnn}}) {{question}}

## Decisions

<!--
Questions answered during refine. Keep the Q-ID, so the history of the spec stays traceable.
Example:
- **Q-002** (REQ-003) Which identity providers must be supported in the first release? → Entra ID only; Okta is out of scope. (product owner, 2026-10-17)
-->

- **Q-nnn** ({{REQ-nnn}}) {{question}} → {{answer}} ({{decided by}}, {{date}})
