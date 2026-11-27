> **Date:** 2026-11-27 (Arbeit am 2026-11-26)
> **Requirement:** docs/feature-packages/10-refine.md
> **Branch:** feature/10-refine
> **Mode:** direct
> **Status:** complete

# Paket 10 umgesetzt: `shade-kit-refine` mit Regeldatei, Abschnitt „Changes“ in `spec.md` und Szenarien S20–S23

## 1. Zusammenfassung

Der Phasen-Skill `shade-kit-refine` ist ausgearbeitet:
- Er ermittelt das Feature über `feature show`.
- Er stoppt ohne Template, bevor er recherchiert.
- Er bietet an, einen fehlgeschlagenen Recherche-Fokus einmal neu zu starten.
- Er befragt den Menschen zu den Findings.
- Er schreibt `spec.md` nach der neuen Regeldatei `skills/shade-kit-refine/references/spec-rules.md`.
- Er legt alle Requirements, die er in der Runde angelegt, geändert oder entfernt hat, in einem zweiten Interview dem Menschen vor.
- Er hängt pro Runde eine Zeile an den neuen letzten Abschnitt `## Changes` an.

Das Template `spec.template.md` hat diesen Abschnitt bekommen; zwei C#-Tests sichern das Template ab.

Von 5 Tasks sind 5 fertig. Dazu kommen R-1 bis R-3 und alle Minor-Findings aus Review-Runde 1. Review-Runde 2 hat keine Critical- und keine Major-Findings ergeben; ihre 8 kleineren Findings hast du als deferred angenommen (D-24).

Den Nachweis liefern 6434 grüne Tests sowie die Szenarien S20–S23 in Iteration 4 mit 125 von 125 Checks; 28 Mutationen der Ausgaben wurden alle erkannt.

**Es wurde nichts committet.** Als Nächstes `git diff` prüfen und committen, was übernommen werden soll.

## 2. Voraussetzungen

- Working Tree zu Beginn: sauber auf `feature/10-refine` (D-1, ohne Rückfrage festgehalten).
- Laufzeit-Skillliste: verfügbar (rund 90 Skills, 9 Agent-Typen).
- Größenhinweis: n/a, die Arbeit war nicht klein (mehr als 5 Dateien, Skill, Template und Szenarien).

## 3. Anforderung und Acceptance Criteria

Paket 10 umsetzen: Die Phase `refine` des Profils `feature` klärt die Anforderung des aktuellen Features. Dazu dienen eine Recherche (Fokusse `gaps`/G und `security`/RS) und ein strukturiertes Interview. Das Ergebnis schreibt sie nach `spec.md`:
- stabile IDs (REQ, AC, A, Q),
- MoSCoW-Priorität und Status,
- Out of scope, Open questions, Decisions.

`refine` kann mehrere Runden laufen. AC-1 bis AC-7 entsprechen FP10-AC1 bis FP10-AC7, AC-8 bis AC-15 kamen bei der Umsetzung hinzu.

Die Verdikte stammen aus Teil B von Review-Runde 2. Bei den Skill-ACs mutieren sie die Szenario-Ausgaben. Das belegt, dass die Checks greifen; ob der Skill-Text richtig ist, belegen die Szenario-Läufe selbst.

