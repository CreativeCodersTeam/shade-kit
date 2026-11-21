> **Date:** 2026-11-20
> **Requirement:** docs/feature-packages/09-discuss-and-intake.md
> **Branch:** feature/09-discuss-and-intake
> **Mode:** direct
> **Status:** complete

# Paket 09 umgesetzt: `discuss`, `new`, Template `discussion.md` und `feature new --move-source`

## 1. Zusammenfassung

Die Skills `shade-kit-discuss` und `shade-kit-new` und das Template `discussion.md` sind ausgearbeitet. Damit Diskussionsnotizen in das Feature wandern, das aus ihnen entsteht, kam in der Engine die Option `shade-kit feature new --move-source` dazu (Paket 03). Alle 19 Skills stehen auf Kit-Version `0.9.0`, gesetzt über `next-version: v0.9.0` in `GitVersion.yml`. Von 7 Tasks sind 7 fertig, dazu R-1 und die Rework-Punkte aus zwei Review-Runden. Eine dritte Review-Runde hat der Nutzer ausdrücklich abgewählt (D-31).

Den Nachweis liefern 6471 grüne Tests und die Szenario-Läufe S10–S17 in `tests/skill-scenarios/`. **Es wurde nichts committet.** Als Nächstes `git diff` prüfen und committen, was übernommen werden soll.

## 2. Voraussetzungen

- Working Tree zu Beginn: sauber auf `feature/09-discuss-and-intake` (D-1, ohne Rückfrage festgehalten).
- Laufzeit-Skillliste: verfügbar (rund 90 Skills, 14 Agent-Typen).
- Größenhinweis: n/a, die Arbeit war nicht klein (mehr als 5 Dateien, Engine- und Skill-Änderungen).

## 3. Anforderung und Acceptance Criteria

Paket 09 umsetzen: `discuss` (freies Brainstorming, Notizen nach Template, ändert nie den Feature-Zustand) und `new` (legt ein Feature aus Text, Issue-URL, Datei oder Diskussionsnotizen über `shade-kit feature new` an), dazu das endgültige Template `discussion.md`. Die AC-Nummern entsprechen FP09-AC1 bis FP09-AC10 in Paket 09. AC5 wurde umformuliert (D-19), AC9 und AC10 sind neu.

