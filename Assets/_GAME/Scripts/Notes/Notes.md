# Japanese Pitch Accent Trainer - Development Notes

## 📝 IMPORTANT: How to Write Summary Notes for Chat Interface

**CRITICAL:** When writing summaries in this file that will be shared via chat interface:
- **NEVER use triple backticks (```)** - they close code blocks prematurely in chat
- Use **4-space indentation** for code examples instead
- Use **single backticks (`)** for inline code only
- Use **markdown headers and lists** for structure
- This prevents formatting issues when copying between computers via chat

---

# Japanese Pitch Accent Trainer - Refactoring Progress Summary

## Projekt-Überblick
Unity 6.1 Projekt für japanische Aussprache-Training mit Fokus auf Pitch-Akzent und Rhythmus durch Chorusing-Übungen (gleichzeitiges Sprechen mit nativen Aufnahmen).

## Heutige Errungenschaften

### 1. Vollständige Code-Architektur Refactoring
**Vorher:** Monolithisches MicAnalysis Script mit Code-Duplikation  
**Nachher:** Modulare, saubere Architektur mit geteilten Komponenten

### 2. Kern-Komponenten Implementiert

#### PitchAnalyzer.cs (Kern-Engine)
- Static class für geteilte Pitch-Analyse
- Autocorrelation-Algorithmus mit Hann-Windowing
- Unterstützt Real-Time + Pre-Analysis von AudioClips
- Mono/Stereo-Konvertierung
- Smoothing und Statistiken
- Robuste Fehlerbehandlung

**Hauptfunktionen:**
- PitchAnalyzer.AnalyzeAudioBuffer(buffer, timestamp, settings)
- PitchAnalyzer.PreAnalyzeAudioClip(clip, settings, interval)
- PitchAnalyzer.SmoothPitchData(data, windowSize)

#### PitchDataPoint Struktur
    public struct PitchDataPoint {
        public float timestamp;    // Zeit in Sekunden
        public float frequency;    // Pitch in Hz (0 = Stille)
        public float confidence;   // Korrelationskoeffizient (0-1)
        public float audioLevel;   // Lautstärke (0-1)
        public bool HasPitch => frequency > 0;
    }

### 3. Modulares Visualisierungs-System

#### PitchVisualizer.cs
- Unterstützt Real-Time + Pre-rendered Visualisierung
- HSV-Farbmapping (rot → violett für tief → hoch)
- Logarithmische Pitch-Skalierung
- Dual-Track-Support für Chorusing
- Pre-rendered native Aufnahmen (dunkel/inaktiv)
- Sync-Aktivierung während Playback

#### VisualizationSettings
- cubePrefab, cubeParent, cubeSpacing
- maxCubes, trackOffset (für zweite Spur)
- pitchScaleMultiplier, min/maxFrequency
- silenceColor, HSV-Mapping, saturation, brightness

### 4. Event-basierte Mikrofonanalyse

#### MicAnalysisRefactored.cs
- Verwendet geteilten PitchAnalyzer
- Event-System: OnPitchDetected?.Invoke(pitchData)
- Noise Gate mit Ambient-Kalibrierung
- Lose Kopplung zwischen Komponenten
- Robuste Mikrofoninitialisierung

#### MicrophoneSelector.cs (Updated)
- Kompatibel mit MicAnalysisRefactored
- Filtert virtuelle Audio-Devices (Oculus, VR, etc.)
- Debug-Funktionen und Status-Anzeige

### 5. Chorusing-System Grundlage

#### ChorusingManager.cs
- Event-basierte Integration mit MicAnalysisRefactored
- Pre-Analysis von nativen Aufnahmen
- Dual-Track Visualisierung (User + Native)
- Synchronisierte Playback-Steuerung
- Automatische AudioSource-Erstellung

### 6. Wichtige technische Verbesserungen

#### Konstante Synchronisation
- Würfel erscheinen IMMER (auch bei Stille) für perfekte Timeline
- Stille = kleine schwarze/transparente Würfel
- Vorbereitung für Audio-Synchronisation mit nativen Sprechern

#### Optimierte Parameter für Japanisch
- minFrequency: 80Hz (Minimum menschliche Stimme)
- maxFrequency: 800Hz (Optimiert für japanische Pitch-Akzente)
- analysisInterval: 0.1f (100ms für gute Reaktionszeit)
- correlationThreshold: 0.1f (Empfindlich aber robust)

## 7. Test-Setup Bereit

### Einfacher Test-Workflow:
1. MicrophoneSelector → Echtes Mikrofon wählen (nicht Oculus)
2. RefactoredTestManager → "Start Test" klicken
3. Sprechen/summen → Bunte Würfel erscheinen kontinuierlich
4. Stille Abschnitte → Kleine schwarze Würfel (behält Timing)

### Erweiterte Chorusing-Tests:
1. AudioClip zu ChorusingManager hinzufügen
2. StartChorusing() → Dual-Track Visualisierung
3. Native Aufnahme (hinten, dunkel) + User Input (vorne, farbig)
4. Synchrone Aktivierung der nativen Würfel

## 8. Datei-Struktur
    Assets/_GAME/Scripts/
    ├── Core/
    │   └── PitchAnalyzer.cs          ✅ Kern-Engine
    ├── Visualization/
    │   └── PitchVisualizer.cs        ✅ Modulare Visualisierung
    ├── Chorusing/
    │   └── ChorusingManager.cs       ✅ Hauptcontroller
    ├── Debug/
    │   ├── RefactoredTestManager.cs  ✅ Test-System
    │   └── PitchAnalyzerTest.cs      ✅ Komponenten-Tests
    ├── MicAnalysisRefactored.cs      ✅ Event-basierte Mikrofonanalyse
    ├── MicrophoneSelector.cs         ✅ Updated für neues System
    └── Notes/
        └── Notes.md                  ✅ Diese Datei

## 9. Nächste Schritte (für anderen PC)
1. Git-Sync: Repository klonen und Unity 6.1 öffnen
2. Test-Scene setup: Gemäß heutiger Architektur
3. Native Aufnahmen: Japanische