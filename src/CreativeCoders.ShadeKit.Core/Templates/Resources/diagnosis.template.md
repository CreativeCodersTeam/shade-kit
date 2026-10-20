# Diagnosis: {{feature-title}}

<!--
Skeleton template. Package 16 (bugfix profile) owns it and writes its final structure and guidance; until then only the section headings are fixed.
How to fill this template:
- Replace every {{placeholder}} and remove every guidance comment like this one.
- Reproduction: a reliable way to trigger the bug, with the steps and the environment. If the bug cannot be reproduced, stop and ask the human for more information instead of guessing.
- Root cause: traced from the symptom to the cause in the code, with file and line evidence.
- Ruled-out causes: every cause that was checked and excluded, with the reason.
- Affected code: the files, types, and tests the fix touches, with paths. This section is the evidence for the Definition of Ready item "affected areas known".
- Fix options: every option with its trade-offs, and the recommended one.
-->

Feature: `{{feature-id}}` · Request: [request.md](request.md) · Spec: [spec.md](spec.md) · Research: [researches.md](researches.md)

## Reproduction

{{steps and environment}}

## Observed and expected behaviour

{{observed behaviour versus expected behaviour}}

## Root cause

{{root cause with file and line evidence}}

## Ruled-out causes

{{causes checked and ruled out}}

## Affected code

{{affected files, types, and tests}}

## Fix options

{{fix options with a recommendation}}
