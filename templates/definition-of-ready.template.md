# Definition of Ready: {{feature-id}}

<!--
Entry gate of plan. The engine copies this template into the feature folder as checks/definition-of-ready.md when plan starts. The agent checks every item against the feature artifacts and fills in Result and Evidence; then the human decides on the transition.
- Result: pending | pass | fail | n/a.
- "n/a" only when an item refers to a phase or artifact that the feature's profile does not have (for example map in bugfix). The reason goes into Evidence. "n/a" does not fail the gate.
- Evidence: what was checked, with IDs or paths. For "fail": what is missing.
- On every run, also after a reopen, recheck every item, overwrite Result and Evidence, and add a line to "Check runs", so earlier results stay visible.
- Teams may add, change, or remove items in .shadekit/templates/. Keep the IDs of existing items stable.
-->

Feature: `{{feature-id}}` · Spec: [spec.md](../spec.md)

| ID | Criterion | Result | Evidence |
|---|---|---|---|
| DOR-01 | Every requirement in `spec.md` has an ID (`REQ-nnn`) and at least one acceptance criterion. | pending | |
| DOR-02 | Every acceptance criterion can be decided by an automated test, or is marked "(manual)" with a reason. | pending | |
| DOR-03 | Every requirement has a priority: must, should, or could. | pending | |
| DOR-04 | `spec.md` has no open questions: "Open questions" says "None." and no `(open: Q-nnn)` marker is left. | pending | |
| DOR-05 | `spec.md` has no template leftovers: no `{{placeholder}}` and no guidance comment. | pending | |
| DOR-06 | "Out of scope" is stated, also when nothing is excluded. | pending | |
| DOR-07 | Every assumption is confirmed by a human or marked as a risk. | pending | |
| DOR-08 | Every edge case has an acceptance criterion or is listed in "Out of scope". | pending | |
| DOR-09 | "Security considerations" lists the findings, or says "None identified." with a reason. | pending | |
| DOR-10 | The affected areas are known: `map` is done, or was skipped on purpose with a recorded reason. | pending | |

## Check runs

<!--
One line per run: date and time, result (pass | fail), and the IDs of the failed items.
Example:
- 2026-10-17 14:05 – fail (DOR-04, DOR-07)
-->