| AC | Text (kurz) | Origin | Tests (Datei · Name) | Result | Verdict |
|---|---|---|---|---|---|
| AC-1 | Jedes REQ hat eine ID und mindestens ein AC | explicit | check_runs.py · S20, S21 „every requirement has at least one AC-<its number>.n“ | pass | proven (executed: AC-Zeilen gelöscht → rot) |
| AC-2 | `researches.md` hat `refine / gaps` und `refine / security` | explicit | check_runs.py · S20 „researches.md has refine / gaps and refine / security“; S21 „the failed focus left refine / security of round 1 unchanged“ | pass | proven (executed: Abschnitt umbenannt → rot) |
| AC-3 | Zweite Runde behält alle IDs, removed statt gelöscht | explicit | check_runs.py · S21 „REQ-001 to REQ-003 kept“, „REQ-003 marked removed, not deleted“ | pass | proven (executed: REQ-003-Block gelöscht → rot) |
| AC-4 | Immer Out of scope und Open questions | explicit | ShippedTemplatesTests · ReadContent_SpecTemplate_HasSectionsInRuleOrderEndingWithChanges | pass | proven (executed: Template-Überschrift geändert → Test rot; „Out of scope“ entfernt → Check rot) |
|  |  |  | check_runs.py · S20, S21, S23 „spec.md sections in rule order“, „out of scope stated“, „open questions section stated“ | pass |  |
| AC-5 | Interview fragt jedes Gap-Finding oder hält die Verwerfung fest | explicit | check_runs.py · S20 „every gap finding was asked in the interview or dismissed“, „the interview asked as many topics as it returned“ | pass | proven (executed: Frage entfernt, Rückgabe unverändert → rot) |
| AC-6 | Jedes REQ hat Priorität und Status | explicit | check_runs.py · S20, S21, S23 „every requirement has a priority and a status“ | pass | proven (executed: Priority-Zeile entfernt → rot) |
| AC-7 | Beantwortete Q unter Decisions, kein Marker mehr | explicit | check_runs.py · S21, S23 „Q-001 and Q-002 under Decisions“; S20, S21, S23 „no inline marker for a decided question“, „every open question has its inline marker“ | pass | proven (executed: Marker eingefügt → rot) |
| AC-8 | Ordner und Titel über `feature show --json` | implicit | check_runs.py · S20–S23 „ran feature show <sso> --json“ | pass | proven (executed: Befehl verfälscht → rot) |
| AC-9 | Security-Findings verlinkt oder „None identified“ mit Grund | implicit | check_runs.py · S20, S21, S23 „security considerations linked or None identified“ | pass | proven (executed: unverlinkte Zeile → rot) |
|  |  |  | ShippedTemplatesTests · ReadContent_SpecTemplate_GuidanceFollowsSpecRulesForChangesAndSecurity | pass |  |
| AC-10 | Jeder Edge case zeigt auf ein AC oder out of scope | implicit | check_runs.py · S20, S21, S23 „every edge case points to an AC or out of scope“ | pass | proven (executed: Edge case ohne Ziel → rot) |
| AC-11 | Bericht nennt Offenes und empfiehlt dann eine weitere Runde | explicit | check_runs.py · S20, S21 „report recommends another round“; S21 „report names the missing security research“; S23 „report recommends another round only while something is open“ | pass | proven (executed: Empfehlung im Bericht entfernt → rot); Einschränkung: eine verneinte Empfehlung bleibt grün (Runde 2, F1, deferred) |
| AC-12 | `## Changes` zuletzt, eine Zeile pro Runde, an ältere Spec angehängt | implicit | ShippedTemplatesTests · beide Tests oben | pass | proven (executed: `created` → `initial` → rot; S23 Abschnitt entfernt → rot; S23 `created` → rot) |
|  |  |  | check_runs.py · S20 „one Changes line“; S21 „first Changes line kept and a second appended“, „an added requirement is named without its criteria“; S23 „Changes added at the end with one line for this round, not 'created'“, „the line names the changed AC-001.2“ | pass |  |
| AC-13 | Review-Interview zu jedem REQ, das die Runde berührt hat | implicit (D-2, D-9) | check_runs.py · S20 „every requirement of the round is put to the review“; S21 „every requirement the round touched is put to the review“ | pass | proven (executed: Review-Frage zu REQ-008 entfernt → rot) |
| AC-14 | Ohne Template und ohne `spec.md` Stopp vor der Recherche | implicit (D-11, D-15) | check_runs.py · S22 „no spec.md written“, „no researches.md written“, „no research run“, „no interview question“, „names the missing template“ | pass | proven (executed: zweites `feature show` → rot) |
| AC-15 | Fehlgeschlagener Fokus: einmal neu oder weiter | implicit (D-12) | check_runs.py · S21 „one question offers to research the failed focus again“, „research loaded a second time“ | pass | proven (executed: „again“ entfernt → rot) |