| AC | Text (kurz) | Origin | Tests (file · name) | Result | Verdict |
|---|---|---|---|---|---|
| AC-1 | `discuss` ohne Feature schreibt nach `.shadekit/discussions/`, legt kein Feature an | explicit | checks/check_runs.py · S10, S12 | pass | read-checked — not executed (Skill-Text, Szenario-Checks) |
| AC-2 | `discuss` mit Feature schreibt in dessen `discussions/`, `feature.json` unverändert | explicit | check_runs.py · S11 | pass | read-checked — not executed |
| AC-3 | `new` mit Issue-URL: Issue-basierte ID, `request.md` wie von `feature new` geschrieben | explicit | FeatureStoreCreateTests · CreateAsync_IssueUrlWithTitle_CreatesIssueBasedFeatureFolderAndFeatureFile | pass | read-checked — not executed (Engine-Teil aus 03; ein erfolgreiches Issue-Lesen über den Skill hat kein Szenario) |
|  |  |  | FeatureStoreCreateTests · CreateAsync_TrackerUrlWithoutTitle_WritesIssueAsRequestAndUsesIssueTitle | pass |  |
|  |  |  | TrackerIssueMarkdownTests · (alle) | pass |  |
|  |  |  | check_runs.py · S14 | pass |  |
| AC-4 | `new` mit Datei oder Notizen übergibt die Datei, `request.md` mit ihrem Inhalt | explicit | FeatureStoreCreateTests · CreateAsync_FileSource_WritesFileContentAsRequestWithResolvedLineEnding | pass | read-checked — not executed |
|  |  |  | check_runs.py · S13 | pass |  |
| AC-5 | `new` führt keinen `git`-Befehl aus, legt keinen Branch an, nennt den vorgeschlagenen Branch | explicit (umformuliert, D-19) | check_runs.py · S13–S17 | pass | read-checked — not executed (Szenario-Checks) |
| AC-6 | nach `new` wird der Skill der nächsten Phase genannt, kein Skill gestartet | explicit | check_runs.py · S13, S16, S17 | pass | read-checked — not executed (die S17-Checks sind seit Runde 2 an Kopien als falsifizierbar belegt) |
|  |  |  | PhaseEngineNextTests · GetNext_PhaseThatCannotBeContinuedInProgress_ReturnsNoContinueButForwardAndReopen | pass |  |
| AC-7 | `new` mit aktivem Feature nennt es und legt erst nach Entscheidung des Menschen an | explicit | check_runs.py · S15, S16 | pass | read-checked — not executed |
| AC-8 | `discuss` nennt den Skill des nächsten Schritts und startet keinen | explicit | check_runs.py · S10–S12 | pass | read-checked — not executed |
| AC-9 | Notizen direkt in `.shadekit/discussions/` werden ins Feature verschoben, `source.value` zeigt den neuen Pfad, andere Quellen werden mit `--move-source` abgelehnt | implicit (aus D-5/D-6) | DiscussionNotesMoveTests · (alle) | pass | proven (executed: Runde 1: Unterordner-Prüfung entfernt → `Create_SourceNotDirectlyInDiscussions_…` und `CreateAsync_MoveSourceWithUnmovableSource_…` rot; `identity.Source` statt verschobener Quelle → 5 Store-Tests rot; `MoveSource` nicht weitergereicht → 1 Unit- und 3 E2E-Tests rot; Löschen vor dem Override → `CreateAsync_MoveSourceAndOverrideWriteFails_…` rot. Runde 2: Vorprüfung entfernt → `CreateAsync_MoveSourceWithUrl_…` rot; Vorprüfung vor die Git-Identität → `CreateAsync_MoveSourceWithTextAndGitIdentityMissing_…` rot; `isOriginalKept` falsch → `ExecuteAsync_PlainModeMovedSourceNotRemoved_…` rot) |
|  |  |  | FeatureStoreCreateMoveSourceTests · (alle 25) | pass |  |
|  |  |  | FeatureSourceFilesTests · (alle) | pass |  |
|  |  |  | FeatureNewCommandTests · ExecuteAsync_MoveSourceSet_PassesMoveSourceToStore, …_PlainModeMovedSource_WritesMovedLineAfterPaths, …_PlainModeMovedSourceNotRemoved_WritesCopiedLineAndWarning, …_PlainModeMovedSourceWithMarkupCharacters_WritesPathsVerbatim, …_JsonModeMovedSource_WritesMovedPathAsSourceValueWithoutExtraField, FeatureNewCommandOptions_MoveSource_HasOptionalLongOnlyOptionParameter | pass |  |
|  |  |  | FeatureCommandsEndToEndTests · FeatureNew_MoveSourceJson_ReturnsMovedSourceValueAndRemovesOriginal, FeatureNew_PlainMoveSource_WritesMovedLine, FeatureNew_MoveSourceWithTextSource_Returns33WithMoveSourceHintAndChangesNothing | pass |  |
|  |  |  | CliErrorMapperTests · Map_InvalidFeatureSourceWithSourceNotMovableProblem_ReturnsExitCode33WithMoveSourceHint, MapWarning_FeatureSourceNotRemoved_ReturnsSourceNotRemovedWarningNamingFileAndReason | pass |  |
| AC-10 | Notizen folgen `discussion.md` (kurzer Titel, Metadatenzeile, vier Abschnitte, Optionen als Unterabschnitte, keine Platzhalter) | implicit | check_runs.py · S10, S11, S12 | pass | read-checked — not executed |

Zählung: 1 proven (executed), 9 read-checked, 0 unproven.

## 4. Entscheidungen

