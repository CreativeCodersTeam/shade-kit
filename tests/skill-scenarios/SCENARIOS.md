# Scenarios

Work folder: `<work>`. Fixtures are in `<work>/fixtures/`. Output goes to `<work>/out/<id>.md`.

Fixture table shared by every scenario (command → file):

| Command | Fixture |
|---|---|
| `shade-kit version --json --expect <kit-version>` | `env-version.json` |
| `shade-kit phase start refine --json` | `env-start-refine-already.json` |
| `shade-kit phase start quick --json` | `env-start-quick.json` |
| `shade-kit phase next --json` | scenarios S1–S4: `env-next-refine.json`; scenario S9: `env-next-quick.json` |
| `shade-kit gate info finish --feature <quick-feature> --json` | `env-gate-info-finish.json` |

## S1 — next-steps in OpenAI Codex CLI
- Agent: OpenAI Codex CLI, default mode (not plan mode); `request_user_input` is not available.
- Skill: `shade-kit-next-steps`, loaded by `shade-kit-refine` at its end.
- Input from the calling skill: why it ends: **end of work**, "Refine round done: two requirements were added to spec.md."; warnings: none; no `phase next` result passed.
- Repository: `<work>/repo` (read only).

## S2 — next-steps in GitHub Copilot CLI
- Agent: GitHub Copilot CLI; `ask_user` is available.
- Otherwise identical to S1.

## S3 — phase-entry in OpenAI Codex CLI, phase already active
- Agent: OpenAI Codex CLI, default mode (not plan mode); `request_user_input` is not available.
- Skill: `shade-kit-phase-entry`, loaded as step 1 of `shade-kit-refine` (the human called the refine skill); phase `refine`.
- Canned answer: to the question whether to continue refine or end: "End here".
- Repository: `<work>/repo` (read only).

## S4 — phase-entry in Claude Code, phase already active
- Agent: Claude Code; `AskUserQuestion` is available.
- Otherwise identical to S3.

## S5 — gate-check in OpenAI Codex CLI, plan mode
- Agent: OpenAI Codex CLI in plan mode; `request_user_input` is available.
- Skill: `shade-kit-gate-check`, loaded by `shade-kit-phase-entry` of `shade-kit-finish`, with featureId `<quick-feature>`, target phase `finish`, checklist `checks/definition-of-done.md`.
- Repository: `<work>/repo-s5` (you may edit the checklist copy there; run `date` for real).
- Canned answers for items a human must decide: TEAM-01 pass ("PO accepted it in the ticket"), TEAM-02 pass ("told support on Monday"), TEAM-03 fail ("legal was not asked yet"), TEAM-04 pass ("release manager agreed"), TEAM-05 pass ("designer checked it").
- End the run with the return to the calling skill (no next-steps message).

## S6 — gate-check in Claude Code
- Agent: Claude Code; `AskUserQuestion` is available.
- Repository: `<work>/repo-s6`.
- Otherwise identical to S5.

## S7 — interview in GitHub Copilot CLI
- Agent: GitHub Copilot CLI; `ask_user` is available.
- Skill: `shade-kit-interview`, loaded by `shade-kit-refine` for feature `<sso-feature>` with:
  - mode `targeted`;
  - topics: T1 (blocking) "Tenant model: `request.md` asks for SSO with Entra ID, but does not say whether the app accepts accounts of one tenant or of any tenant."; T2 "Password login: `request.md` does not say whether password login stays available next to SSO.";
  - target artifact: `spec.md`, section "Requirements";
  - recording rule: each answer becomes a requirement `REQ-nnn` or changes the one it concerns;
  - recording time: `on return`;
  - artifacts to check against: `.shadekit/features/<sso-feature>/request.md`.
- Canned answers: to every topic question: the option you proposed (the recommended one). To any other question: "yes".
- Repository: `<work>/repo` (read only). End the run with the return to the calling skill.

## S8 — interview in OpenAI Codex CLI, default mode
- Agent: OpenAI Codex CLI, default mode (not plan mode); `request_user_input` is not available.
- Otherwise identical to S7.

## S9 — quick in OpenAI Codex CLI, change too large
- Agent: OpenAI Codex CLI, default mode (not plan mode); `request_user_input` is not available.
- Skill: `shade-kit-quick`; the human typed `$shade-kit:shade-kit-quick`. The current feature is `<quick-feature>` (profile `quick`).
- Repository: `<work>/repo-s9` (read only); its `request.md` is `.shadekit/features/<quick-feature>/request.md`.
- Canned answers: to any interview question about the change: "I don't know the approach yet; it is not decided between a gateway and per service." To any other question: "yes".
- Do not implement anything; end the run with the closing message.
