# {{topic}}

<!--
How to fill this template:
- Replace every {{placeholder}} and remove every guidance comment like this one.
- The discuss skill writes these notes from the conversation: at the end of the discussion, or earlier when the human asks. A later write in the same discussion rewrites this file with the full state of the discussion. Writing the notes never changes the feature state.
- The heading is the topic alone, as a short title of a few words, without a prefix: when a feature is created from these notes, shade-kit feature new takes the first level-1 heading as the feature title.
- Feature: the id of the current feature, or "none" when the discussion is not about a feature (no current feature, or a finished one).
- Request: the link to the feature's request.md. The notes of a feature are in its discussions/ folder, so the link is ../request.md. Remove "· Request: …" when the feature is "none".
- Date: the day of the discussion as yyyy-mm-dd.
- Participants: the git user names of the people who took part, separated by commas.
- Context: why the topic came up and what is already given, in a few sentences.
- Options: one subsection per option that was discussed, with a short description and its pros and cons. When the discussion had several topics, put the topic in front of the option ("### <topic>: <option>"). Write "None." when no options came up.
- Conclusions: one point per decision of the human; name the chosen option. Only what the human decided goes here, never a proposal the human did not take up. Write "None." when nothing was decided.
- Open questions: one point per question that is still unresolved. Write "None." when there is none.
-->

Feature: `{{feature-id | none}}` · Request: [request.md](../request.md) · Date: {{yyyy-mm-dd}} · Participants: {{participants}}

## Context

{{context}}

## Options

### {{option}}

{{description}}

Pros:

- {{pro}}

Cons:

- {{con}}

## Conclusions

- {{conclusion}}

## Open questions

- {{open question}}