Zählung: 15 proven (executed), 0 read-checked, 0 unproven. Nach Runde 1 standen AC-5, AC-11 und AC-13 noch auf read-checked und AC-12 auf unproven (siehe Abschnitt 7).

## 4. Entscheidungen

| ID | Punkt | Entscheidung | Origin | Runde |
|---|---|---|---|---|
| D-1 | Working Tree | sauber, Branch `feature/10-refine` | n/a (sauber) | — |
| D-2 | 0 / G-3 Bestätigung der Entwürfe | Review-Frage am Ende zu neuen/geänderten REQs, Prioritäten, ACs | recommended accepted | 1 |
| D-3 | 0 / G-4 Artefakte späterer Phasen | Notiz über Änderungen in `spec.md` (Template-Änderung) statt Nennung im Bericht | user's own (Alternative) | 1 |
| D-4 | 1 Struktur | Regeln in `skills/shade-kit-refine/references/spec-rules.md` | user's own (Alternative) | 1 |
| D-5 | 2 Ablauf | wie heute: Interview → Spec → Review; neue Annahmen bleiben bis zur nächsten Runde `unconfirmed` | user's own (Alternative) | 1 |
| D-6 | 3 Naming | `## Changes`, Szenarien ab S20, Bericht `docs/implementation/10-refine.md` | recommended accepted | 1 |
| D-7 | 4 Changes-Form | jede Runde, letzter Abschnitt, erste Runde `created` | recommended accepted | 1 |
| D-8 | 4 / G-6 Feature-Ordner | `shade-kit feature show <id> --json` | recommended accepted | 1 |
| D-9 | 4 / G-5 Entfernen | auch der Agent darf als removed markieren, das Review bestätigt | user's own (Alternative) | 1 |
| D-10 | 4 / G-5 Findings | offene Findings → Q-nnn mit Marker; Security → REQ/AC oder offene Q | recommended accepted | 1 |
| — | 5 Feature-show-Fehler | Ende mit refusal | pre-answered — SKILL.md „Ending“ | 1 |
| D-11 | 5 / G-7 Template fehlt | Stopp vor der Recherche, end of work | recommended accepted | 1 |
| D-12 | 5 / G-7 Fokus fehlgeschlagen | Frage: einmal neu oder weiter | recommended accepted | 1 |
| D-13 | 6 Tests | S20–S22 + C#-Guard der Abschnittsreihenfolge | recommended accepted | 1 |
| D-14 | 7 Schreiben | neu aus dem Template (dessen Zeilenende), später in place, Englisch | recommended accepted | 1 |
| — | 7 Zustandsdateien | kein Lesen/Schreiben von `feature.json`/`gates.json`/`config.json` | pre-answered — CLAUDE.md „Skill conventions“ | 1 |
| — | 8 Abhängigkeiten | n/a — kein Paket hinzugefügt oder geändert | n/a | 1 |
| D-15 | Runde 2 | Template-Stopp nur ohne `spec.md`; älterer Spec wird `## Changes` angehängt | recommended accepted | 2 |
| D-16 | Runde 2 | offene Punkte in 16, 17 und im README-Log | recommended accepted | 2 |
| D-17 | Gate 2 | Skills: skill-creator, dotnet-fundamentals, dotnet-tester behalten; writing-for-agents abgewählt; Review-Set bestätigt | user's own | Gate 2 |
| D-18 | Gate 3 | Task-Liste T1–T5 (mit AC-15 in S21), Modus direct | recommended accepted | Gate 3 |
| D-19 | Gate 5 R1 | Rework-Plan R-1 bis R-3 wie vorgeschlagen | recommended accepted | Gate 5 R1 |
| D-20 | Gate 5 R1 | alle Minor/Suggestion/Nitpick beheben (M-1 bis M-4, S-1, N-1, N-2) | user's own | Gate 5 R1 |
| D-21 | Gate 5 R1 | ein Requirement behält seine Bedeutung; neue Bedeutung = removed + neue ID | recommended accepted (aus Iteration 2) | Gate 5 R1 |
| D-22 | Gate 5 R1 | neue Q-IDs in der Reihenfolge, in der das Interview fragte | recommended accepted (aus Iteration 2) | Gate 5 R1 |
| D-23 | Gate 5 R1 | Changes nennt ein hinzugefügtes/entferntes REQ nur mit seiner REQ-ID | recommended accepted (aus Iteration 1) | Gate 5 R1 |
| D-24 | Gate 5 R2 | Review akzeptiert, alle Findings der Runde 2 deferred | user's own | Gate 5 R2 |

