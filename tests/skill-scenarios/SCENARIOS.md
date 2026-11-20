# Scenarios

Work folder: `<work>`. Fixtures are in `<work>/fixtures/`. Output goes to `<work>/out/<id>.md`.

Fixture table shared by the scenarios S1–S9 (command → file):

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

## Discuss and new (S10–S17)

These scenarios replace the shared fixture table with their own table. Commands that are not in a scenario's table have
no fixture (rule 3 of the protocol).

Canned discussion (S10–S12): the human brings up the topic "Whether the app should offer SSO login with Entra ID next
to the password login." To every question about the topic: the option you proposed (the recommended one), or the first
option you named. When asked whether to end the discussion or go on: end it. When asked for the notes, or whether to
write them: yes. To any other question: "yes".

## S10 — discuss in Claude Code, no current feature
- Agent: Claude Code; `AskUserQuestion` is available.
- Skill: `shade-kit-discuss`; the human typed `/shade-kit-discuss` and then the topic above.
- Repository: `<work>/repo-s10` (you may write the notes there; run `git` read-only and `date` for real).
- Fixtures:

  | Command | Fixture |
  |---|---|
  | `shade-kit version --json --expect <kit-version>` | `env-version.json` |
  | `shade-kit feature current --json` | `env-current-none.json` |
  | `shade-kit phase next --json` | `env-next-none.json` |

## S11 — discuss in GitHub Copilot CLI, active current feature
- Agent: GitHub Copilot CLI; `ask_user` is available.
- Skill: `shade-kit-discuss`; the human typed `/shade-kit-discuss` and then the topic above.
- Repository: `<work>/repo-s11` (you may write the notes there; run `git` read-only and `date` for real).
- Fixtures:

  | Command | Fixture |
  |---|---|
  | `shade-kit version --json --expect <kit-version>` | `env-version.json` |
  | `shade-kit feature current --json` | `env-current-sso.json` |
  | `shade-kit phase next --json` | `env-next-refine.json` |

## S12 — discuss in OpenAI Codex CLI, finished current feature, no template
- Agent: OpenAI Codex CLI, default mode (not plan mode); `request_user_input` is not available.
- Skill: `shade-kit-discuss`; the human typed `$shade-kit:shade-kit-discuss` and then the topic above.
- Repository: `<work>/repo-s12` (you may write the notes there; run `git` read-only and `date` for real).
- Fixtures:

  | Command | Fixture |
  |---|---|
  | `shade-kit version --json --expect <kit-version>` | `env-version.json` |
  | `shade-kit feature current --json` | `env-current-done.json` |
  | `shade-kit phase next --json` | `env-next-done.json` |

## S13 — new in Claude Code from discussion notes
- Agent: Claude Code; `AskUserQuestion` is available.
- Skill: `shade-kit-new`; the human typed `/shade-kit-new` and then: "Create a feature from the notes in `.shadekit/discussions/<notes-file>`."
- Repository: `<work>/repo-s13` (read only).
- Canned answers: to the question about the profile: the repository's default profile. To any other question: "yes".
- Fixtures:

  | Command | Fixture |
  |---|---|
  | `shade-kit version --json --expect <kit-version>` | `env-version.json` |
  | `shade-kit feature current --json` | `env-current-none.json` |
  | any `shade-kit feature new … --json` | `env-new-notes.json` |
  | `shade-kit phase next --json` | `env-next-new-notes.json` |

## S14 — new in GitHub Copilot CLI from an issue URL that cannot be read
- Agent: GitHub Copilot CLI; `ask_user` is available.
- Skill: `shade-kit-new`; the human typed `/shade-kit-new` and then: "Start a feature for <issue-url>".
- Repository: `<work>/repo-s14` (read only).
- Canned answers: to the question about the profile: `feature`. To any other question: "yes".
- Fixtures:

  | Command | Fixture |
  |---|---|
  | `shade-kit version --json --expect <kit-version>` | `env-version.json` |
  | `shade-kit feature current --json` | `env-current-none.json` |
  | any `shade-kit feature new … --json` | `env-new-issue.json` |
  | `shade-kit phase next --json` | `env-next-none.json` |

## S15 — new in OpenAI Codex CLI, active current feature, the human ends
- Agent: OpenAI Codex CLI, default mode (not plan mode); `request_user_input` is not available.
- Skill: `shade-kit-new`; the human typed `$shade-kit:shade-kit-new` and then: "New feature: export the order list as CSV."
- Repository: `<work>/repo-s15` (read only).
- Canned answers: to the question whether to create a new feature anyway or to end: end here. To any other question: "yes".
- Fixtures:

  | Command | Fixture |
  |---|---|
  | `shade-kit version --json --expect <kit-version>` | `env-version.json` |
  | `shade-kit feature current --json` | `env-current-sso.json` |
  | `shade-kit phase next --json` | `env-next-refine.json` |

## S16 — new in Claude Code, active current feature, a new bug fix anyway
- Agent: Claude Code; `AskUserQuestion` is available.
- Skill: `shade-kit-new`; the human typed `/shade-kit-new` and then, as the requirement, exactly these two lines:

  ```text
  Login fails when the user's name contains an umlaut
  Steps: sign in as "Jörg Müller"; the page shows error 500 and the log has $LOGIN_ERROR.
  ```

- Repository: `<work>/repo-s16` (read only). Your shell is a POSIX shell (zsh).
- Canned answers: to the question whether to create a new feature anyway or to end: create a new feature anyway. To the question about the profile: `bugfix`. To any other question: "yes".
- Fixtures:

  | Command | Fixture |
  |---|---|
  | `shade-kit version --json --expect <kit-version>` | `env-version.json` |
  | `shade-kit feature current --json` | `env-current-sso.json` |
  | any `shade-kit feature new … --json` | `env-new-bugfix.json` |
  | `shade-kit phase next --json` | `env-next-new-bugfix.json` |

## S17 — new in Claude Code, the branch selects the active feature, a new feature anyway
- Agent: Claude Code; `AskUserQuestion` is available.
- Skill: `shade-kit-new`; the human typed `/shade-kit-new` and then, as the requirement: "Export the order list as CSV."
- Repository: `<work>/repo-s17` (read only); its current branch is `feature/<sso-feature>`.
- Canned answers: to the question whether to create a new feature anyway or to end: create a new feature anyway. To the question about the profile: `feature`. To any other question: "yes".
- Fixtures:

  | Command | Fixture |
  |---|---|
  | `shade-kit version --json --expect <kit-version>` | `env-version.json` |
  | `shade-kit feature current --json` | `env-current-sso-branch.json` |
  | any `shade-kit feature new … --json` | `env-new-csv.json` |
  | `shade-kit feature show <csv-feature> --json` | `env-show-csv.json` |
  | `shade-kit phase next --json` | `env-next-refine-branch.json` |