| ID | Punkt | Entscheidung | Origin | Runde |
|---|---|---|---|---|
| D-1 | Working Tree | sauber, Branch `feature/09-discuss-and-intake` | n/a (sauber) | — |
| D-2 | 0 / G-3 `done`-Feature | `discuss` schreibt dann auf Top-Level | recommended accepted | 1 |
| D-3 | 0 / G-7 Titel aus Notizen | Template-Überschrift `# {{topic}}` | recommended accepted | 1 |
| D-4 | 0 / G-11 Proposed-Regeln | Active-Feature-Check Decided (Branch-Angebot später durch D-18 ersetzt) | recommended accepted | 1 |
| D-5 | 0 / G-8 Notizen nach `new` | in `discussions/` des neuen Features verschieben, `source.value` anpassen | user's own | 1 |
| D-6 | 0 Engine-Form | explizite Option, nur für eine Datei direkt in `.shadekit/discussions/` | user's own | 1 |
| D-7 | 4 Ausgabe | kein neues JSON-Feld; Plain-Zeile „Moved …“ | recommended accepted | 1 |
| D-8 | 1 Struktur | in `FeatureStore.CreateAsync`, atomar | recommended accepted | 1 |
| D-9 | 2 Architektur | kopieren, Original nach Erfolg löschen; Fehlschlag → Warnung | recommended accepted | 1 |
| D-10 | 3 Naming | `--move-source` / `NewFeatureRequest.MoveSource` | recommended accepted | 1 |
| D-11 | 3 / G-1 Dateiname | lokale Zeit, Slug-Regeln der Feature-IDs | recommended accepted | 1 |
| D-12 | 4 / G-9 Profil | „Repository-Default“ → kein `--profile` | recommended accepted | 1 |
| D-13 | 4 / G-14 Freitext | ein Argument, shell-sicher gequotet | recommended accepted | 1 |
| D-14 | 4 / G-12 Template | `### <option>` mit Pros/Cons, Datum `yyyy-mm-dd`, git-Usernamen | recommended accepted | 1 |
| D-15 | 5 Fehler | Exit 33 `invalid-source` (`SourceNotMovable`), Warnung `source-not-removed` | recommended accepted | 1 |
| D-16 | 5 / G-2 Kollision | Suffix `-2`, `-3` … | recommended accepted | 1 |
| D-17 | 5 / G-5 kein Template | ohne Template schreiben und das sagen | alternative chosen | 1 |
| D-18 | 5 / G-10 Branch | kein Branch-Angebot, kein git-Befehl | user's own | 1 |
| D-19 | 0 FP09-AC5 | umformuliert | recommended accepted | 1 |
| D-20 | 0 Branch-Info | `suggestedBranch` als Info-Zeile | recommended accepted | 1 |
| D-21 | 6 Tests Engine | Core + CLI + End-to-End + Mapper | recommended accepted | 1 |
| D-22 | 6 / G-13 Skill-Nachweis | Szenarien S10+ in `tests/skill-scenarios` | recommended accepted | 1 |
| D-23 | 7 / G-4 Wiederholtes Schreiben | eine Datei pro Lauf, wird neu geschrieben | recommended accepted | 1 |
| D-24 | 7 / G-6 Ordner, Line-Ending | Skill legt den Ordner an; Line-Ending des Templates, sonst des Betriebssystems | user's own (Ergänzung) | 1 |
| — | 7 Persistenz | `feature.json` bleibt Format v1 | pre-answered — `CLAUDE.md`: "`feature.json` stays format version 1" | 1 |
| — | 8 Abhängigkeiten | n/a — kein Paket hinzugefügt oder geändert | n/a | 1 |
| D-25 | Runde 2 Kit-Version | `0.9.0` | alternative chosen | 2 |
| D-26 | Gate 2 Guard | `next-version` in `GitVersion.yml` | alternative chosen | Gate 2 |
| D-27 | Gate 3 Tasks | T1–T7 bestätigt | confirmed | Gate 3 |
| D-28 | Gate 3 Modus | direct | recommended accepted | Gate 3 |
| D-29 | Gate 5 R1 | R-1 über `feature show`, keine Profil→Phase-Tabelle | recommended accepted | Gate 5.1 |
| D-30 | Gate 5 R1 | Minor 1, 3, 5, 6 und alle Suggestions/Nitpicks beheben; Minor 2 und 4 zurückgestellt | user's own selection | Gate 5.1 |
| D-31 | Gate 5 R2 | ausgewählte Befunde beheben, **kein weiteres Review** | user's own | Gate 5.2 |
| D-32 | Gate 5 R2 | alle 4 Minor sowie alle Suggestions und Nitpicks beheben | user's own selection | Gate 5.2 |
| D-33 | Gate 5 R2 | verschobene Notizen behalten `Feature: none` (Variante bestätigt) | recommended accepted | Gate 5.2 |