Waivers: keine.

## 5. Skills

**Laufzeit-Discovery:** rund 90 Skills und 9 Agent-Typen. Nach dem Abgleich blieben diese Kandidaten: skill-creator:skill-creator, mattpocock-skills:writing-for-agents, cc-ai-dotnet:dotnet-fundamentals, cc-ai-dotnet:dotnet-tester, cc-ai-dotnet:dotnet-reviewer und der Agent feature-dev:code-reviewer.

| Skill / Agent | Kategorie | Vorgeschlagen | Ergebnis |
|---|---|---|---|
| skill-creator:skill-creator | Skill-Texte | Skill-Tasks | kept |
| mattpocock-skills:writing-for-agents | knowledge | optional | dropped by user (D-17) |
| cc-ai-dotnet:dotnet-fundamentals | knowledge | C#-Task | kept |
| cc-ai-dotnet:dotnet-tester | tester | C#-Task | kept |
| anthropic-skills:skill-creator | knowledge | excluded — Dublette | stayed excluded |
| cc-ai-dotnet:dotnet-xmldocs | documentation | excluded — Testcode ohne öffentliche API | stayed excluded |
| cc-ai-dotnet:dotnet-dev, cc-ai-dev:implement-dev-plan, feature-dev:feature-dev | workflow | excluded — nested workflow | stayed excluded |
| cc-ai-dotnet:dotnet-reviewer + Agent feature-dev:code-reviewer | reviewer | Review | in beiden Runden benutzt; der Agent-Typ hat kein Write/Bash/Skill, deshalb lief das Review in einem general-purpose-Agenten mit dotnet-reviewer und dem Vorgehen des Code-Reviewers |

**Zuordnung und tatsächliches Laden:**

| Task | Zugeordnet | Geladen |
|---|---|---|
| T1, R-2, M-3 | dotnet-fundamentals, dotnet-tester | ja, vor dem ersten Schreiben von T1; bei R-2/M-3 weiter im Kontext |
| T2, T3, T4, R-1, R-3, M-1, M-2, S-1 | skill-creator | ja, vor dem ersten Schreiben von T2; weiter im Kontext |
| T5, M-4, N-1, N-2 | — (Doku) | n/a |

## 6. Tasks

### Task T1: Template `## Changes` und C#-Guard (Status: done)

- Liefert: AC-4, AC-12 (Template-Teil). Dateien: `spec.template.md`, `ShippedTemplatesTests.cs`.
- Tests: 1 von 1 grün (`ReadContent_SpecTemplate_HasSectionsInRuleOrderEndingWithChanges`); vorher rot, weil die Überschrift fehlte.
- Doku: Guidance-Kommentar des Abschnitts. Pakete: n/a — keine Änderung.
- Implementierung und Verifikation: selbst (direct). Build und Suite grün.
- Smoke-Check: `shade-kit init` in einem temporären Repository schreibt `.shadekit/templates/spec.md` mit `## Changes` als letztem Abschnitt.
- Abweichung: Phase 3/4 von dotnet-tester (Agent für fehlende Fälle) entfällt; die Ressource ist statisch und hat keine Codepfade.

