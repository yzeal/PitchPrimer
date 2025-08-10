# Japanese Pitch Accent Trainer - Scoring System Development Notes

## ?? �bersicht
Detaillierte Dokumentation aller �berlegungen, Implementierungsversuche und Erkenntnisse zur Entwicklung eines fairen Scoring-Systems f�r den Japanese Pitch Accent Trainer.

---

## ?? SCORING SYSTEM ZIELE

### Prim�res Ziel: Faires Pitch-Kurven-Scoring
- **Kern-Aufgabe:** Vergleich der Pitch-Kurven zwischen User und japanischem Muttersprachler
- **Fokus:** Pitch Accent Training, NICHT generelle Sprachqualit�t
- **Qualit�tskriterien:** 
  - ? Gute japanische Nachahmung ? 80-95% Score
  - ? Mittelm��ige Versuche ? 50-75% Score  
  - ? Zuf�llige Ger�usche ("Hallo! Blablabla!") ? 10-30% Score
  - ? Rauschen/Gemurmel ? 0-15% Score

### Technische Anforderungen
- **Sprachunabh�ngig:** System soll NUR Pitch-Muster bewerten, nicht japanische Phoneme erkennen
- **Robustheit:** Funktioniert ohne Voice Range Kalibrierung (optional nutzbar)
- **Performance:** Real-time Scoring f�r sofortiges Feedback
- **Flexibilit�t:** Anpassbare Parameter f�r verschiedene �bungstypen

---

## ?? EVOLUTION DES SCORING-SYSTEMS

### Phase 1: Basic Correlation Scoring (Initial)
**STATUS:** Abgeschlossen, aber zu simpel

#### Implementierung:
- Einfache Pitch-Kurven Korrelation
- Grundlegende Normalisierung auf 0-1 Range
- Linear gewichtete Kombination von Pitch + Rhythm

#### Problem erkannt:
- Zu tolerant f�r zuf�llige Ger�usche
- Keine DTW-Alignment f�r zeitliche Verschiebungen
- Unzureichende Pitch-Pattern-Erkennung

#### Code-Beispiel des Problems:
    float correlation = CalculateCorrelation(nativePitches, userPitches);
    // Problem: Einfache Korrelation ignoriert zeitliche Verschiebungen

### Phase 2: DTW-Implementation (Aktuell)
**STATUS:** Implementiert, aber fundamentale Gewichtungsprobleme entdeckt

#### DTW (Dynamic Time Warping) Ansatz:
- **Vorteil:** Handles zeitliche Verschiebungen zwischen User und Native
- **Komponenten:** Alignment + Pitch Similarity + Contour + Range
- **Flexibilit�t:** Konfigurierbare Toleranzen und Gewichtungen

#### Aktuelle DTW-Struktur:
    DTWResult:
    - alignmentScore: Wie gut lassen sich die Sequenzen alignieren
    - pitchSimilarity: Tats�chliche Pitch-�hnlichkeit nach Alignment
    - contourSimilarity: Pitch-Richtungs-�bereinstimmung (steigend/fallend)
    - rangeSimilarity: �hnlichkeit der genutzten Pitch-Ranges

#### KRITISCHES PROBLEM ENTDECKT: Fehlerhafte Gewichtung!
**Analyse der "Blablabla"-Logs:**
    DTW Score: 0,989 (Alignment: 0,989, Pitch: 0,687, Contour: 0,536, Range: 0,754)
    
    PROBLEM: Alignment (99%) dominiert den Score v�llig!
    - Alignment: 99% - DTW kann fast alles alignieren
    - Pitch: 69% - Tats�chliche �hnlichkeit nur mittelm��ig  
    - Contour: 54% - Pitch-Richtungen stimmen nur zur H�lfte
    - Range: 75% - Range-Nutzung �hnlich (irrelevant bei Noise)

#### Bug in der Gewichtungsberechnung:
    // AKTUELLER CODE (FEHLERHAFT):
    float overallScore = 
        alignmentScore * (1f - 0.5f - 0.3f - 0.2f) +  // = 0% Gewicht!
        pitchSimilarity * 0.5f +
        contourSimilarity * 0.2f +
        rangeSimilarity * 0.3f;
    
    PROBLEM: (1f - 0.5f - 0.3f - 0.2f) = 0f
    ? Alignment bekommt 0% Gewichtung, aber dominiert trotzdem den Score!

