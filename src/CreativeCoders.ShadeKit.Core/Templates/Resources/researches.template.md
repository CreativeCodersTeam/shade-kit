# Research: {{feature-title}}

<!--
How to fill this template:
- Replace every {{placeholder}} and remove every guidance comment like this one.
- One file per feature. The research skill creates it from this template on the first research run and adds the sections of the focuses that ran. It writes no empty sections.
- One section per phase and focus, headed "<phase> / <focus>" with the short name of the focus (for example "refine / gaps", "map / existing architecture", "diagnose / security"). Each phase owns its sections and their focuses.
- A section holds the date of its run and the findings. Every finding has an ID (the prefix of its focus and a number, for example G-1) and names its source inline: a file path relative to the repository root with line or line range (a file of the feature folder by its name, for example request.md:12), a link, or a search that shows that something is missing ("Search: <pattern> in <path>, no match"). A finding without a confirmed source is marked as an assumption with its basis; a part that its source does not back is marked as an assumption on the same line.
- A later run of the same phase and focus replaces that section in place; the section of a new focus is added at the end of the file. A run never touches the other sections. The file keeps only the latest run of each section; git keeps the history.
- Other skills may add a note to a finding, for example the reason the human dismissed it ("Dismissed: <reason>"). The next run of the section reads the notes, does not report a dismissed finding again without new evidence, and carries the dismissed finding with its note into the new section.
- A phase that skips its research on purpose may record a "<phase> / skipped" section with the reason and the git user (for example "map / skipped").

Shape of a section:

## refine / gaps

Date: 2026-11-13

- **G-1** "Fast login" has no target time. Source: request.md:12
- **G-2** The request does not say what happens to open sessions when an account is disabled. Source: request.md:8-15
- **G-3** Every user already has a company account. Source: Search: "UserStore" in src/, no match. Assumption: the request implies it.
- **G-4** The request names Okta, the existing login only Entra ID. Sources: request.md:21; src/Auth/LoginOptions.cs:14 Dismissed: already answered by Q-002 in spec.md.

A section without findings says what was checked: "No findings. Checked: <what>."
-->

Feature: `{{feature-id}}` · Request: [request.md](request.md) · Spec: [spec.md](spec.md)