### Task T2: `references/spec-rules.md` (Status: done)

- Liefert: die Regeln für AC-1, 3, 4, 6, 7, 9, 10, 12. Datei: `skills/shade-kit-refine/references/spec-rules.md` (neu).
- Inhalt:
  - Abschnitte und IDs; die Regeln „Meaning“ (D-21) und „Removing“ (D-9)
  - Security considerations (D-10, M-1, M-2)
  - Assumptions, Questions (D-22, S-1), Changes (D-7, D-15, D-23), Schreiben (D-14)
- Tests: Szenarien (T4).

### Task T3: `SKILL.md` überarbeitet (Status: done)

- Liefert: AC-2, 5, 8, 11, 13, 14, 15 und den Ablauf.
- Inhalt:
  - Schritt 2 `feature show` und Template-Stopp
  - Schritt 4 Fokus-Fehler mit kanonischer Frage-Tool-Klammer
  - Schritt 5 Interview
  - Schritt 6 Spec
  - Schritt 7 Review
  - Schritt 8 Changes
  - Schritt 9 Bericht; dazu Regeln
- Tests: Guards im Namespace Skills 159 von 159; `quick_validate.py` (PyYAML in einer venv) gültig für alle 19 Skills; Szenarien.

### Task T4: Szenarien S20–S23 (Status: done)

- Dateien: `tests/skill-scenarios/` (`SCENARIOS.md`, `generate_fixtures.py`, `checks/check_runs.py`, `RUNNER-PROTOCOL.md` Regel 1, `README.md`).
- Iterationen: 4, jeweils mit frischen Runner-Subagenten und einem neu erzeugten Arbeitsordner.
  - Iteration 1 → Klarstellungen (Changes-Form, Dismissed ohne Finding-IDs, `(spec)`-Verweis, Entwürfe statt Annahmen).
  - Iteration 2 fand eine echte Lücke: Der Runner hatte REQ-003 unter alter ID in eine andere Anforderung umgedeutet. Daraus wurde die Meaning-Regel (D-21).
  - Iteration 3: 92 von 92.
  - Iteration 4 nach Review-Runde 1, mit S23: 125 von 125.
- Fehlalarme des Checkers wurden behoben, sobald sie auftraten:
  - Die Entry-Rückgabe nennt die General Skills.
  - Ein Wiederherstellungsbefehl in einer Nachricht ist kein ausgeführter Befehl.
  - Statusspalte von `git status`.
  - Die Goal-Prüfung war zu streng.
  - Prosa „git commit … was not looked up“ ist kein Befehl.
  - Ein `None or None == False`-Fehler.
  - Die Neutralformel „whether to run another round“ ist keine Empfehlung.

### Task T5: Dokumentation (Status: done)

