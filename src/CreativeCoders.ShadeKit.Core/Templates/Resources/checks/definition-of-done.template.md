# Definition of Done: {{feature-id}}

<!--
Entry gate of finish. The engine copies this template into the feature folder as checks/definition-of-done.md when finish starts. The agent checks every item against the feature artifacts and the code base and fills in Result and Evidence; then the human decides on the transition.
- Result: pending | pass | fail | n/a.
- "n/a" only when an item refers to a phase or artifact that the feature's profile does not have (for example tasks.md in quick). The reason goes into Evidence. "n/a" does not fail the gate.
- Evidence: what was checked, with IDs, paths, or command results. For "fail": what is missing.
- The agent never checks off playbook items itself. DOD-09 only confirms that a human did.
- On every run, also after a reopen, recheck every item, overwrite Result and Evidence, and add a line to "Check runs", so earlier results stay visible.
- Teams may add, change, or remove items in .shadekit/templates/checks/. Keep the IDs of existing items stable.
-->

Feature: `{{feature-id}}` · Spec: [spec.md](../spec.md) · Tasks: [tasks.md](../tasks.md) · Verification: [verification.md](../verification.md)

| ID | Criterion | Result | Evidence |
|---|---|---|---|
| DOD-01 | Build, test, and lint pass with the quality commands recorded in `review.md`, as recorded in `verification.md`. | pending | |
| DOD-02 | Coverage was measured and recorded in `verification.md`. If `config.json` sets `coverageThreshold`, the value reaches it. | pending | |
| DOD-03 | Every acceptance criterion is covered by a passing test, or is marked manual and has an item in `playbook.md`. | pending | |
| DOD-04 | Every task in `tasks.md` is done, or has a "Not done" reason that the human accepted. | pending | |
| DOD-05 | `review.md` has no open blocker. | pending | |
| DOD-06 | The quality bar was not lowered: no new analyzer or lint suppressions, skipped or deleted tests, or weakened assertions without a reason accepted in `review.md`. | pending | |
| DOD-07 | Every deviation from the plan is recorded in `plan.md` with a reason, and every migration or rollout step is done or handed over. | pending | |
| DOD-08 | The documentation matches the change (for example README, changelog, API docs), or Evidence states why no update is needed. | pending | |
| DOD-09 | Every item in `playbook.md` is checked off by a human. | pending | |

## Check runs

<!--
One line per run: date and time, result (pass | fail), and the IDs of the failed items.
Example:
- 2026-10-24 09:30 – fail (DOD-05, DOD-09)
-->