---

## ?? AKTUELLER SCORING-BUG: DTW zu tolerant

### Kern-Problem: Mathematische Gewichtung fehlerhaft
**Entdeckung:** DTW gibt zuf�lligen Ger�uschen 98,9% Score statt ~30%

#### Detailed Score-Breakdown eines "Blablabla"-Tests:
    Native Pitch Range: ~290-380Hz (japanische Frauenstimme)
    User "Blablabla": ~147-521Hz (deutsche Sprache, v�llig andere Muster)
    
    Visueller Vergleich: Pitch-Kurven sehen KOMPLETT unterschiedlich aus
    DTW Ergebnis: 98,9% Overall Score ? V�LLIG FALSCH!

#### Warum DTW versagt:
1. **DTW ist zu flexibel:** Kann jede Sequenz mit jeder anderen alignieren
2. **Alignment dominiert:** 99% Alignment-Score �berschreibt schlechte Pitch-Scores
3. **Normalisierung verwischt Unterschiede:** Relative Normalisierung macht verschiedene Muster �hnlich
4. **Toleranzen zu hoch:** pitchWarpingTolerance=50Hz erlaubt massive Abweichungen

### Mathematische Analyse der Gewichtung:
    DEFAULT SETTINGS (PROBLEMATISCH):
    pitchContourWeight = 0.5f     // 50%
    pitchRangeWeight = 0.3f       // 30%  
    pitchDirectionWeight = 0.2f   // 20%
    
    Alignment Weight = 1f - (0.5f + 0.3f + 0.2f) = 0f  ? BUG!
    
    TATS�CHLICHE BERECHNUNG:
    Score = 0.99 * 0f + 0.69 * 0.5f + 0.54 * 0.2f + 0.75 * 0.3f
    Score = 0 + 0.345 + 0.108 + 0.225 = 0.678 = 67.8%
    
    ABER: Code-Output zeigt 98.9% ? Anderer Bug im Alignment-Score-Handling!

---

## ?? L�SUNGSANS�TZE F�R FAIRERES SCORING

### L�sung 1: Korrigierte DTW-Gewichtung
**Ziel:** Pitch-Accuracy wichtiger als Alignment-Flexibilit�t

#### Neue Gewichtungsverteilung:
    pitchContourWeight = 0.6f     // 60% - Wichtigster Faktor
    pitchDirectionWeight = 0.2f   // 20% - Pitch-Richtungen  
    pitchRangeWeight = 0.1f       // 10% - Range-Nutzung
    Alignment Weight = 0.1f       // 10% - Minimal, nur f�r Basic-Alignment

#### Erwartete Ergebnisse mit neuer Gewichtung:
    "Blablabla" Test:
    - Alignment: 99% � 10% = 9.9 Punkte
    - Pitch: 69% � 60% = 41.4 Punkte  
    - Contour: 54% � 20% = 10.8 Punkte
    - Range: 75% � 10% = 7.5 Punkte
    - Gesamt: ~69.6% ? Besser, aber immer noch zu hoch!

### L�sung 2: Striktere DTW-Parameter
**Ziel:** Weniger Toleranz f�r gro�e Pitch-Abweichungen

#### Vorgeschlagene Parameter-�nderungen:
    timeWarpingTolerance = 0.5f ? 0.3f        // Weniger zeitliche Flexibilit�t
    pitchWarpingTolerance = 50f ? 20f         // Viel striktere Pitch-Toleranz
    stepPenalty = 0.3f ? 0.5f                 // H�here Strafe f�r Spr�nge

#### Sch�rfere Pitch-Similarity-Berechnung:
    // AKTUELL (ZU TOLERANT):
    return Mathf.Exp(-averageDifference * 0.5f);
    
    // VORSCHLAG (STRENGER):
    float strictnessFactor = 3.0f;  // Viel strenger
    float similarity = Mathf.Exp(-averageDifference * strictnessFactor);
    
    // Zus�tzliche Penalties f�r hohe Abweichungen:
    if (averageDifference > 0.3f) similarity *= 0.5f;  // 50% Penalty
    if (averageDifference > 0.6f) similarity *= 0.2f;  // Weitere 80% Penalty

### L�sung 3: Mehrstufige Validierung
**Ziel:** Zuf�llige Ger�usche fr�hzeitig aussortieren