Waivers: D-31, die Review-Runde 3 auf das Delta von Rework-Runde 2 entfällt. Lücke: Die Änderungen der Runde 2 sind nur durch Tests, Mutationen an Kopien, einen erneuten S17-Lauf und meine eigene Prüfung belegt, nicht durch einen unabhängigen Reviewer. Betroffen sind `FeatureSourceFiles`, die Checker-Regexe, Schritt 8/9 von `new`, die Reihenfolge in 03 und der `GitVersion.yml`-Kommentar.

## 5. Skills

**Laufzeit-Discovery:** rund 90 Skills und 14 Agent-Typen; Kandidaten nach Abgleich: dotnet-fundamentals, dotnet-tester, dotnet-xmldocs, skill-creator, code-design, documentation-and-adrs, dotnet-nuget-manager, dotnet-reviewer und der Agent agent-skills:code-reviewer.

| Skill / agent | Category | Proposed use | Outcome |
|---|---|---|---|
| cc-ai-dotnet:dotnet-fundamentals | knowledge | Engine-Task | kept |
| cc-ai-dotnet:dotnet-tester | tester | Engine-Tests | kept |
| cc-ai-dotnet:dotnet-xmldocs | documentation | XML-Doku | kept |
| skill-creator:skill-creator | skill texts | Skill-Texte, Template | kept |
| cc-ai-dev:code-design | design | Design-Pass | added (optional, vom Nutzer gewählt) |
| agent-skills:documentation-and-adrs | documentation | Paket-Doku | not selected (Gate 2) |
| cc-ai-dotnet:dotnet-nuget-manager | tooling | — | n/a (keine Paketänderung) |
| cc-ai-dotnet:dotnet-dev, cc-ai-dev:implement-dev-plan, agent-skills:build, feature-dev | workflow | excluded — nested workflow | stayed excluded |
| cc-ai-dotnet:dotnet-reviewer + Agent agent-skills:code-reviewer | reviewer | Review | in beiden Runden benutzt |

| Task | Mapped skills | Loaded |
|---|---|---|
| T1 Engine | dotnet-fundamentals, code-design, dotnet-tester, dotnet-xmldocs | ja, vor dem ersten Schreiben |
| T2–T6 | skill-creator | ja, vor dem ersten Schreiben von T2 |
| T7 Doku | — | n/a |
| Rework R1/R2 | wie T1 bzw. T2 | in dieser Session schon geladen, nicht erneut aufgerufen |

## 6. Tasks

### Task T1: Engine `feature new --move-source` (Status: done)
- Liefert AC-4 und AC-9.
- **Dateien Core:** `Features/DiscussionNotesMove.cs` (neu), `Features/FeatureSourceFiles.cs` (neu, R2), `Features/Warnings/FeatureSourceNotRemovedWarning.cs` (neu), `NewFeatureRequest.cs`, `NewFeatureResult.cs`, `FeatureStore.cs`, `FeatureIdFactory.cs` (R2), `Exceptions/FeatureSourceProblem.cs`, `Store/KitStorePaths.cs`
- **Dateien CLI:** `FeatureNewCommand(Options).cs`, `CliErrorMapper.cs`, `ShadeKitCliWarningCodes.cs`
- **Tests:** neue und erweiterte Tests in 7 Testdateien, alle grün
- **Doku:** 01, 03, `CLAUDE.md`. **Pakete:** n/a, keine Änderung.
- **Verifikation:** selbst (direct), Build und Suite grün, dotnet-tester Phase 3 per Agent (8 Fälle ergänzt). Mutation: Löschen vor dem Override → Test rot.
- **Smoke-Check:** `init` → Notizen → `feature new --move-source --json` → ok, `source.value` zeigt den neuen Pfad, Original weg. Text mit `--move-source` → 33. URL mit `--move-source` → 33 in 0,13 s ohne HTTP.
- **Abweichung:** eigene Testklasse `FeatureStoreCreateMoveSourceTests` statt Anhängen an die 2330 Zeilen von `FeatureStoreCreateTests`.

