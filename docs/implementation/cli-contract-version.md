> **Date:** 2026-11-22
> **Requirement:** Recommendation of the analysis of 2026-11-21 ("one contract number decouples CLI and skills"); the report was removed on 2026-11-24, its content is in [06-1 Background](../feature-packages/06-1-engine-version-check.md#background-why-a-cli-contract)
> **Branch:** feature/rework-versioning-branch-mgmt
> **Mode:** sub-agents (sequential, ein Implementer und ein Verifier pro Task)
> **Status:** complete — mit zurückgestellten Review-Befunden (Abschnitt 9)

# CLI-Vertragsversion statt Lockstep-Versionshandshake zwischen Skills und Engine

## 1. Summary

Der Handshake zwischen Skills und Engine prüft nicht mehr die Produktversion. Bisher galt: `--expect <x.y.z>`, major.minor musste gleich sein, und Prerelease-Engines wurden durchgewunken. Jetzt prüft er eine ganzzahlige CLI-Vertragsversion mit Fenster. `CliContract.Current` und `CliContract.MinSupported` sind beide 1. Die Skills rufen `shade-kit version --json --expect-contract 1` auf, die Engine antwortet mit Exit 54 `engine-outdated` oder 55 `skills-outdated`. Entfernt sind `--expect`, der alte Checker, die Prerelease-Nachsicht, `metadata.version` in allen 19 Skills und der an GitVersion gekoppelte Guard. Neu ist `SkillContractTests`.

Alle 5 Tasks sind erledigt. Task 4 war nach zwei FAIL-Verifikationen vorübergehend blockiert (D-23). Die Lücke wurde in den Review-Runden 1 bis 3 geschlossen. Es gab 4 Review-Runden und 4 Rework-Tasks (R-1 bis R-4). Am Ende gibt es keinen Befund der Schwere Critical, Major oder Minor mehr.

**Nichts ist committet.** Bitte `git diff` prüfen und übernehmen, was du akzeptierst.

## 2. Preconditions

- **Arbeitsbaum beim Start:** Er war dirty, mit dem gestagten Rename der Berichtsdatei auf einen englischen Dateinamen (Bericht am 2026-11-24 entfernt, Inhalt in [06-1 Background](../feature-packages/06-1-engine-version-check.md#background-why-a-cli-contract)). Nach D-1 hast du zuerst committet (`6b675f8`), der Baum war danach sauber.
- **Skill-Liste der Laufzeit:** verfügbar, mit rund 75 Skills und 13 Agent-Typen.
- **Größenhinweis:** `n/a`. Die Arbeit war nicht klein: 5 Tasks und mehr als 70 Dateien.

## 3. Requirement and Acceptance Criteria

Die Kompatibilität von Skills und Engine hängt nicht mehr an der Produkt-SemVer, sondern an einer eigenen ganzzahligen Vertragsnummer. Die Engine erklärt ein Fenster `MinSupported ≤ R ≤ Current`. Jeder Handshake-Skill nennt die eine Vertragsnummer R, für die er geschrieben ist. Die Produktversion bleibt bei GitVersion. Die Skills tragen keine eigene Version mehr. Verteilt wird weiter über den Tag `v<engine>` bzw. über `main`.

Die Verdikte stammen aus Teil B der Reviews, Stand Runde 4. `proven` heißt, ein Test wurde gegen eine gebrochene Version rot gesehen. `read-checked` heißt, die Tests wurden nur gelesen. Ergebnis: **10 ACs proven (executed), 2 read-checked (AC-11, AC-12), 0 unproven**. AC-10 ist im Checker-Teil proven, im Generator-Teil read-checked. AC-9 ist gestrichen.

| AC | Text | Origin | Tests (file · name) | Result | Verdict |
|---|---|---|---|---|---|
| AC-1 | `CliContract` in `Core/Versions/` mit `Current = MinSupported = 1` | explicit | Core.Tests/Versions/CliContractTests · Window_Constants_AreCurrentAndMinSupported | pass | proven (executed: `Current = 2` → rot) |
| AC-2 | `version --json` meldet `data.contract` | explicit | Cli.Tests VersionCommandTests · ExecuteAsync_JsonMode_WritesContractWithCurrentAndMinSupported | pass | proven (executed: M15 Feld ignoriert; M7b/M7c Werte vertauscht bzw. zusammengelegt; M16 Fix zurückgenommen → rot) |
|  |  |  | VersionCommandTests · ExecuteAsync_PlainMode_WritesContractLineAfterVersionLine | pass |  |
|  |  |  | VersionCommandTests · ExecuteAsync_JsonModeWithWiderWindow_WritesCurrentAndMinSupportedOfWindow (R-1) | pass |  |
|  |  |  | VersionCommandTests · ExecuteAsync_PlainModeWithWiderWindow_WritesContractLineOfWindow (R-1) | pass |  |
|  |  |  | ProgramTests · RunAsync_VersionJson_WritesContract | pass |  |
| AC-3 | Fenster: im Fenster ok; `R > Current` → Engine veraltet; `R < MinSupported` → Skills veraltet (Verweis auf `integrations install`); keine Prerelease-Nachsicht | explicit | CliContractCheckerTests · Check_RequestedWithinWindow_Succeeds, Check_RequestedAboveCurrent_ThrowsEngineContractOutdatedExceptionWithContracts, Check_RequestedBelowMinSupported_ThrowsSkillsContractOutdatedExceptionWithContracts, Check_RequestedZeroWithDefaultWindow_ThrowsSkillsContractOutdatedException, Check_NegativeRequested_ThrowsArgumentOutOfRangeException, Ctor_NullWindow_ThrowsArgumentNullException, Window_Constructed_ReturnsWindowOfConstructor | pass | proven (executed: `>`→`>=`, `<`→`<=`, Hinweistext B vertauscht → rot) |
|  |  |  | CliContractWindowTests (3); ContractExceptionsTests (2); ShadeKitCoreServiceCollectionExtensionsTests · …RegistersCliContractCheckerWithCliContractWindow u. a. | pass |  |
|  |  |  | VersionCommandTests · ExecuteAsync_JsonModeExpectContractInWindow_WritesSuccess, ExecuteAsync_JsonModeEngineOutdated_WritesEngineOutdatedEnvelopeAndAbortsWith54, ExecuteAsync_JsonModeSkillsOutdated_WritesSkillsOutdatedEnvelopeAndAbortsWith55, ExecuteAsync_PlainModeEngineOutdated_AbortsWith54AndMessageWithoutOutput, ExecuteAsync_PlainModeSkillsOutdated_AbortsWith55AndMessageWithoutOutput | pass |  |
|  |  |  | CliErrorMapperTests · Map_EngineContractOutdated_Returns54WithUpdateEngineMessage, Map_SkillsContractOutdated_Returns55WithIntegrationsInstallMessage | pass |  |
|  |  |  | ProgramTests · RunAsync_VersionJsonExpectContract999_WritesEngineOutdatedAndReturns54, RunAsync_VersionJsonExpectContract0_WritesSkillsOutdatedAndReturns55, RunAsync_VersionJsonExpectContract1_WritesSuccessWithExpectedContract | pass |  |
| AC-4 | Fehlender Wert oder keine Ganzzahl → Exit 2 beim Parsen | explicit | ExpectedContractOptionConverterTests · Convert_Null_ThrowsExpectedContractInvalidException, Convert_InvalidText_ThrowsExpectedContractInvalidExceptionWithValue, Convert_ValidText_ReturnsInt | pass | proven (executed: `null` durchgelassen; Regex akzeptiert „01“ → rot) |
|  |  |  | CliErrorMapperTests · MapArgumentError_ExpectedContractInvalidWithoutValue_ReturnsInvalidArgumentsWithGotNone, MapArgumentError_ExpectedContractInvalidWithValue_ReturnsInvalidArgumentsNamingValue | pass |  |
|  |  |  | ProgramTests · RunAsync_VersionJsonExpectContractNotANumber_WritesInvalidArgumentsAndReturns2, RunAsync_VersionExpectContractWithoutValue_WritesInvalidArgumentsAndReturns2 | pass |  |
| AC-5 | `--expect`, `EngineVersionChecker`, Prerelease-Warnung und deren Codes entfernt | explicit | ShadeKitCliExitCodesTests · ExitCodes_Contract_HaveStableValues, ExitCodes_AllConstants_DoNotDefineRemovedVersionMismatch50 | pass | proven (executed: `VersionMismatch = 50` wieder eingefügt; `--expect 0.9.0` in einer Befehlsliste → rot) |
|  |  |  | ShadeKitCliErrorCodesTests · ForExitCode_ContractExitCodes_ReturnsContractErrorCodes, ForExitCode_UndefinedExitCode_ThrowsArgumentOutOfRangeException(50) | pass |  |
|  |  |  | ShadeKitCliWarningCodesTests · WarningCodes_AllConstants_DoNotContainRemovedPrereleaseVersionMismatch | pass |  |
|  |  |  | VersionCommandOptionsTests · Options_ExpectOption_DoesNotExist, Options_ExpectContract_HasConverterAndLongName; ProgramTests · RunAsync_VersionJsonExpectOldOption_IsNotChecked | pass |  |
| AC-6 | Mit `--expect-contract` keine Abfrage der Integrationen | explicit (im Bericht „optional“) | VersionCommandTests · ExecuteAsync_ExpectContract_DoesNotReadIntegrationsAndOmitsField, ExecuteAsync_ContractMismatch_DoesNotReadIntegrations, ExecuteAsync_PlainModeExpectContract_WritesNoIntegrationLines, ExecuteAsync_JsonModeWithoutExpectContract_WritesIntegrations; ProgramTests · RunAsync_VersionJsonExpectContract1_… | pass | proven (executed: Integrationsabfrage im Handshake-Pfad ergänzt → rot) |
| AC-7 | 5 Handshakes und 2 Befehlslisten im neuen Wortlaut; Reaktion auf die neuen Codes; Ablehnungszeilen in `shade-kit-next-steps`; `metadata.version` in allen 19 Skills entfernt | explicit | Core.Tests/Skills/SkillContractTests · SkillFiles_ShippedSkills_NameTheCurrentCliContractEverywhere; ShippedSkillsTests, SkillAgentWordingTests, SingleSkillSourceTests; Szenarien S18/S19 | pass | proven (executed: Handshake auf 2, Befehlsliste auf `--expect 0.9.0`, `metadata.version` wieder eingefügt → rot) |
| AC-8 | `SkillContractTests` statt `SkillKitVersionTests` | explicit | SkillContractTests (Real-Tree-Test plus 19 Fixture-Tests bzw. Theories) | pass | proven (executed: `Current = 2`; Handshake geändert → rot) |
| AC-9 | Snapshot-Test der Vertragsoberfläche | explicit („Vorschlag“) | — | — | dropped (D-12); offener Punkt in `docs/feature-packages/README.md` |
| AC-10 | Szenario-Tooling: R aus dem Handshake-Text, erstes Kommando `--expect-contract <R>`, Fixtures für „Engine zu alt“ und „Skills zu alt“ | explicit | `generate_fixtures.py`; `checks/check_runs.py --only S18,S19` auf den echten Runs (22/22) und auf handgeschriebenen Varianten | pass | Checker: proven (executed: MD1–MD9 → jede Variante rot). Generator: read-checked — not executed |
| AC-11 | `GitVersion.yml`: Kommentar beschreibt nur die Produktrolle, `next-version` bleibt | explicit | Diff (nur Kommentar), Build grün | pass | read-checked — not executed (nur Doku) |
| AC-12 | Lockstep-Formulierungen in CLAUDE.md, Feature-Paketen, Konzept §9.6 und Konsistenzlog ersetzt | explicit | grep, Stichproben gegen den Code | pass | read-checked — not executed (nur Doku) |
| AC-13 | E2E-Tests für den Ablehnungspfad ohne Tag und ohne Release-Build | implicit | ProgramTests · …ExpectContract999…, RunAsync_VersionPlainExpectContract999_…, …ExpectContract0…; IntegrationsCommandsEndToEndTests · Version_ExpectContractMismatchWithInstalledIntegration_Exits54WithoutAskingClaude | pass | proven (executed: `Check(Current)` statt `Check(R)` → rot) |
| AC-14 | `data.expectedContract` gibt den geprüften Wert zurück | implicit | VersionCommandTests · ExecuteAsync_JsonModeExpectContract_WritesExpectedContract, ExecuteAsync_JsonModeWithoutExpectContract_OmitsExpectedContract, ExecuteAsync_JsonModeExpectContractWithRealChecker_SucceedsForEveryContractInWindow | pass | proven (executed: `Math.Min(R, Current)` → rot) |

## 4. Decisions

| ID | Point | Decision | Origin | Round |
|---|---|---|---|---|
| D-1 | Arbeitsbaum | Rename zuerst committen, danach sauber | recommended accepted | — |
| D-2 | G-4 Integrationen überspringen | Bei `--expect-contract` keine Abfrage, `data.integrations` fehlt; Plain-Modus und `version` ohne Option listen weiter | recommended accepted | 1 |
| D-3 | G-5 `metadata.version` | Aus allen 19 Skills entfernen, Regel aus CLAUDE.md und 06-2 streichen | recommended accepted | 1 |
| D-4 | G-10 research notes of the analysis | Unangetastet lassen (removed on 2026-11-24; the facts they supported are in [06-1 Background](../feature-packages/06-1-engine-version-check.md#background-why-a-cli-contract)) | recommended accepted | 1 |
| D-5 | 1 Struktur | Wie im Bericht: Core/Versions(+Exceptions), Cli/Commands/Version, Core.Tests/Skills | recommended accepted | 1 |
| D-6 | G-6 Doku | Feature-Pakete vor Ort umschreiben, Ersetztes datiert behalten, ein Eintrag im Konsistenzlog | recommended accepted | 1 |
| D-7 | 2 Architektur | Statisches `CliContract`; `ICliContractChecker`/`CliContractChecker(CliContractWindow)` als Singleton mit `CliContract.Window` | recommended accepted | 1 |
| D-8 | 3 Benennung | `EngineContractOutdatedException` und `SkillsContractOutdatedException`; `ExpectedContractOptionConverter` mit einer `ExpectedContractInvalidException` | recommended accepted | 1 |
| D-9 | 4 API-Vertrag / G-2 / G-8 | `data.contract {current, minSupported}`, `data.expectedContract`; Exit 54 `engine-outdated`, 55 `skills-outdated`; 50 und `version-mismatch` entfallen; Plain-Zeile `CLI contract 1 (supported: 1 to 1)` | recommended accepted | 1 |
| D-10 | G-1 Übergang alte Engine | Risiko akzeptiert: Skills prüfen `data.contract` nicht | user's own | 1 |
| D-11 | G-11 Übergang alte Skills | `--expect` ganz entfernen (kein Tombstone) | user's own | 1 |
| D-12 | G-3 Snapshot | Kein Snapshot-Test; offener Punkt im README: nach v1 neu entscheiden | user's own | 1 |
| D-13 | G-7 Szenarien | Pflichtteil plus Fehler-Fixtures (999, 0) plus S18/S19 plus Subagent-Runs | user's own | 1 |
| D-14 | Wortlaut-Guard | `SkillContractTests` prüft Zahlen, Altlasten und den kanonischen Satz in 5 festen Skills | recommended accepted | 1 |
| D-15 | 6 Teststrategie | Unit- und E2E-Tests (echter Parser in `ProgramTests`) | recommended accepted | 1 |
| D-16 | G-9 Wertebereich | Ganzzahl ≥ 0, nur ASCII-Ziffern, kein Vorzeichen, keine führenden Nullen | recommended accepted | 1 |
| D-17 | 7 Persistenz | n/a — `VersionCommand` nutzt keinen Kit-Store, nichts in `.shadekit/` betroffen | n/a | 1 |
| D-18 | 8 Abhängigkeiten | n/a — kein Paket geändert; Python nur Standardbibliothek | n/a | 1 |
| D-19 | Gate 2 Skills | Behalten: dotnet-fundamentals, dotnet-tester, dotnet-xmldocs, code-design, skill-creator. Gestrichen: api-and-interface-design, deprecation-and-migration, documentation-and-adrs. Review: dotnet-reviewer | user's own | — |
| D-20 | Gate 3 Tasks | T1–T5 bestätigt | recommended accepted | — |
| D-21 | Gate 3 Modus | Sub-Agents, streng sequenziell, ein Subagent pro Task | user's own (präzisiert) | — |
| D-22 | T1 blockiert | „Done when“ von T1 angepasst: Der FP01-AC8-Guard darf bis T2 rot sein | recommended accepted | — |
| D-23 | T4 2. FAIL | Als blockiert markiert, Lücke in `ENGINE_UPDATE` als offener Punkt (später geschlossen, siehe D-24 bis D-26) | user's own | — |
| D-24 | Gate 5 Runde 1 | M2, M4 und M5 beheben; M1, M3, S1 und N1 zurückstellen | user's own | — |
| D-25 | Gate 5 Runde 2 | M6 beheben; N2 zurückstellen | user's own | — |
| D-26 | Gate 5 Runde 3 | M7 beheben, bekannte Grenze dokumentieren (S2), Formulierungen präzisieren (N3), danach keine weitere Heuristik-Runde | recommended accepted | — |
| D-27 | Gate 5 Runde 4 | Review abgenommen; S3 und N4 zurückgestellt | recommended accepted | — |

Waivers: keine.

## 5. Skills

**Laufzeit-Discovery:** rund 75 Skills und 13 Agent-Typen in der Laufzeit, 13 Kandidaten nach dem Abgleich.

| Skill / agent | Category | Proposed use | Outcome |
|---|---|---|---|
| cc-ai-dotnet:dotnet-fundamentals | knowledge (baseline) | Code-Tasks | kept |
| cc-ai-dotnet:dotnet-tester | tester | alle Tasks | kept |
| cc-ai-dotnet:dotnet-xmldocs | documentation | öffentliche Typen | kept |
| cc-ai-dev:code-design | knowledge | Code-Tasks | kept |
| skill-creator:skill-creator | knowledge | Skill- und Szenario-Tasks | kept |
| agent-skills:api-and-interface-design | knowledge | Engine-Task | dropped by user (D-19) |
| agent-skills:deprecation-and-migration | knowledge | Entfernen von `--expect` | dropped by user (D-19) |
| agent-skills:documentation-and-adrs | documentation | Doku-Task | dropped by user (D-19) |
| cc-ai-dotnet:dotnet-nuget-manager | tooling | — | stayed excluded (D-18) |
| agent-skills:test-driven-development | knowledge | — | stayed excluded (überschneidet sich mit dotnet-tester) |
| cc-ai-dotnet:dotnet-dev | workflow | excluded — nested workflow | stayed excluded |
| cc-ai-dotnet:dotnet-reviewer | reviewer | review | used in Phase 5 (4 Runden) |
| agent-skills:code-reviewer | review agent | Ausweichlösung | not needed |

**Zuordnung (Gate 3) und tatsächlich geladen:**

| Task | Mapped skills | Loaded by implementer | Confirmed by verifier |
|---|---|---|---|
| T1 Core-Vertrag | dotnet-fundamentals, dotnet-tester, dotnet-xmldocs, code-design | alle 4 (Turn 1) | V1 ok (berichtbasiert) |
| T2 CLI-Umstellung | dotnet-fundamentals, dotnet-tester, dotnet-xmldocs, code-design | alle 4 (Turn 2) | V1 ok (berichtbasiert) |
| T3 Skills und Guard | dotnet-tester, skill-creator, dotnet-fundamentals, code-design | alle 4 (Turn 2) | V1 ok (berichtbasiert) |
| T4 Szenario-Tooling | skill-creator | skill-creator (Turn 2; im Retry ebenfalls Turn 2) | V1 ok (berichtbasiert) |
| T5 Doku | — (D-19) | keine | n/a bestätigt |
| R-1 Fenster-Naht | dotnet-fundamentals, dotnet-tester, dotnet-xmldocs, code-design | alle 4 (Turn 2) | V1 ok (berichtbasiert) |
| R-2, R-3, R-4 Checker und Doku | skill-creator | skill-creator (Turn 4, 2 bzw. 2) | V1 ok (berichtbasiert) |

## 6. Tasks

### Task #1 — CLI-Vertrag in Core (Status: done)

- **Liefert:** AC-1 und AC-3 (Core-Teil).
- **Dateien:** `Core/Versions/CliContract.cs`, `CliContractWindow.cs`, `ICliContractChecker.cs`, `CliContractChecker.cs`, `Exceptions/EngineContractOutdatedException.cs`, `Exceptions/SkillsContractOutdatedException.cs`, `ShadeKitCoreServiceCollectionExtensions.cs`, dazu Tests.
- **Tests:** alle 15 geplanten grün, ohne Umbenennungen.
- **Doku:** XML-Docs. **Pakete:** n/a.
- **Implementer:** Subagent. Er meldete BLOCKED nur wegen des Guards FP01-AC8, weil dafür die Datei `CliErrorMapper` geändert werden musste, die zu T2 gehört. Daraus wurde D-22.
- **Verifier:** Subagent, PASS.
- **Smoke-Check:** n/a, nur Core betroffen.
- **Abweichungen:** keine.

### Task #2 — CLI auf den Vertrag umstellen, alten Check entfernen (Status: done)

- **Liefert:** AC-2, AC-3, AC-4, AC-5, AC-6, AC-13 und AC-14.
- **Dateien:** `Cli/Commands/Version/*`, `CliErrorMapper.cs`, Exit-, Error- und Warning-Codes, DI. Es wurden 7 Core-Typen und 2 CLI-Typen samt ihrer Tests gelöscht.
- **Tests:** Alle geplanten Tests sind grün. Umbenannt wurden `ExecuteAsync_JsonModeWithoutExpect_…` → `…WithoutExpectContract_WritesEnvelopeWithContractAndIntegrations`, `Ctor_NullEngineVersionChecker_…` → `Ctor_NullCliContractChecker_…` und `Version_ExpectMismatchWithInstalledIntegration_StillExits50` → `Version_ExpectContractMismatchWithInstalledIntegration_Exits54WithoutAskingClaude`.
- **Doku:** XML-Docs. **Pakete:** n/a.
- **Implementer und Verifier:** je ein Subagent, PASS.
- **Smoke-Check:** `--expect-contract 1/999/0/abc` → Exit 0/54/55/2. Die Plain-Zeile zeigt den Vertrag.
- **Abweichungen:** Zwei bestehende Plain-E2E-Tests erwarten jetzt die Vertragszeile. Ein Integrations-E2E-Test prüft jetzt 999 → 54.

### Task #3 — Skills und `SkillContractTests` (Status: done)

- **Liefert:** AC-7 und AC-8.
- **Dateien:** 19 `skills/*/SKILL.md`, `SkillContractTests.cs` neu, `SkillKitVersionTests.cs` gelöscht.
- **Tests:** 34 Fälle grün. Der Fall „Handshake-Skill fehlt“ wurde als eigener Test abgespalten.
- **Validierung:** `quick_validate.py` meldet 19/19 gültig.
- **Implementer und Verifier:** je ein Subagent. Der Verifier gab PASS und hat dabei 7 echte Mutationen der Skills gemacht und danach wiederhergestellt.
- **Smoke-Check:** `version --json --expect-contract 1` → ok.
- **Abweichungen:** Der Finder ist `internal static` in der Testklasse. `--expected` wird nicht als altes `--expect` gemeldet.

### Task #4 — Szenario-Tooling (Status: done, vorübergehend blocked per D-23)

- **Liefert:** AC-10.
- **Dateien:** `generate_fixtures.py`, `SCENARIOS.md` (S18, S19), `checks/check_runs.py`, `README.md`.
- **Implementer:** ein Subagent, dann ein Retry. **Verifier:** FAIL, Retry, erneut FAIL. Nach dem zweiten FAIL markierte der Nutzer den Task als blockiert (D-23). Die Lücke in `ENGINE_UPDATE` wurde in R-2, R-3 und R-4 geschlossen.
- **Runner-Subagents:** S18 und S19 liefen mit der README-Vorlage. Der Checker meldet 22/22.
- **Smoke-Check:** Generatorlauf mit Exit 0, `CLI contract: 1`.
- **Abweichungen:** keine außerhalb der Dateien.

### Task #5 — Doku und GitVersion-Kommentar (Status: done)

- **Liefert:** AC-11 und AC-12, D-6 und D-12.
- **Dateien:** `GitVersion.yml` (nur der Kommentar), `CLAUDE.md`, 14 Feature-Pakete und `README.md`, `research/shade-kit-concept.md`.
- **Implementer:** ein Subagent, dann ein Retry. **Verifier:** zuerst FAIL wegen 01:74 „always-present `integrations`“, nach dem Retry ein neuer Verifier mit PASS.
- **Smoke-Check:** Die dokumentierten Aussagen wurden ausgeführt: `--expect-contract=999` → Exit 0 ohne Prüfung, `--expect 0.1.0` → Exit 0.
- **Abweichung im Brief:** Zwei Tests tragen weiterhin `FP06-1-AC6`. In 06-1 ist das korrekt dokumentiert.

### R-1 — Fenster aus einer Quelle (Befund M2) · R-2 — ENGINE_UPDATE und Runs-Eintrag (M4, M5) · R-3 — ENGINE_UPDATE über alle Phrasen (M6) · R-4 — Aufrufform, bekannte Grenze, Wortlaut (M7, S2, N3)

- **R-1:** `ICliContractChecker.Window`. `VersionCommand` meldet das Fenster des Checkers. Es gibt zwei neue Tests mit dem Fenster (2, 4). Verifier PASS, M7b und M7c werden rot.
- **R-2 bis R-4:** Die Heuristik in `check_runs.py` ordnet die gesammelten 44 Phrasen richtig ein, und die Aufrufform `$shade-kit:` zählt nicht als Werkzeug. Der Runs-Eintrag vom 2026-11-22 ist ergänzt, D-23 ist in 06-1 und im README datiert geschlossen, und die bekannte Grenze ist dokumentiert. Jeder Rework hatte einen eigenen Verifier, alle mit PASS.

## 7. Review

Reviewer: `cc-ai-dotnet:dotnet-reviewer` als Subagent. Taxonomie: Critical, Major, Minor, Suggestion, Nitpick.

| Round | Report | Critical | Major | Minor | Suggestion / Nitpick | Rework done | Minor selected | Deferred |
|---|---|---|---|---|---|---|---|---|
| 1 | docs/reviews/2026-11-22-feature-rework-versioning-branch-mgmt-uncommitted.md | 0 | 0 | 5 | 1 / 1 | R-1 (M2), R-2 (M4, M5) | M2, M4, M5 | M1, M3, S1, N1 (D-24) |
| 2 | …-uncommitted-round-2.md | 0 | 0 | 1 | 0 / 1 | R-3 (M6) | M6 | N2 (D-25) |
| 3 | …-uncommitted-round-3.md | 0 | 0 | 1 | 1 / 1 | R-4 (M7, S2, N3) | M7 (+ S2, N3) | — |
| 4 | …-uncommitted-round-4.md | 0 | 0 | 0 | 1 / 1 | — | — | S3, N4 (D-27) |

**Anforderungstreue laut Teil B:**

| Round | Proven (executed) | Read-checked | Unproven | Gestrichen | Decision departures | Scope findings |
|---|---|---|---|---|---|---|
| 1 | 10 | 3 | 0 (AC-2-Werte: teilweise unproven) | 1 | 0 | 0 |
| 2 | 11 | 2 | 0 | 1 | 0 | 0 |
| 3 | 11 | 2 | 0 | 1 | 0 | 0 |
| 4 | 10 | 2 | 0 | 1 | 0 | 0 |

In den Runden 2 bis 4 ist AC-10 gemischt: Der Checker-Teil ist proven, der Generator-Teil nur read-checked.

**Wiederholte Befunde:** Die `ENGINE_UPDATE`-Heuristik hatte Befunde in T4 (zweimal), in Runde 1 (M4), in Runde 2 (M6) und in Runde 3 (M7, eine Regression aus R-3). Ursache ist der Ansatz, eine Regex über freien Text. Seit R-4 ist das als bekannte Grenze dokumentiert, und nach D-26 wird die Heuristik nicht weiter erweitert.

## 8. Verify Before Committing

- **`--expect` wird jetzt still ignoriert (D-11).** Skills, die vor dieser Änderung installiert wurden, bestehen gegen die neue Engine ohne Prüfung. Siehe `ProgramTests.RunAsync_VersionJsonExpectOldOption_IsNotChecked`.
- **Alte Engines prüfen nichts (D-10).** Eine Engine von vor dieser Änderung antwortet auf `--expect-contract` mit `ok:true`, ohne zu prüfen. Die Skills prüfen `data.contract` nicht.
- **`--expect-contract=999`, also die Form mit Gleichheitszeichen, wird vom Parser ignoriert** und liefert Exit 0. `SkillContractTests` akzeptiert diese Form bislang (Befund M1, zurückgestellt). Achte bei neuen Skill-Texten darauf.
- **Kein Snapshot-Test (D-12).** Wer die für Skills sichtbare CLI-Oberfläche ändert, muss `CliContract.Current` von Hand anheben. Offener Punkt in `docs/feature-packages/README.md`.
- **Exit-Code 50 ist frei**, `version-mismatch` und `engine-prerelease-version-mismatch` gibt es nicht mehr. Das ist in `ShadeKitCliExitCodes.cs`, `ShadeKitCliErrorCodes.cs` und `ShadeKitCliWarningCodes.cs` sichtbar.
- **`docs/feature-packages/06-1-engine-version-check.md` ist neu geschrieben.** Der alte Text steht als datierter Abschnitt „Replaced on 2026-11-22 … (history)“ darin.
- **`tests/skill-scenarios/checks/check_runs.py:32-57`** ist eine dokumentierte Heuristik mit bekannter Grenze. Ein FAIL in S18/S19 ist gegen den Abschlusstext zu lesen.
- **Neue Review-Berichte** liegen in `docs/reviews/`, vier Dateien. Entscheide, ob du sie committen willst.

## 9. Open Items

**Zurückgestellte Befunde:**

| Befund | Runde | Entscheidung | Inhalt |
|---|---|---|---|
| M1 | 1 | D-24 | `SkillContractTests` akzeptiert `--expect-contract=<n>`, das die Engine ignoriert |
| M3 | 1 | D-24 | FP06-1-AC1 bis AC5 haben keine Tests mit `[Trait("Requirement", …)]` |
| S1 | 1 | D-24 | Vertragsnummer 1 ist in einigen Tests fest eingetragen; die Anleitung zum Anheben in CLAUDE.md nennt diese Stellen nicht |
| N1 | 1 | D-24 | Ein fehlender kanonischer Satz wird auf Zeile `:1:` gemeldet |
| N2 | 2 | D-25 | `Window_Constructed_ReturnsWindowOfConstructor` testet nur einen trivialen Getter |
| S3 | 4 | D-27 | `:\S` in `_TOOL` ist breiter als dokumentiert; Vorschlag `:\w` |
| N4 | 4 | D-27 | Der `_TOOL`-Kommentar nennt den Bindestrich nicht |

**Offene Punkte in `docs/feature-packages/README.md`:**
- (a) Snapshot-Test nach Abschluss von v1 neu entscheiden (D-12).
- (b) Lücke im Szenario-Checker (D-23): in Review-Runde 1 geschlossen und in Runde 2 verfeinert.
- (c) `shade-kit-next-steps` Schritt 5.3 regelt nicht den Fall, dass `phase next` nie lief. Beide Runner haben diesen Teil ausgelassen.

**Blockierte Tasks:** keine mehr. T4 war vorübergehend blockiert (D-23).

**Skills, die nicht geladen werden konnten:** keine.

**Nicht ausgeführt:** Die 17 bestehenden Szenarien S1 bis S17 wurden nach der Änderung des Handshakes nicht neu durchgespielt. Nur S18 und S19 liefen (D-13).

## 10. Final Verification

- **Build:** `dotnet build` → 0 Warnungen, 0 Fehler.
- **Gesamtsuite:** `dotnet test` → 6440 total, 6432 passed, 0 failed, 8 skipped (nur unter Windows).
- **Smoke-Check:** `dotnet run --project src/CreativeCoders.ShadeKit.Cli -- version --json --expect-contract 1/999/0/abc` → Exit 0/54/55/2. `version` zeigt `CLI contract 1 (supported: 1 to 1)`.
- **Szenario-Checker:** `check_runs.py <work> --only S18,S19` → 22/22.
- **Geänderte Dateien:** 89 Einträge in `git status --porcelain`, einschließlich dieses Berichts:
  - 59 geändert
  - 13 gelöscht
  - 15 neue Code- und Testdateien
  - `docs/reviews/` (4 Berichte)
  - dieser Bericht

## 11. Skill-Invocation Log

```
Task #1 — CLI-Vertrag in Core
  [x] cc-ai-dotnet:dotnet-fundamentals — vor dem ersten Write von CliContractTests.cs (Implementer Turn 1; Verifier V1 ok)
  [x] cc-ai-dotnet:dotnet-tester — Turn 1, vor dem ersten Write (Verifier V1 ok)
  [x] cc-ai-dotnet:dotnet-xmldocs — Turn 1, vor dem ersten Write (Verifier V1 ok)
  [x] cc-ai-dev:code-design — Turn 1, vor dem ersten Write (Verifier V1 ok)
Task #2 — CLI-Umstellung
  [x] cc-ai-dotnet:dotnet-fundamentals — Turn 2, vor dem ersten Write/rm (Verifier V1 ok)
  [x] cc-ai-dotnet:dotnet-tester — Turn 2 (Verifier V1 ok)
  [x] cc-ai-dotnet:dotnet-xmldocs — Turn 2 (Verifier V1 ok)
  [x] cc-ai-dev:code-design — Turn 2 (Verifier V1 ok)
Task #3 — Skills und Guard
  [x] cc-ai-dotnet:dotnet-tester — Turn 2, erster Write in Turn 5 (Verifier V1 ok)
  [x] skill-creator:skill-creator — Turn 2 (Verifier V1 ok)
  [x] cc-ai-dotnet:dotnet-fundamentals — Turn 2 (Verifier V1 ok)
  [x] cc-ai-dev:code-design — Turn 2 (Verifier V1 ok)
  [n/a] cc-ai-dotnet:dotnet-xmldocs — keine öffentliche API: Die Testklasse und ihr Finder sind nicht öffentlich
Task #4 — Szenario-Tooling
  [x] skill-creator:skill-creator — Turn 2, erster Write in Turn 7; im Retry Turn 2, erster Write in Turn 6 (Verifier V1 ok)
  [n/a] .NET-Skills — kein C# geändert, nur Python und Markdown unter tests/skill-scenarios/
Task #5 — Doku und GitVersion.yml
  [n/a] keine Skills zugeordnet — documentation-and-adrs vom Nutzer gestrichen (D-19), Code-Skills gelten nicht für Dokumente
R-1 — Fenster-Naht (M2)
  [x] cc-ai-dotnet:dotnet-fundamentals — Turn 2, vor dem ersten Write (Verifier V1 ok)
  [x] cc-ai-dotnet:dotnet-tester — Turn 2 (Verifier V1 ok)
  [x] cc-ai-dotnet:dotnet-xmldocs — Turn 2 (Verifier V1 ok)
  [x] cc-ai-dev:code-design — Turn 2 (Verifier V1 ok)
R-2 — ENGINE_UPDATE und Runs-Eintrag (M4, M5)
  [x] skill-creator:skill-creator — Turn 4, erster Write in Turn 6 (Verifier V1 ok)
R-3 — ENGINE_UPDATE über alle Phrasen (M6)
  [x] skill-creator:skill-creator — Turn 2, vor dem ersten Write (Verifier V1 ok)
R-4 — Aufrufform, bekannte Grenze, Wortlaut (M7, S2, N3)
  [x] skill-creator:skill-creator — Turn 2, vor dem ersten Write (Verifier V1 ok)
```

Hinweis: Die Reihenfolge der Skill-Aufrufe stammt aus den Berichten der Implementer. Die Verifier konnten die Transkripte nicht einsehen und haben V1 jeweils als berichtbasiert bestätigt. Sie haben dieselben Skills selbst geladen, bevor sie Code lasen.