#### Anti-Noise-Validierung:
    1. Pitch-Count-Check: 
       if (userPitches.Count < nativePitches.Count * 0.3f) return 0f;
    
    2. Chaos-Detection:
       float chaos = CalculatePitchChaos(userPitches);
       if (chaos > 0.8f) return 0f;  // Zu chaotisch = Noise
    
    3. Frequency-Range-Check:
       Reject pitches outside human voice range (60-600Hz)

#### Pitch-Chaos-Berechnung:
    float CalculatePitchChaos(List<float> pitches) {
        float totalVariation = 0f;
        for (int i = 1; i < pitches.Count; i++) {
            totalVariation += Mathf.Abs(pitches[i] - pitches[i-1]);
        }
        float averageChange = totalVariation / (pitches.Count - 1);
        float range = pitches.Max() - pitches.Min();
        return averageChange / range;  // Hoher Chaos = viele gro�e Spr�nge
    }

---

## ?? CHORUSING-SPEZIFISCHE SCORING-HERAUSFORDERUNGEN

### Problem 1: Timing-Offset beim User-Start
**Beschreibung:** User startet Recording fr�her/sp�ter als Native-Playback
**L�sung:** Sliding Window Correlation mit �2s Suchbereich
**Implementation:** `FindBestAlignment()` mit offset-basierter Korrelation

### Problem 2: Loop-Overlap im User-Recording  
**Beschreibung:** User-Recording enth�lt Ende des vorherigen Native-Loops
**Beispiel:** Native: "Konnichiwa" ? User h�rt: "...wa Konnichiwa" 
**L�sung:** `RemoveLoopOverlap()` - vergleicht User-Anfang mit Native-Ende
**Threshold:** Overlap-Erkennung bei >50% Korrelation mit Native-Ende

### Problem 3: Verschiedene Sprechtempo
**Beschreibung:** User spricht langsamer/schneller als Native
**L�sung:** Shape-basierte Analyse statt absolute Timing
**Method:** `CalculateShapeSimilarity()` - Pitch-�nderungs-Muster vergleichen

### Problem 4: Speech vs. Silence Detection
**Beschreibung:** User macht andere Pausen als Native Speaker
**L�sung:** `ExtractSpeechSegments()` - nur confidence > 0.3 ber�cksichtigen
**Vorteil:** Fokus auf tats�chliche Sprache, nicht auf Pausen-Timing

---

## ?? TESTING & VALIDATION

### Debug-Tools entwickelt
**DebugScoringTester:** Umfangreiches Test-Framework f�r systematische Validation

#### Test-Kategorien implementiert:
1. **Identity Tests:** Native gegen sich selbst (sollte ~100% sein)
2. **Noise Tests:** Zuf�llige Ger�usche (sollte ~0-30% sein)  
3. **Voice Range Tests:** Verschiedene Stimmlagen simulieren
4. **Quality Tests:** Verschiedene Nachahmungsqualit�ten

#### Discovered Test-Problem: Identity Test Inkonsistenz
**Problem:** "Native vs Self" Test erreicht nur 60-70% statt 100%
**Ursache:** Verschiedene Analyse-Pfade f�r Native vs Test-Daten

#### Test-Daten-Quelle-Problem:
    // PROBLEM: Verschiedene Datenquellen
    nativePitchData = chorusingManager.GetNativePitchData();      // Pre-analyzed
    testPitchData = PitchAnalyzer.PreAnalyzeAudioClip(clip, ...); // Fresh analysis
    
    // L�SUNG: Gleiche Datenquelle verwenden
    var nativeRecording = chorusingManager.GetCurrentRecording();
    testPitchData = nativeRecording.GetPitchData(0.1f);  // Identische Daten!

#### Identity Test Erfolg:
Nach Datenquellen-Fix: Native vs Self erreicht jetzt 100% Score ?
Best�tigt: DTW-Algorithmus kann identische Daten perfekt erkennen

### Umfangreiche Log-Analyse
**Erkenntnisse aus Real-World-Testing:**