### Task T2: Template `discussion.md` (Status: done)
- Liefert AC-10. Dateien: `Templates/Resources/discussion.template.md`, 02. Tests: `ShippedTemplatesTests` grün, Inhalt über S10–S12 geprüft.

### Task T3: Kit-Version 0.9.0 (Status: done)
- **Dateien:** 19 × `SKILL.md`, `GitVersion.yml`, 06-2.
- **Nachweis:** `SkillKitVersionTests` grün; der gebaute `shade-kit.dll` meldet `0.9.0-09-discuss-and-intake.3`.
- **Abweichung:** `next-version` steht als `v0.9.0` statt `0.9.0`. GitVersion 6.8.2 parst den Wert mit `tag-prefix` und lehnt die Form ohne `v` ab („Failed to parse 0.9.0“), **FACT**.

### Task T4: Skill `shade-kit-discuss` (Status: done)
- Liefert AC-1, AC-2, AC-8, AC-10. Nachweis über S10–S12, Iteration 2: 72 von 72.

### Task T5: Skill `shade-kit-new` (Status: done)
- Liefert AC-3 bis AC-7. Nachweis über S13–S17, nach R-1: 84 von 84; S17 in Iteration 4: 18 von 18.

### Task T6: Szenarien S10–S17 (Status: done)
- **Dateien:** `SCENARIOS.md`, `RUNNER-PROTOCOL.md`, `generate_fixtures.py`, `checks/check_runs.py`, `README.md`.
- **Läufe:** Iteration 1: 130 von 133. Iteration 2: 133 von 133. Iteration 3 (nach R-1): 83 von 84, nach dem Checker-Fix 84 von 84. Iteration 4 (nur S17): 18 von 18.
- **Abweichung:** S1–S9 wurden nicht neu ausgeführt, die zugehörigen Skills sind bis auf die Versionsnummer unverändert.

### Task T7: Doku (Status: done)
- Paket 09 mit allen Entscheidungen, AC1–AC10 mit Nachweisen und offenen Punkten; 03, 06-2, `README.md` mit Statuszeile, Consistency Log und offenen Punkten; `CLAUDE.md`.

## 7. Review

| Round | Report | Critical | Major | Minor | Suggestion | Nitpick | Rework done | Selected | Deferred |
|---|---|---|---|---|---|---|---|---|---|
| 1 | docs/reviews/2026-11-20-feature-09-discuss-and-intake-uncommitted.md | 0 | 1 | 6 | 4 | 5 | R-1 | M1, M3, M5, M6, alle S und N | M2, M4 (D-30) |
| 2 | docs/reviews/2026-11-20-feature-09-discuss-and-intake-uncommitted-round2.md | 0 | 0 | 4 | 3 | 4 | — | alle (D-32) | — |

Konformität: Runde 1: 1 proven (executed), 9 read-checked, 0 unproven. Runde 2 (AC5, AC6, AC9): AC9 proven (executed), AC5 und AC6 read-checked.

**Reviewer:** `cc-ai-dotnet:dotnet-reviewer` im Agent `agent-skills:code-reviewer`.

**Wiederholte Befunde:** Runde-1-Nitpick N1 (`--json`-Check für S16) war in meiner Umsetzung wirkungslos und kam in Runde 2 als Minor 2 zurück. Behoben und an einer Kopie belegt.

**Ablauf:** In Runde 1 lief versehentlich ein zweiter Review-Agent parallel. Ich habe ihn nach dem Abschluss des ersten gestoppt und sein Skeleton gelöscht. Der Bericht von Runde 1 nennt in seinem Pfadhinweis noch `-2`; den habe ich an den Standardpfad verschoben.

## 8. Vor dem Commit prüfen

