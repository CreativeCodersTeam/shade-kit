# Tasks: {{feature-title}}

<!--
How to fill this template:
- Replace every {{placeholder}} and remove every guidance comment like this one.
- List the tasks in the order they are worked on. The default is one flat list. A large feature may group its tasks under stage headings (### Setup, ### Foundation, ### Requirements, ### Polish); each stage ends with a line "Checkpoint: <what works now>".
- Task IDs T-nnn are stable. Never renumber them. A dropped task stays in the list with "Not done: dropped – <reason>" in its notes.
- The checkbox is the task status. [x] means done: the code is written and every proving test of the task exists and passes. There is no separate status field.
- A proving test's checkbox is [x] when the test exists and passes.
- A task that is not done at the end of implement keeps its empty checkbox, and its notes start with "Not done: blocked | skipped | dropped – <reason>".
- Parallel: "yes" only when the task touches other files than every task it could run alongside and depends on none of them.
- Mapping rules, checked by plan before approval:
  - Every requirement in spec.md that is not removed appears in at least one task.
  - Every acceptance criterion has at least one proving test, or is marked manual in the coverage matrix.
-->

Feature: `{{feature-id}}` · Spec: [spec.md](spec.md) · Plan: [plan.md](plan.md)

## Tasks

<!--
Every task uses the same shape.
Example:
- [ ] **T-003** – Add token refresh to the SSO client
  - Requirements: REQ-002
  - Depends on: T-001
  - Parallel: no
  - Affected: `src/Auth/SsoClient.cs`
  - Proving tests:
    - [ ] `SsoClientTests.Refresh_ExpiredToken_RenewsToken` → AC-002.1
    - [ ] `SsoClientTests.Refresh_RevokedToken_Throws` → AC-002.2
  - Notes: –
-->

- [ ] **T-001** – {{task-title}}
  - Requirements: {{REQ-nnn}}
  - Depends on: {{T-nnn | none}}
  - Parallel: {{yes | no}}
  - Affected: {{paths or components}}
  - Proving tests:
    - [ ] `{{test name}}` → {{AC-nnn.n}}
  - Notes: {{– | deviations, reason why not done}}

## Coverage matrix

<!--
One row per acceptance criterion in spec.md; criteria of removed requirements are left out.
plan writes the matrix, implement keeps it in sync with the task blocks, and verify and the report read it.
A row without a proving test that is not marked manual is a gap. plan reports every gap before the human is asked to approve.
Manual: "no", or "yes – <reason>" for criteria that go into playbook.md.
Example:
| AC-002.1 | REQ-002 | `SsoClientTests.Refresh_ExpiredToken_RenewsToken` | T-003 | no |
-->

| Criterion | Requirement | Proving tests | Tasks | Manual |
|---|---|---|---|---|
| {{AC-nnn.n}} | {{REQ-nnn}} | {{test names}} | {{T-nnn}} | {{no \| yes – reason}} |