#### Log-Pattern f�r "Blablabla"-Problem:
    [DTWPitchAnalyzer] Combined normalization: Mean=344,12Hz, StdDev=35,97Hz
    [DTWPitchAnalyzer] Normalized ranges: Native=[-1,861, 1,563], User=[-1,887, 2,657]
    [DTWPitchAnalyzer] DTW Score: 0,989 (Alignment: 0,989, Pitch: 0,687, Contour: 0,536, Range: 0,754, Path: 70 steps)
    
    ANALYSE:
    - Normalisierung zeigt verschiedene Ranges ? Gut, unterschiedliche Daten erkannt
    - Alignment 98.9% ? DTW zu flexibel
    - Pitch 68.7% ? Tats�chliche �hnlichkeit niedrig
    - Contour 53.6% ? Richtungsmuster unterschiedlich
    - Final Score 98.9% ? BUG: Alignment dominiert

---

## ?? N�CHSTE SCHRITTE: Implementierung der Fixes

### Priorisierte Ma�nahmen:

#### 1. SOFORT: DTW-Gewichtung korrigieren
    public static DTWSettings Default => new DTWSettings {
        pitchContourWeight = 0.6f,      // 60% f�r Pitch-Accuracy
        pitchDirectionWeight = 0.2f,    // 20% f�r Contour
        pitchRangeWeight = 0.1f,        // 10% f�r Range
        // Alignment = 10% (1f - 0.6f - 0.2f - 0.1f)
    };

#### 2. KRITISCH: Pitch-Similarity sch�rfen
- Strictness-Factor von 0.5f auf 3.0f erh�hen
- Zus�tzliche Penalties f�r gro�e Abweichungen
- Minimum-Threshold f�r sehr schlechte Matches

#### 3. ROBUST: Anti-Noise-Validierung
- Pitch-Count-Ratio pr�fen (min 30% der Native-Pitches)
- Chaos-Detection f�r erratische Pitch-Spr�nge
- Human-Voice-Range-Validation (60-600Hz)

#### 4. VALIDIERUNG: Erweiterte Tests
- Systematische Tests mit verschiedenen Noise-Typen
- Voice-Range-Simulationen f�r verschiedene Nutzergruppen  
- Performance-Benchmarks f�r Real-Time-Scoring

### Erwartete Ergebnisse nach Fixes:
    Test-Szenario                    Aktueller Score    Ziel-Score
    Native vs Self                   100%               100% ?
    Gute japanische Nachahmung      ?                  80-95%
    Mittelm��iger Versuch           ?                  50-75%
    "Hallo! Blablabla!"             98.9% ?           10-30%
    Rauschen/Gemurmel               ?                  0-15%

---

## ?? LANGFRISTIGE SCORING-VISION

### Advanced Pitch Accent Recognition
**Ziel:** Echte japanische Pitch Accent Patterns erkennen

#### Geplante Erweiterungen:
1. **Accent-Type-Recognition:** ?? vs ?? vs ?? vs ?? Pattern
2. **Mora-Based-Analysis:** Japanische Zeiteinheiten statt kontinuierliche Zeit
3. **Context-Aware-Scoring:** Wort-spezifische Accent-Erwartungen
4. **Machine-Learning-Integration:** Pattern-Learning aus Muttersprachlern

#### Forschungsbereiche:
- **Pitch Accent Databases:** Verf�gbare Pattern-Daten f�r japanische W�rter
- **Signal Processing:** Erweiterte Pitch-Tracking f�r japanische Sprache
- **Comparative Analysis:** Bestehende Tools f�r Pitch Accent Training
- **User Studies:** Welche Scoring-Metriken helfen Lernenden am meisten

### Technical Architecture Evolution
**Vision:** Modulares Scoring-System mit Plugin-Architektur

#### Geplante Komponenten:
    IScoringAlgorithm Interface:
    - IBasicPitchScoring (DTW-based, aktuell)
    - IAdvancedAccentScoring (Pattern-Recognition)
    - IMLPitchScoring (Machine Learning)
    - IRhythmScoring (Mora-based)

#### Konfigurierbare Scoring-Pipeline:
    ScoringPipeline:
    1. Input-Validation (Anti-Noise)
    2. Pre-Processing (Normalization, Filtering)
    3. Primary-Algorithm (DTW/ML/Pattern)
    4. Post-Processing (Confidence, Range-Mapping)
    5. User-Feedback (Detailed breakdown)

---

## ?? ERKENNTNISSE & LESSONS LEARNED