- `GitVersion.yml`: `next-version: v0.9.0` ist die einzige Version in der Datei. Der Kommentar wurde umgeschrieben (D-26 gegen die bisherige Regel „holds no version“).
- `skills/*/SKILL.md`: alle 19 Skills auf `0.9.0`. Ein Engine-Build `0.8.x` lehnt diese Skills beim Handshake ab.
- `src/.../FeatureIdFactory.cs`: Die Datei-Auflösung steckt jetzt in `FeatureSourceFiles` (R2, ohne unabhängiges Review, D-31). Das Verhalten ist gleich, abgesichert durch die bestehenden 03-Tests.
- `src/.../FeatureStore.cs`: Mit `--move-source` kommt die Vorprüfung vor dem Laden der Quelle. Damit gewinnt Exit 33 über 34 bei einer Web-URL ohne `--title` (03, Order of work).
- `skills/shade-kit-new/SKILL.md`, Schritte 6, 8 und 9: Im Branch-Fall läuft `feature show`, und der Skill erklärt, dass die Closing-Message das andere Feature zeigt.
- `CLAUDE.md`: Es gibt jetzt eine zweite Ausnahme bei den Templates (`discuss`), die Zeile zu `--move-source` und die Regel zu `next-version`.
- `tests/skill-scenarios/checks/check_runs.py`: Die Checks für S10–S17 sind neu, Schritt 0 des Runners fällt aus dem git-Check heraus, und `--only` ist neu.
- Zurückgestellt (D-30): Minor 2 (eine Datei wird nicht geprüft, wenn das Profil genannt war) und Minor 4 (keine `[Trait("Requirement", …)]` an den neuen Tests).

## 9. Offene Punkte

- **Zurückgestellt:** Runde-1-Minor 2 (Skill `new` Schritt 7) und Minor 4 (Requirement-Traits), beide nach D-30.
- **06-3 `next-steps`:** Die Skills geben eine feste Zeile für das Geschehene vor, laut 06-3 schreibt `next-steps` sie aber selbst. Die Fallback-Zeile bietet nach `invalid-source` keinen erneuten Aufruf an. Bei `done` wird `/shade-kit-drop` genannt.
- **06-7 Interview:** Für die Abschlussfrage im `open`-Modus fehlt eine Header-Regel. Wie weit eigene Topics gehen, ist offen, ebenso der Umgang mit abgelehnten Topic-Vorschlägen.
- **Szenarien fehlen** für ein erfolgreiches Issue-Lesen über `new` (der Generator hat kein `gh`), einen Versions-Mismatch in `discuss` und `source-not-removed` über den Skill.
- **Unverified:** GitVersion ignoriert `next-version` ab einem Tag `≥ 0.9.0`. Das stammt aus der Doku und wurde hier nicht beobachtet.
- **Paket 18:** Repositories, die vor dem 2026-11-20 initialisiert wurden, behalten das alte Template-Skeleton bis zu `upgrade`.

## 10. Abschließende Verifikation

- **Build:** `dotnet build` → 0 Warnungen, 0 Fehler
- **Suite:** `dotnet test` → 6471 bestanden, 0 fehlgeschlagen, 8 übersprungen. Die übersprungenen Tests sind die bestehenden Windows- und Root-Fälle; dass die neuen Tests gelaufen sind, ist per TRX geprüft.
- **Skill-Frontmatter:** `uv run --with pyyaml python3 quick_validate.py` → 19 von 19 mit Exit 0. Mein erster Zwischenstand „19 von 19“ war falsch, weil PyYAML fehlte; das habe ich korrigiert.
- **Szenarien:** S13–S17 (Iteration 3) 84 von 84 mit dem endgültigen Checker, S17 (Iteration 4) 18 von 18, S10–S12 (Iteration 2) 72 von 72.
- **Smoke-Check:** Die gebaute CLI wurde in einem temporären git-Repo geprüft (siehe T1).
- **Geänderte Dateien:** 48 geänderte getrackte Dateien und 8 neue (`DiscussionNotesMove.cs`, `FeatureSourceFiles.cs`, `FeatureSourceNotRemovedWarning.cs`, 3 Testdateien, 2 Review-Berichte), dazu dieser Bericht.

