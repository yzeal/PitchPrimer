# Japanese Pitch Accent Trainer - Development Notes

## 📝 IMPORTANT: How to Write Summary Notes for Chat Interface

**CRITICAL:** When writing summaries in this file that will be shared via chat interface:
- **NEVER use triple backticks (```)** - they close code blocks prematurely in chat
- Use **4-space indentation** for code examples instead
- Use **single backticks (`)` for inline code only
- Use **markdown headers and lists** for structure
- This prevents formatting issues when copying between computers via chat

---

## 🎵 AUDIO FORMAT & CONVERSION SETUP

### Recommended Audio Format for Unity Cross-Platform:
**Format:** WAV (16-bit, Mono, 44.1kHz)
- ✅ Unity native support on all platforms (PC, Mac, iOS, Android)
- ✅ Uncompressed quality for optimal pitch analysis
- ✅ No licensing issues (unlike MP3)
- ✅ Fast loading for real-time analysis
- ✅ File size: ~2.6MB per minute

### FFmpeg Commands for MP4 → WAV Conversion:

**Basic extraction (preserves original quality):**

    ffmpeg -i input.mp4 -vn -acodec pcm_s16le -ar 44100 -ac 1 output.wav

**Optimized for speech analysis (with frequency filtering):**

    ffmpeg -i input.mp4 -vn -acodec pcm_s16le -ar 44100 -ac 1 -af "highpass=f=80,lowpass=f=800" output.wav

**Batch conversion for multiple files:**

    for %f in (*.mp4) do ffmpeg -i "%f" -vn -acodec pcm_s16le -ar 44100 -ac 1 "%~nf.wav"

**Parameter explanations:**
- `-vn` → No video track
- `-acodec pcm_s16le` → 16-bit WAV format
- `-ar 44100` → 44.1kHz sample rate
- `-ac 1` → Mono (saves 50% file size)
- `-af "highpass=f=80,lowpass=f=800"` → Speech frequency filter (80-800Hz)

### Unity Import Settings for Japanese Audio:
- **Load Type:** Decompress On Load (for real-time analysis)
- **Compression Format:** PCM (uncompressed)
- **Quality:** 100%
- **Force To Mono:** Enabled
- **Sample Rate Setting:** Preserve Sample Rate

### Audio Output Device Selection:
**NOTE:** Unity does not support audio output device selection - always uses system default device. This is a Unity limitation, not a project issue. For testing convenience, users need to set their preferred audio device at the system level before starting the application.

---

# Japanese Pitch Accent Trainer - Refactoring Progress Summary

## Projekt-Überblick
Unity 6.1 Projekt für japanische Aussprache-Training mit Fokus auf Pitch-Akzent und Rhythmus durch Chorusing-Übungen (gleichzeitiges Sprechen mit nativen Aufnahmen).

## LATEST UPDATE - Day 2 Achievements

### 1. ✅ Complete Test Scene Setup 
**MAJOR:** Created step-by-step test scene setup guide for refactored architecture
- Clean scene structure with separated GameObjects
- Proper Inspector configuration guide
- All component references correctly wired
- UI Canvas with proper button events

### 2. ✅ Architecture Improvements
**FIXED:** ChorusingManager duplicate settings issue
- Removed duplicate VisualizationSettings from ChorusingManager
- Now references PitchVisualizers directly (single source of truth)
- Added GetSettings() method to PitchVisualizer for read-only access
- Much cleaner Inspector, less error-prone

### 3. ✅ Enhanced Noise Gate Controls
**ADDED:** Exposed noise gate settings in MicAnalysisRefactored
- enableNoiseGate: true/false toggle
- noiseGateMultiplier: adjustable sensitivity (3.0f default)
- ambientCalibrationTime: calibration duration (2.0f default)  
- ambientSamplePercentage: sample percentage for ambient (0.7f default)
- debugNoiseGate: separate noise gate debugging
- RecalibrateNoiseGate() method for runtime adjustment

### 4. ✅ Refined Component Integration
**IMPROVED:** Event system and component references
- All scripts properly integrated with new architecture
- MicrophoneSelector updated for MicAnalysisRefactored
- Clean separation of concerns between components
- Robust error handling and debug logging

## Previous Achievements (Day 1)

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
        public float timestamp;  // Zeit in Sekunden
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
- **NEW:** GetSettings() method for ChorusingManager integration

#### VisualizationSettings
- cubePrefab, cubeParent, cubeSpacing
- maxCubes, trackOffset (für zweite Spur)
- pitchScaleMultiplier, min/maxFrequency
- silenceColor, HSV-Mapping, saturation, brightness

### 4. Event-basierte Mikrofonanalyse

#### MicAnalysisRefactored.cs
- Verwendet geteilten PitchAnalyzer
- Event-System: OnPitchDetected?.Invoke(pitchData)
- **ENHANCED:** Full noise gate controls exposed in Inspector
- Lose Kopplung zwischen Komponenten
- Robuste Mikrofoninitialisierung

#### MicrophoneSelector.cs (Updated)
- Kompatibel mit MicAnalysisRefactored
- Filtert virtuelle Audio-Devices (Oculus, VR, etc.)
- Debug-Funktionen und Status-Anzeige

### 5. Chorusing-System Grundlage

#### ChorusingManager.cs
- **IMPROVED:** No duplicate settings, references visualizers directly
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

## 7. Current Test Setup Status

### ✅ Completed Basic Testing:
1. MicrophoneSelector → Echtes Mikrofon wählen (nicht Oculus)
2. RefactoredTestManager → "Start Test" klicken
3. Sprechen/summen → Bunte Würfel erscheinen kontinuierlich
4. Stille Abschnitte → Kleine schwarze Würfel (behält Timing)
5. Noise gate fine-tuning → Settings exposed and adjustable

### 🔄 NEXT: Advanced Chorusing Tests (for Home PC):
**PRIORITY:** Test full chorusing functionality with native recording
1. **AudioClip Setup:** Add Japanese native speaker AudioClip to ChorusingManager
2. **Dual-Track Test:** StartChorusing() → Verify dual visualization works
3. **Synchronization Test:** Native recording (hinten, dunkel) + User input (vorne, farbig)
4. **Timing Verification:** Ensure synchrone aktivierung der nativen Würfel
5. **Audio Loop Test:** Verify auto-loop functionality works correctly
6. **Performance Test:** Monitor frame rate with dual-track rendering

## 8. Datei-Struktur (Updated)
    Assets/_GAME/Scripts/
    ├── Core/
    │   └── PitchAnalyzer.cs          ✅ Kern-Engine
    ├── Visualization/
    │   └── PitchVisualizer.cs        ✅ Modulare Visualisierung (improved)
    ├── Chorusing/
    │   └── ChorusingManager.cs       ✅ Hauptcontroller (refactored)
    ├── Debug/
    │   ├── RefactoredTestManager.cs  ✅ Test-System
    │   └── PitchAnalyzerTest.cs      ✅ Komponenten-Tests
    ├── MicAnalysisRefactored.cs      ✅ Event-basierte Mikrofonanalyse (enhanced)
    ├── MicrophoneSelector.cs         ✅ Updated für neues System
    └── Notes/
        └── Notes.md                  ✅ Diese Datei

## 9. NEXT STEPS for Home PC

### Immediate Testing Goals:
1. **Setup Native AudioClip:**
   - Find/create Japanese speech sample (10-30 seconds)
   - Import to Unity project
   - Assign to ChorusingManager.nativeClip
   - Verify pre-analysis runs without errors

2. **Test Chorusing Workflow:**
   - Call ChorusingManager.StartChorusing() via UI button
   - Verify native track pre-renders (dark cubes in back)
   - Verify user track works simultaneously (bright cubes in front)
   - Check synchronization between audio playback and native visualization

3. **Debug Common Issues:**
   - Monitor console for any pre-analysis errors
   - Verify AudioSource auto-creation works
   - Check native cube activation timing matches audio playback
   - Ensure loop functionality works correctly

4. **Performance Optimization:**
   - Monitor frame rate with dual visualization
   - Check memory usage during pre-analysis
   - Optimize cube count if needed for smooth playback

### Scene Setup Checklist for Home PC:
    ✅ Unity 6.1 project created
    ✅ Scripts copied to correct folder structure
    ✅ Test scene with all GameObjects created
    ✅ Inspector settings configured
    ✅ Basic microphone test working
    🔄 Native AudioClip added to ChorusingManager
    🔄 Chorusing start/stop UI buttons added
    🔄 Dual-track visualization tested
    🔄 Audio synchronization verified

### Required UI Additions:
1. **Add Chorusing Control Buttons:**
   - "Start Chorusing" button → ChorusingManager.StartChorusing()
   - "Stop Chorusing" button → ChorusingManager.StopChorusing()
   - "Set Native Clip" button (optional for testing different clips)

2. **Status Display Enhancement:**
   - Show chorusing active state
   - Display native playback time
   - Show native analysis progress during pre-analysis

### Testing Parameters:
    Noise Gate Settings (if too aggressive):
      noiseGateMultiplier: 2.0f (reduce from 3.0f)
      ambientCalibrationTime: 3.0f
      debugNoiseGate: true (for monitoring)
    
    Native Visualization (for clear distinction):
      trackOffset: (0, 0, 2)
      saturation: 0.5 (dimmed)
      brightness: 0.7 (darker)
    
    User Visualization (for prominence):
      trackOffset: (0, 0, 0)
      saturation: 0.8 (vibrant)
      brightness: 1.0 (bright)

## 10. Known Issues & Solutions

### Problem: Noise gate too aggressive
**Solution:** Adjust noiseGateMultiplier down to 2.0f or 1.5f

### Problem: ChorusingManager duplicate settings
**Solution:** ✅ FIXED - Now references visualizers directly

### Problem: Virtual devices in microphone list
**Solution:** ✅ WORKING - MicrophoneSelector filters automatically

### Problem: Pitch scale too small for visibility
**Solution:** ✅ WORKING - pitchScaleMultiplier at 1.5f

### Problem: Timeline breaks during silence
**Solution:** ✅ WORKING - Constant cube generation implemented

## 11. Architecture Lessons Learned

### ✅ Single Source of Truth:
- PitchAnalyzer handles all analysis logic
- PitchVisualizers manage their own settings
- No duplicate configuration needed

### ✅ Event-Driven Design:
- Loose coupling between components
- Easy to extend with new features
- Clean separation of concerns

### ✅ Inspector-Friendly:
- All critical settings exposed
- Debug controls separate from main settings
- Runtime status information available

## COPILOT CONTEXT for Home Computer:
This Unity 6.1 project implements real-time pitch analysis for Japanese pronunciation training using chorusing exercises (speaking simultaneously with native recordings). The refactored architecture uses modular components: PitchAnalyzer (core analysis), PitchVisualizer (dual-track display), MicAnalysisRefactored (microphone input), and ChorusingManager (coordination). All basic functionality is working. NEXT STEP: Test full chorusing with native AudioClip playback and dual-track visualization synchronization.

**Status: ✅ Refactoring complete, ✅ Basic testing successful, 🔄 Ready for chorusing implementation!**