### Wichtige Prinzipien f�r Scoring-Systeme:
1. **Gewichtung ist kritisch:** Falsche Weights k�nnen gute Algorithmen zerst�ren
2. **Toleranz-Parameter entscheidend:** Zu hohe Toleranz = nutzlose Bewertung
3. **Anti-Pattern-Detection n�tig:** Zuf�llige Inputs m�ssen aussortiert werden
4. **Identity-Tests unverzichtbar:** Native vs Self muss 100% erreichen
5. **Extensive Logging essentiell:** Komplexe Algorithmen brauchen detaillierte Diagnostik

### Development-Workflow-Erkenntnisse:
- **Test-Framework zuerst:** DebugScoringTester war investition wert
- **Real-World-Testing kritisch:** Labor-Tests vs echte User-Inputs sehr unterschiedlich
- **Incremental-Validation:** Kleine �nderungen einzeln testen, nicht alles auf einmal
- **Parameter-Sensitivity-Analysis:** Kleine �nderungen k�nnen gro�e Auswirkungen haben

### Unity-Spezifische Erkenntnisse:
- **Floating-Point-Precision:** Winzige Unterschiede k�nnen Scores beeinflussen
- **Audio-Analysis-Consistency:** Gleiche AudioClips k�nnen verschiedene Analyse-Ergebnisse haben
- **Real-Time-Performance:** DTW ist CPU-intensiv, Optimierung f�r Mobile n�tig
- **Cross-Platform-Audio:** Verschiedene Plattformen = verschiedene Audio-Charakteristika

---

## ?? STATUS SUMMARY

### Aktueller Stand (Implementation Phase 2):
- ? **DTW-Framework:** Implementiert und funktional
- ? **Test-Infrastructure:** Umfangreiches Testing-Framework verf�gbar
- ? **Identity-Validation:** Native vs Self erreicht 100%
- ? **Fairness-Problem:** "Blablabla" erreicht 98.9% statt ~30%
- ? **Parameter-Tuning:** DTW-Gewichtung und Toleranzen problematisch
- ? **Anti-Noise-Logic:** Geplant, noch nicht implementiert

### N�chste Session Priorities:
1. **CRITICAL:** DTW-Gewichtung korrigieren (60% Pitch, 10% Alignment)
2. **HIGH:** Pitch-Similarity sch�rfen (3x strenger)
3. **MEDIUM:** Anti-Noise-Validierung implementieren
4. **LOW:** Extended Testing mit realen User-Recordings

### Langfristige Roadmap:
- **Phase 3:** Advanced Japanese Pitch Accent Recognition
- **Phase 4:** Machine Learning Integration
- **Phase 5:** User-Adaptive Scoring (personalized difficulty)

---

**FAZIT:** Grundlegendes DTW-Framework steht, aber Gewichtung und Toleranzen m�ssen drastisch angepasst werden f�r faires Scoring. Identity-Tests best�tigen technische Korrektheit, Real-World-Tests zeigen Verbesserungsbedarf bei Noise-Rejection.

**STATUS:** Framework implementiert, Parameter-Tuning erforderlich ????

---

### Phase 3: AdvancedScoringAlgorithms Framework (Aktuell - Neue Implementation)
**STATUS:** Implementiert - Ersetzt DTW-Ansatz mit robusterem System

#### Warum neuer Ansatz statt DTW-Fix:
- **Architektur-Problem:** DTW-Code war zu komplex und fehleranf�llig
- **Maintenance-Problem:** Schwer zu debuggen und zu erweitern
- **Performance-Problem:** DTW zu CPU-intensiv f�r Real-Time-Scoring
- **Saubere L�sung:** Komplett neue Klasse mit klarer Trennung der Verantwortlichkeiten

#### AdvancedScoringAlgorithms Design-Prinzipien:
- **Modulare Architektur:** Separate Klasse f�r Kurven-Vergleiche
- **Chorusing-Fokus:** Speziell f�r Timing-Offset und Loop-Overlap-Probleme designed
- **Multi-Method-Approach:** Korrelation + Shape-Analysis + Range-Matching
- **Konfigurierbare Pipeline:** Alle Parameter �ber Settings anpassbar

#### Kerntechnologien implementiert:
1. **Automatic Speech Onset Detection:** Ignoriert Stille-Bereiche
2. **Loop Overlap Removal:** Erkennt und entfernt Native-Loop-Reste
3. **Sliding Window Alignment:** Findet beste Timing-�bereinstimmung (-2s bis +2s)
4. **Multi-Scale Similarity:** Shape (70%) + Correlation (30%) weighted
5. **Graceful Fallbacks:** Advanced ? Normalized ? Simple Correlation