- `10-refine.md`: Status, **Decided**/**Implemented** mit D-IDs, Zeile „Changes“, Szenario-Läufe, Stand der ACs, offene Punkte.
- `README.md`: Status und Konsistenz-Log.
- 06-2 und CLAUDE.md: Dateien im eigenen Skill-Ordner; welche Guards sie lesen (M-4).
- 16, 17: offener Punkt zu „Changes“.

### Rework R-1 bis R-3 und die Findings aus Runde 1 (Status: done)

- **R-1:** Die Checks lesen nur noch ihren Teil der Ausgabe:
  - `refine_step(n)`
  - `interview_return` (Answered/Open plus verschiedene `Topic n/m`)
  - die Review-Fragen von Schritt 7
  - `refine_report` (Schritt 9 plus Abschlussnachricht), `RECOMMEND_ROUND`

  Die vier realistischen Verletzungen des Reviewers kamen als Mutationen ins Mutations-Tool und werden rot.
- **R-2:** Das Changes-Beispiel im Template folgt jetzt D-23; Test `ReadContent_SpecTemplate_GuidanceFollowsSpecRulesForChangesAndSecurity`, vorher rot.
- **R-3:** S23 mit `repo-s23` (Spec ohne Changes): 30 von 30.
- **M-1 bis M-4, S-1, N-1, N-2:** siehe T2, T3 und T5. M-3 ist durch denselben C#-Test abgesichert.

## 7. Review

| Runde | Bericht | Critical | Major | Minor | Suggestion/Nitpick | Rework | Minor gewählt | Deferred |
|---|---|---|---|---|---|---|---|---|
| 1 | docs/reviews/2026-11-26-feature-10-refine-uncommitted.md | 0 | 2 | 5 | 1 / 2 | R-1, R-2, R-3 (D-19) | alle (D-20) | — |
| 2 | docs/reviews/2026-11-26-feature-10-refine-uncommitted-r2.md | 0 | 0 | 3 | 3 / 2 | — | — | alle 8 (D-24) |

Konformität laut Teil B:
- Runde 1: 11 proven (executed) / 3 read-checked / 1 unproven.
- Runde 2: 15 proven (executed) / 0 read-checked / 0 unproven.

Reviewer: cc-ai-dotnet:dotnet-reviewer in einem general-purpose-Agenten (Abweichung von feature-dev:code-reviewer, siehe Abschnitt 5).

Wiederholte Findings: F1 aus Runde 2 gehört zur selben Klasse wie das Major-Finding aus Runde 1 (ein Check bleibt bei einer realistischen Verletzung grün), ist aber enger: eine verneinte Empfehlung.

## 8. Vor dem Commit prüfen

- `skills/shade-kit-refine/SKILL.md` und `references/spec-rules.md`: Diese Texte lesen die Agenten. Bitte einmal im Ganzen lesen, vor allem Schritt 7 (Review) und die Regeln „Meaning“, „Security considerations“ und „Changes“.
- **Template-Änderung:** `spec.template.md` hat einen neuen letzten Abschnitt und eine geänderte Security-Guidance. Neu initialisierte Repositories bekommen das; bestehende erst mit `upgrade` (Paket 18).
- **D-3, D-4, D-5, D-9:** Hier hast du nicht meinen Vorschlag genommen. Bitte prüfen, ob die Umsetzung deine Absicht trifft: die Changes-Notiz, die Referenzdatei, das Interview vor der Spec und dass der Agent als removed markieren darf.
- **Laden der Referenzdatei:** In GitHub Copilot CLI ist das **unverified**. Die Szenarien spielen die Agenten nur nach.
- **Deferred aus Runde 2:** F2 ist die einzige inhaltliche Regellücke. Schlägt der Security-Fokus in einer späteren Runde fehl, haben neue REQs keine offene Security-Frage.
- `CLAUDE.md`: neuer Konventionspunkt zu den Dateien im eigenen Skill-Ordner.
- `docs/reviews/`: zwei neue Berichte. Committen oder lokal lassen? Unter `docs/reviews/` gab es bisher keine Datei.

## 9. Offene Punkte

- **Deferred (D-24), Review-Runde 2:**
  - F1 verneinte Empfehlung im Checker
  - F2 Security-Fehlschlag in einer späteren Runde
  - F3 Regeltabelle in `10-refine.md`
  - F4 `refine_report` enthält den Begleittext des Runners
  - F5 AC-5-Zählung bei Interview-Abbruch
  - F6 C#-Test sichert den Regelsatz nicht
  - N3 Entscheidungsformat in `SKILL.md:46` und Template `:124`
  - N4 drei Zeilen mit 121 Zeichen
- **Aus den Szenarien, außerhalb von Paket 10** (in `10-refine.md` „Open points“):
  - 06-7: `ask_user` mit mehreren Fragen; abhängige Themen nach einem vertagten Blocker; Widerspruch bei einem Entwurf unter Frage
  - 06-8: Zitieren von `discussions/…`
  - 06-5: Die Frage „continue refine“ kommt vor dem Template-Check; „another round“ in der ersten Runde
  - 02: Der verwaltete CLAUDE.md-Abschnitt „never editing `.shadekit/features/` by hand“ wurde als Verbot der Markdown-Artefakte gelesen
- **Grenze zwischen Entwurf und Annahme:** Wo ein abgeleitetes Kriterium endet und eine Annahme beginnt, bleibt dem Agenten überlassen (offener Punkt in 10).
- **Fehlschlag in Runde 1:** Für einen Security-Fehlschlag in Runde 1 (M-1) gibt es kein Szenario; die Regel ist nur gelesen geprüft.
- **Fehlende Skills:** keine.

## 10. Schlussprüfung

- Build: `dotnet build` → 0 Warnungen, 0 Fehler.
- Ganze Suite: `dotnet test` → 6434 bestanden, 0 fehlgeschlagen, 8 übersprungen (vorhandene Plattform-Skips).
- Skill-Guards: 159 von 159; `quick_validate.py` gültig.
- Szenarien: `check_runs.py <scen4> --only S20,S21,S22,S23` → 125 von 125; Mutations-Tool → 28 von 28 rot. Arbeitsordner und Tools liegen außerhalb des Repositorys im Scratchpad der Session.
- Smoke-Check: `shade-kit init` schreibt das neue Template (T1).
- Geänderte Dateien: 18 (14 geändert, 4 neu: `references/spec-rules.md`, zwei Review-Berichte, dieser Bericht).

## 11. Skill-Invocation-Log

```text
Task T1 — Template ## Changes + C#-Guard
  [x] cc-ai-dotnet:dotnet-fundamentals — aufgerufen vor dem ersten Schreiben von ShippedTemplatesTests.cs
  [x] cc-ai-dotnet:dotnet-tester — aufgerufen vor dem ersten Schreiben von ShippedTemplatesTests.cs; Test zuerst rot
  [n/a] cc-ai-dotnet:dotnet-xmldocs — keine öffentliche API (nur Testcode)
  [n/a] cc-ai-dotnet:dotnet-nuget-manager — keine Abhängigkeit hinzugefügt, entfernt oder geändert
Task T2 — references/spec-rules.md
  [x] skill-creator:skill-creator — aufgerufen vor dem ersten Schreiben von spec-rules.md
Task T3 — SKILL.md
  [x] skill-creator:skill-creator — geladen seit T2, vor der ersten Änderung an SKILL.md; quick_validate.py gültig
Task T4 — Szenarien S20–S23
  [x] skill-creator:skill-creator — geladen seit T2; die Evals laufen über das Szenario-Verfahren des Repos (D-13) statt über den generischen Eval-Viewer
Task T5 — Dokumentation
  [n/a] — keine Skill-Zuordnung (nur Doku)
R-1 — Checker-Prüfungen eingrenzen
  [x] skill-creator:skill-creator — in dieser Session seit T2 geladen
R-2 / M-3 — Template-Beispiel und Security-Guidance
  [x] cc-ai-dotnet:dotnet-fundamentals, cc-ai-dotnet:dotnet-tester — in dieser Session seit T1 geladen; Test zuerst rot
R-3 — Szenario S23
  [x] skill-creator:skill-creator — in dieser Session seit T2 geladen
M-1, M-2, S-1 — Regeln
  [x] skill-creator:skill-creator — in dieser Session seit T2 geladen
M-4, N-1, N-2 — Doku, Docstring, Umbruch
  [n/a] — keine Skill-Zuordnung
```