## 11. Skill-Invocation Log

Task T1: Engine `--move-source`
  [x] cc-ai-dotnet:dotnet-fundamentals: aufgerufen vor dem ersten Schreiben von `FeatureStoreTestContext.cs` (dieselbe Runde wie die drei folgenden)
  [x] cc-ai-dev:code-design: aufgerufen vor dem ersten Schreiben von T1; Design-Pass und Gegenprüfung, siehe Design-Notizen unten
  [x] cc-ai-dotnet:dotnet-tester: aufgerufen vor dem ersten Schreiben von T1; Phase 3 als eigener Agent, Phase 4 mit 8 ergänzten Tests
  [x] cc-ai-dotnet:dotnet-xmldocs: aufgerufen vor dem ersten Schreiben von T1; XML-Doku zu `MoveSource`, `MovedSourceFrom`, `SourceNotMovable`, der Warnung, `DiscussionNotesMove` und `FeatureSourceFiles`
  [n/a] cc-ai-dotnet:dotnet-nuget-manager: keine Abhängigkeit hinzugefügt, entfernt oder geändert
Task T2: Template
  [x] skill-creator:skill-creator: aufgerufen vor dem ersten Schreiben von `discussion.template.md`
Task T3: Version 0.9.0
  [x] skill-creator:skill-creator: geladen seit T2, vor der ersten Änderung an `SKILL.md`
Task T4 und T5: Skills
  [x] skill-creator:skill-creator: geladen seit T2; `quick_validate.py` mit PyYAML: 19 von 19
Task T6: Szenarien
  [x] skill-creator:skill-creator: geladen seit T2; die Evals laufen über das Szenario-Verfahren des Repos (D-22) statt über den generischen Eval-Viewer
Task T7: Doku
  [n/a] keinem Skill zugeordnet (documentation-and-adrs an Gate 2 nicht gewählt)
R-1 und Rework-Runde 1/2 (C#)
  [x] dotnet-fundamentals, code-design, dotnet-tester, dotnet-xmldocs: in dieser Session bei T1 geladen und weiter im Kontext, nicht erneut aufgerufen
R-1 und Rework-Runde 1/2 (Skill-Texte, Szenarien)
  [x] skill-creator: in dieser Session bei T2 geladen, nicht erneut aufgerufen

### Design-Notizen (code-design)

| # | Decision | Principle | Alternative rejected, why |
|---|---|---|---|
| 1 | `DiscussionNotesMove` als eigene interne Einheit; `FeatureStore` orchestriert nur | SRP: die Verschiebe-Regel ändert sich mit 09, die Erzeugung mit 03; `FeatureStore` hatte schon rund 410 Zeilen und 5 Abhängigkeiten | private Methode in `FeatureStore`: noch eine Regel im vollen Host |
| 2 | kein Interface für `DiscussionNotesMove` | YAGNI: reine Logik, direkt getestet | `IDiscussionNotesMove`: spekulative Allgemeinheit |
| 3 | `FeatureSourceFiles` für Factory und Vorprüfung (R2) | DRY: ein Wissen (welche Quelle eine Datei ist), zuvor zweimal vorhanden | zwei Kopien mit Kommentar „as the factory does“: können still auseinanderlaufen |
| 4 | Kopie ins Staging, Löschen nach dem Override | Robustheit: jeder Rollback lässt die Notizen an Ort und Stelle | `MoveFile` ins Staging: ein fehlgeschlagenes Zurückverschieben könnte die Notizen verlieren |

- **Refactor-Pass:** Collection Expression in `FeatureStore`, Kommentar zu `FeatureRelativePath`, `CreateNotMovableException` herausgezogen.
- **Bewusst gelassen:** `FeatureStore.CreateAsync` ist lang (Bestand von 03); ein Umbau außerhalb des Scopes wäre ein Fall für den Skill `refactor`.
- **Defaults ohne Bestätigung durch den Nutzer:** keine. Die Notiz-Variante war zuerst Proposed und ist seit D-33 bestätigt.