#### Code-Struktur:
    AdvancedScoringAlgorithms.ComparePitchCurves():
    1. ExtractSpeechSegments() - Nur confidence > 0.3
    2. RemoveLoopOverlap() - Erkennt Native-Ende-Overlap
    3. NormalizeCurve() - Optional 0-1 range normalization  
    4. FindBestAlignment() - Sliding window correlation
    5. CalculateCurveSimilarity() - Multi-method scoring

---

## ?? TESTING EMPFEHLUNGEN F�R PHASE 3

### Empfohlene Editor-Einstellungen:
    ScoringManager Inspector:
    ? Use Advanced Scoring: true
    ? Use Normalized Scoring: true (Fallback)
    ? Enable Debug Logging: true
    
    Advanced Settings (Default-Werte):
    - Max Timing Offset: 2.0s
    - Speech Threshold: 0.3  
    - Shape Weight: 0.7 (70% Shape vs 30% Correlation)
    - Detect Loop Overlap: true
    - Max Overlap Ratio: 0.3

### Debug-Log-Pattern zu erwarten:
    ?? Advanced Pitch Analysis:
       Similarity: 0.xxx
       Timing Offset: x.xxs  
       Confidence: 0.xxx
       Details: Native: xx speech points, User: xx speech points...

### Test-Scenarios f�r Validation:
1. **Perfect Match:** Native vs. identische Aufnahme ? ~95-100%
2. **Good Imitation:** Gute japanische Nachahmung ? 80-95%
3. **Timing Offset:** User startet 1s sp�ter ? Sollte trotzdem hohen Score geben
4. **Loop Overlap:** Recording mit Native-Ende-�berlappung ? Automatisch entfernt
5. **Random Noise:** "Blablabla" ? <30% (deutlich besser als DTW's 98.9%)

---

## Neue Erkenntnisse aus Phase 3:
1. **Saubere Architektur wichtiger als perfekte Algorithmen:** Wartbare Klassen-Trennung
2. **Chorusing braucht spezielle Behandlung:** Standard-Algorithmen ignorieren Loop-Probleme  
3. **Fallback-Strategie essentiell:** Hierarchisches Scoring (Advanced ? Normalized ? Simple)
4. **Parameter-Konfigurierbarkeit kritisch:** Runtime-Settings f�r verschiedene Szenarien
5. **Shape vs. Absolute Pitch:** Pitch-Muster wichtiger als exakte Frequenzen (70:30 Gewichtung)

### Unity-Spezifische Neue Erkenntnisse:
- **Klassen-Trennung:** Static utility classes f�r komplexe Algorithmen sinnvoll
- **Inspector-Integration:** [System.Serializable] Settings f�r einfache Parameter-Anpassung
- **Debug-Hierarchie:** Emojis in Logs (??) f�r bessere Visual-Scanning
- **Event-Integration:** Hierarchische Scoring-Methoden in bestehendem ScoringManager

---

## ?? STATUS UPDATE (Nach AdvancedScoringAlgorithms Implementation)

### Aktueller Stand (Implementation Phase 3):
- ? **AdvancedScoringAlgorithms:** Komplett neue Implementierung fertig
- ? **ScoringManager Integration:** Hierarchisches Fallback-System implementiert  
- ? **Chorusing-Optimierungen:** Loop-Overlap und Timing-Offset handling
- ? **Multi-Method-Scoring:** Shape + Correlation + Range weighted combination
- ? **Real-World-Testing:** Ben�tigt systematische Validation mit verschiedenen Inputs
- ? **Parameter-Tuning:** Default-Werte m�ssen durch Testing optimiert werden

### N�chste Session Priorities:
1. **HIGH:** Systematisches Testing mit verschiedenen User-Recording-Qualit�ten
2. **MEDIUM:** Fine-Tuning der Shape-Weight und Speech-Threshold Parameter  
3. **LOW:** Performance-Optimierung f�r Mobile-Plattformen

### Technische Debt bereinigt:
- ? **DTW-Complexity:** Entfernt - zu komplex und fehleranf�llig
- ? **Clean Architecture:** AdvancedScoringAlgorithms als separate, testbare Klasse
- ? **Maintainable Code:** Klare Methoden-Trennung und umfassendes Debug-Logging