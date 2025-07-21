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

**PowerShell (recommended):**

    Get-ChildItem *.mp4 | ForEach-Object { ffmpeg -i $_.Name -vn -acodec pcm_s16le -ar 44100 -ac 1 ($_.BaseName + ".wav") }

**CMD/Batch (.bat file):**

    for %%f in (*.mp4) do ffmpeg -i "%%f" -vn -acodec pcm_s16le -ar 44100 -ac 1 "%%~nf.wav"

**PowerShell with progress display:**

    $files = Get-ChildItem *.mp4
    $total = $files.Count
    $current = 0
    foreach ($file in $files) {
        $current++
        Write-Host "Processing $current/$total : $($file.Name)"
        ffmpeg -i $file.Name -vn -acodec pcm_s16le -ar 44100 -ac 1 ($file.BaseName + ".wav")
    }

**PowerShell with frequency filtering (optimized for Japanese speech):**

    Get-ChildItem *.mp4 | ForEach-Object { 
        $outputName = $_.BaseName + ".wav"
        ffmpeg -i $_.Name -vn -acodec pcm_s16le -ar 44100 -ac 1 -af "highpass=f=80,lowpass=f=800" $outputName
    }

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

## LATEST UPDATE - Day 9: Voice Calibration System & Visualization Improvements 🎙️

### ✅ MAJOR MILESTONE: PersonalPitchRange Color Mapping Implementation
**BREAKTHROUGH:** Cube colors now relative to individual voice range instead of fixed spectrum
- **PersonalPitchRange-based colors:** Farben basieren jetzt auf `personalMinPitch` bis `personalMaxPitch`
- **Improved color resolution:** Bessere Farbauflösung für tatsächlich verwendete Stimmbreite
- **Consistent mapping:** Gleiche Normalisierung für Würfelhöhe und Farbe
- **Scientific accuracy:** Low voice = red cubes, high voice = blue/purple cubes

#### Technical Implementation:

    private Color GetCubeColor(PitchDataPoint pitchData)
    {
        // NEW: Use PersonalPitchRange for color mapping instead of legacy frequency range
        if (settings.useHSVColorMapping)
        {
            // Clamp pitch to personal range for consistent color mapping
            float clampedPitch = Mathf.Clamp(pitch, range.personalMinPitch, range.personalMaxPitch);
            
            // Map personal pitch range to color spectrum (0.0 to 0.8 on hue wheel)
            float normalizedPitch = (clampedPitch - range.personalMinPitch) / (range.personalMaxPitch - range.personalMinPitch);
            normalizedPitch = Mathf.Clamp01(normalizedPitch);
            
            // Convert to HSV: 0.0 = red (low pitch), 0.8 = purple (high pitch)
            return Color.HSVToRGB(normalizedPitch * 0.8f, settings.saturation, settings.brightness);
        }
        return Color.white;
    }

### ✅ VISUAL ENHANCEMENT: Extended Cube Lifetime for Better Continuity
**USER EXPERIENCE IMPROVEMENT:** Cubes now stay visible longer to prevent abrupt disappearing
- **Extended visibility:** Cubes from first repetition visible until third repetition passes focal point
- **Reduced "pop-out" effect:** Especially important for short audio clips
- **Better visual continuity:** More context visible during chorusing practice
- **Configurable system:** Can be adjusted via `cubeLifetimeMultiplier` parameter

#### Implementation in ManageRepetitions():

    private void ManageRepetitions()
    {
        // NEW: Remove repetitions that are 2+ repetitions behind the focal point
        // This allows cubes from first playthrough to stay visible until third playthrough passes focal point
        float extendedRemoveThreshold = focalPointLocalX - (2.0f * repetitionTotalLength);
        
        // Previous: removeThreshold = focalPointLocalX - repetitionTotalLength (1x)
        // Current: extendedRemoveThreshold = focalPointLocalX - (2.0f * repetitionTotalLength) (2x)
    }

### ✅ INFRASTRUCTURE: Complete Cross-Platform Settings System
**PLATTFORMÜBERGREIFENDE PERSISTIERUNG:** Vollständiges Settings-System für alle Zielplattformen implementiert
- **UserVoiceSettings ScriptableObject:** Strukturierte Daten für Stimm-Kalibrierung
- **SettingsManager Singleton:** DontDestroyOnLoad für szenenübergreifende Persistierung  
- **PlayerPrefs Integration:** Funktioniert auf PC, Mac, iOS, Android, WebGL
- **Event-based application:** Automatische Anwendung auf alle PitchVisualizer

#### Key Components:
- **UserVoiceSettings:** Voice type detection, calibration metadata, Japanese pitch mapping
- **SettingsManager:** Singleton pattern, automatic loading/saving, visualizer integration
- **VoiceRangeCalibrator:** English phrase-based calibration, statistical analysis, quality scoring

### ✅ CALIBRATION SYSTEM: English-Based Voice Range Detection
**INNOVATIVE APPROACH:** Use English phrases to determine user's natural voice range for Japanese training
- **Smart phrase selection:** Optimized English phrases for maximum pitch range detection
- **Statistical analysis:** Outlier removal (10%) and quality scoring
- **Voice type detection:** Automatic classification (Male/Female/Child/Deep/Low)
- **Japanese mapping:** Intelligent adaptation for Japanese pronunciation patterns

#### Calibration Phrases Optimized for Pitch Range:

    private string[] calibrationPhrases = {
        "Hello, how are you today?",           // Natural conversation
        "What a beautiful morning!",           // Exclamation (higher pitch)
        "I'm really excited about this.",      // Emotion (varied pitch)
        "That sounds good to me.",             // Agreement (lower tones)
        "Oh no, that's terrible!",             // Surprise (pitch jumps)
        "Please count from one to ten."        // Steady enumeration
    };

### ✅ TECHNICAL MIGRATION: MicAnalysisRefactored Integration
**MODERNIZATION:** Migrated VoiceRangeCalibrator from legacy MicAnalysis to event-based MicAnalysisRefactored
- **Event-driven architecture:** Uses OnPitchDetected events instead of direct API calls
- **Shared analysis engine:** Leverages modern PitchAnalyzer core
- **Better performance:** Optimized noise gate and confidence filtering
- **Consistent codebase:** Same analysis system for calibration and chorusing

#### API Migration:

    // OLD: Direct API calls (legacy MicAnalysis)
    float currentPitch = micAnalysis.GetCurrentPitch();
    
    // NEW: Event-based (MicAnalysisRefactored)
    micAnalysis.OnPitchDetected += OnPitchDataReceived;
    private void OnPitchDataReceived(PitchDataPoint pitchData)
    {
        if (pitchData.HasPitch && pitchData.confidence >= confidenceThreshold)
            calibrationPitches.Add(pitchData.frequency);
    }

### ⚠️ TESTING STATUS: Calibration System Implementation Complete but Untested
**CURRENT STATE:** All calibration system components implemented and compiling successfully
- **VoiceRangeCalibrator:** Complete implementation with microphone dropdown selection
- **UI Integration:** Ready for microphone dropdown, progress slider, phrase display
- **Settings Persistence:** Full PlayerPrefs integration across platforms
- **Error Handling:** Comprehensive validation and fallback mechanisms

#### Required for Testing:
1. **Scene Setup:** Create CalibrationScene with UI components
2. **Microphone Dropdown:** Add TMP_Dropdown for microphone selection  
3. **UI References:** Wire up instructionText, phraseText, progressSlider, buttons
4. **Build Settings:** Add CalibrationScene to build settings for scene transitions

### 🎯 ARCHITECTURE STATE AFTER VOICE CALIBRATION SYSTEM

#### VoiceRangeCalibrator (New):
- **Event-based pitch collection:** Uses MicAnalysisRefactored.OnPitchDetected
- **Smart microphone selection:** Automatic filtering of virtual audio devices
- **Statistical analysis:** Outlier removal, quality scoring, voice type detection
- **Settings integration:** Direct integration with SettingsManager singleton

#### SettingsManager (New):
- **Cross-platform persistence:** PlayerPrefs-based storage system
- **Automatic application:** ApplyToAllVisualizers() for seamless integration
- **Scene survival:** DontDestroyOnLoad for settings consistency
- **Voice mapping:** Support for both English calibration and Japanese adaptation

#### PitchVisualizer (Enhanced):
- **PersonalPitchRange colors:** Both height and color use same pitch range
- **Extended cube lifetime:** Better visual continuity for short clips
- **Settings integration:** Automatic application of calibrated voice ranges
- **Backwards compatibility:** Legacy settings still supported

### 🎯 SUCCESS CRITERIA ACHIEVED
**COMPREHENSIVE VOICE CALIBRATION FOUNDATION:** Major infrastructure completed
- **PersonalPitchRange color mapping:** Implemented and working ✅
- **Extended cube lifetime:** Visual continuity improved ✅
- **Settings system:** Cross-platform persistence ready ✅
- **Calibration logic:** English-to-Japanese voice mapping ready ✅
- **Modern architecture:** Event-based, modular, maintainable ✅

### 📋 NEXT STEPS FOR CALIBRATION TESTING
**READY FOR IMPLEMENTATION:** Core system complete, UI setup needed
1. **Create CalibrationScene:** Basic Unity scene with Canvas
2. **UI Component Setup:** Dropdown, buttons, text fields, progress slider
3. **Wire References:** Connect UI elements to VoiceRangeCalibrator
4. **Test Workflow:** English calibration → Settings save → Main scene application
5. **Validation:** Verify PersonalPitchRange affects both color and height

### 📚 LESSONS LEARNED: Voice Calibration Architecture
- **English-based approach:** Practical solution for users without Japanese experience
- **Event-driven design:** More robust than direct API dependencies
- **Statistical validation:** Outlier removal and quality scoring ensure reliable results
- **Cross-platform thinking:** PlayerPrefs provides universal storage solution
- **Modular architecture:** Separate calibration from training for clean separation of concerns

**STATUS:** Voice calibration system architecture complete, ready for UI setup and testing! 🎙️

## Day 8: Code Cleanup & Debug Features 🔧

### ✅ CRITICAL FIX: Personal Pitch Range Overwrite Issue Solved
**MAJOR BUG RESOLVED:** Editor pitch range values no longer overwritten at runtime
- **Problem identified:** `InitializePersonalPitchRange()` method was overwriting user-configured values
- **Root cause:** Method called in `Awake()` and `SetAsNativeTrack()` unconditionally overwrote Editor settings
- **Solution:** Complete removal of problematic method - serves no purpose with Unity's serialization

#### The Problem - Over-Engineered "Helpful" Code:

    // ❌ BAD: This overwrote Editor values every time
    private void InitializePersonalPitchRange()
    {
        if (isNativeTrack)
        {
            settings.pitchRange.personalMinPitch = 120f; // Overwrites Editor!
            settings.pitchRange.personalMaxPitch = 280f; // Overwrites Editor!
        }
        else
        {
            settings.pitchRange.personalMinPitch = 100f; // Overwrites Editor!
            settings.pitchRange.personalMaxPitch = 350f; // Overwrites Editor!
        }
    }

#### The Fix - Trust Unity's Serialization:

    // ✅ GOOD: Unity handles this automatically
    void Awake()
    { 
        EnsureInitialization();
        // REMOVED: InitializePersonalPitchRange(); - Unnecessary and harmful!
    }
    
    public void SetAsNativeTrack(bool isNative)
    {
        isNativeTrack = isNative;
        // REMOVED: InitializePersonalPitchRange(); - Unnecessary and harmful!
    }

#### Why This Works Better:
- **Unity serialization:** Automatically creates `PersonalPitchRange` objects with class defaults
- **Editor persistence:** User changes in Inspector are automatically saved
- **Class defaults:** Initial values (100f-300f) are sensible for all use cases
- **No code needed:** The simpler approach is more reliable

### ✅ NEW FEATURE: Native Recording Pitch Analysis Debug System
**ENHANCED DEVELOPMENT TOOLS:** Comprehensive confidence threshold analysis for pitch curve optimization
- **Multi-threshold analysis:** Tests 10 different confidence levels (0.0 to 0.9)
- **Statistical breakdown:** Min/Max/Average pitch at each threshold
- **Data retention analysis:** Shows how much data survives each threshold
- **Intelligent recommendations:** Suggests optimal thresholds based on data quality

#### Implementation in ChorusingManager:

    // NEW: Debug logging for Pitch-Range at various Confidence-Thresholds
    private void LogPitchRangeByConfidence()
    {
        float[] thresholds = { 0.0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f };
        
        foreach (float threshold in thresholds)
        {
            var validPitches = nativePitchData
                .Where(p => p.HasPitch && p.confidence >= threshold)
                .Select(p => p.frequency)
                .ToList();
            
            // Analysis: Min, Max, Average, Range, Sample retention
        }
    }

#### Example Debug Output:

    === PITCH RANGE ANALYSIS BY CONFIDENCE THRESHOLDS ===
    Confidence >= 0.0: Min=120.5Hz, Max=285.3Hz, Avg=180.2Hz, Range=164.8Hz, Samples=156/200 (78.0%)
    Confidence >= 0.3: Min=125.2Hz, Max=280.1Hz, Avg=182.5Hz, Range=154.9Hz, Samples=132/200 (66.0%)
    Confidence >= 0.5: Min=128.8Hz, Max=275.6Hz, Avg=184.1Hz, Range=146.8Hz, Samples=118/200 (59.0%)
    RECOMMENDATION: Use threshold 0.3 (retains 80%+ of data with good quality)

### ✅ TECHNICAL INSIGHT: Pitch vs. Volume Independence Confirmed
**QUESTION ANSWERED:** Does audio volume affect cube height in pitch visualization?
- **Answer:** NO - Code analysis confirms pitch and volume are completely independent
- **Cube height calculation:** Based ONLY on `pitchData.frequency`, NOT `pitchData.audioLevel`
- **Correlation perception:** Likely due to natural speech patterns (louder tones often higher pitch)
- **Scientifically correct:** Implementation properly separates pitch and amplitude analysis

#### Code Evidence:

    private float CalculatePitchScale(PitchDataPoint pitchData)
    {
        float pitch = pitchData.frequency; // ✅ ONLY frequency used
        // ❌ audioLevel is NOT used for height calculation
        
        float normalizedPitch = (pitch - range.personalMinPitch) / (range.personalMaxPitch - range.personalMinPitch);
        return Mathf.Lerp(range.minCubeHeight, range.maxCubeHeight, normalizedPitch);
    }

### 🎯 DEVELOPMENT WORKFLOW IMPROVEMENTS
**BETTER DEBUGGING:** Enhanced tools for pitch curve analysis and development
- **Confidence analysis:** Understand audio quality vs. data retention trade-offs
- **Threshold optimization:** Data-driven approach to confidence filtering
- **Clean codebase:** Removed unnecessary initialization methods
- **Trust Unity:** Leverage Unity's serialization instead of custom initialization

### 🏗️ ARCHITECTURE STATE AFTER CLEANUP

#### PitchVisualizer (Simplified):
- **Removed InitializePersonalPitchRange():** No longer overwrites Editor values
- **Clean initialization:** Unity's serialization handles PersonalPitchRange creation
- **Simplified lifecycle:** `Awake()` and `SetAsNativeTrack()` no longer call removed method
- **Preserved functionality:** All manual calibration methods (`SetMaleVoiceRange()`, etc.) still work

#### ChorusingManager (Enhanced):
- **Added LogPitchRangeByConfidence():** Comprehensive pitch analysis debugging
- **LINQ integration:** Added `using System.Linq` for data analysis
- **Conditional execution:** Debug analysis only runs when `enableDebugLogging = true`
- **Statistical insights:** Multi-threshold analysis with intelligent recommendations

### 🎯 SUCCESS CRITERIA MET
**CLEAN CODE & BETTER TOOLS:** Major improvements in maintainability and debugging
- **Bug eliminated:** Personal pitch range values persist correctly ✅
- **Editor workflow improved:** User settings no longer mysteriously overwritten ✅
- **Debug tools enhanced:** Comprehensive confidence threshold analysis ✅
- **Code simplified:** Removed unnecessary initialization methods ✅
- **Scientific accuracy confirmed:** Pitch-volume independence verified ✅

### 📚 LESSONS LEARNED: Over-Engineering vs. Simplicity
- **Trust the framework:** Unity's serialization often works better than custom init
- **Editor-first design:** Respect user configurations in Inspector
- **Debug-driven development:** Comprehensive analysis tools improve decision making
- **Question assumptions:** Natural correlations (pitch-volume) can mislead developers
- **Simple is better:** Removing code can be more valuable than adding it

**STATUS:** Code cleanup completed, debugging tools enhanced, unnecessary complexity removed! 🚀

## Day 7: InitialAudioDelay System Implementation 🎵

### ✅ MAJOR IMPROVEMENT: InitialAudioDelay System
**PERFECT FIRST-TIME SYNC:** Audio starts immediately, visuals delayed to compensate for Unity audio latency
- **Problem solved:** Unity has slight delay between Audio.Play() and audible sound
- **Old approach:** Delayed audio start until cubes scrolled (InitialAudioTriggerOffset)
- **New approach:** Audio starts immediately, cube movement delayed by configurable amount
- **First-time exception:** Only the very first audio play uses this delay system
- **All subsequent loops:** Continue using the existing event-based system (works perfectly)

#### Technical Implementation:

**ChorusingManager Changes:**
- **initialAudioDelay parameter:** Configurable delay (default 0.5s) for visual start
- **Immediate audio start:** Audio.Play() called immediately in StartChorusing()
- **Delayed visual updates:** UpdateSimpleVisualization() waits for initialAudioDelay
- **hasDelayedStart flag:** Tracks when visual delay period has ended
- **Removed TriggerInitialAudio():** No longer needed for first audio start

**PitchVisualizer Changes:**
- **Removed InitialAudioTriggerOffset:** No longer needed in settings
- **Removed OnInitialAudioTrigger:** Event no longer used
- **Kept OnAudioLoopTrigger:** Still used for all subsequent loops
- **Simplified audio triggers:** Only loop triggers remain

#### Key Benefits:
- **Perfect first sync:** Audio and visual timing matched from the start
- **Simple implementation:** Minimal changes to existing working system
- **Configurable timing:** Can adjust initialAudioDelay for different systems
- **Exception handling:** First audio play treated as special case
- **Event system preserved:** All loop functionality remains unchanged

### 🎯 CONFIGURATION OPTIONS
**FINE-TUNING FIRST AUDIO SYNC:** Adjustable delay for perfect initial timing

    [Header("Audio Timing")]
    [Tooltip("Delay before starting cube movement to compensate for Unity audio start delay")]
    [SerializeField] private float initialAudioDelay = 0.5f;

**Usage Examples:**
- **initialAudioDelay = 0.3s:** For systems with faster audio processing
- **initialAudioDelay = 0.5s:** Default setting for most systems
- **initialAudioDelay = 0.7s:** For systems with slower audio processing

### ✅ WORKING FEATURES CONFIRMED
**ENHANCED AUDIO TIMING:** Perfect sync from first play
- **Immediate audio start:** Audio.Play() called without waiting ✅
- **Delayed visual start:** Cube movement starts after configurable delay ✅
- **Loop event system:** All subsequent loops use existing event system ✅
- **Clean architecture:** Minimal changes to proven working system ✅
- **Configurable timing:** Easy adjustment for different hardware ✅

### 🏗️ ARCHITECTURE STATE AFTER INITIALAUDIODELAY

#### ChorusingManager (Audio Control + Timing):
- **Immediate audio start:** Audio.Play() in StartChorusing() without delay
- **Visual delay logic:** UpdateSimpleVisualization() waits for initialAudioDelay
- **hasDelayedStart flag:** Tracks visual delay period completion
- **Event subscription:** Only subscribes to OnAudioLoopTrigger (not initial)
- **Clean timing control:** First audio special case, loops use events

#### PitchVisualizer (Visual System + Loop Events):
- **Removed initial trigger:** No longer generates OnInitialAudioTrigger
- **Kept loop triggers:** OnAudioLoopTrigger still used for all loops
- **Simplified settings:** initialAudioTriggerOffset removed
- **Loop-only events:** CheckForLoopTriggers() handles all repetitions

### 🎯 SUCCESS CRITERIA MET
**PERFECT INITIAL SYNC ACHIEVED:** First audio play now perfectly synchronized
- **Eliminates first-play lag:** Audio starts immediately, visuals compensate ✅
- **Maintains loop system:** All subsequent loops use proven event system ✅
- **Simple configuration:** One parameter to adjust timing ✅
- **Clean implementation:** Minimal changes to existing architecture ✅
- **Hardware adaptable:** Can adjust delay for different systems ✅

**STATUS:** InitialAudioDelay system successfully implemented and working perfectly! 🚀

## Day 7 Earlier Achievement: Event-Based Audio Triggering System 🎵

### ✅ BREAKTHROUGH: Event-Based Audio Control Architecture
**MAJOR SYSTEM REDESIGN:** Audio playback now controlled by visual cube scrolling events
- **Event-driven architecture:** Visual system triggers audio events instead of time-based coordination
- **Perfect synchronization:** Audio starts exactly when visual cubes reach trigger points
- **Clean separation:** ChorusingManager handles audio, PitchVisualizer handles visual + events

#### Key Technical Implementation:

**PitchVisualizer Audio Events:**
- **OnAudioLoopTrigger:** Fired when approaching end of each repetition for next loop
- **CheckForLoopTriggers():** Monitors totalElapsedCubes vs trigger offsets
- **Audio trigger tracking:** Prevents duplicate triggers with triggeredLoops HashSet

**ChorusingManager Event Handlers:**
- **TriggerAudioLoop():** Restarts audio for next loop cycle
- **Event subscription:** Clean subscribe/unsubscribe pattern in StartChorusing()/StopChorusing()
- **hasAudioStarted flag:** Prevents multiple triggers

#### Architecture Benefits:
- **Visual-audio sync:** Audio timing driven by actual visual cube positions
- **Configurable precision:** Trigger offsets allow fine-tuning of timing
- **Event safety:** Duplicate trigger prevention and proper cleanup
- **State management:** Clear audio state tracking

### 🎯 WORKING FEATURES CONFIRMED
**SOLID EVENT FOUNDATION:** Core event-based audio system working
- **Loop audio triggering:** Subsequent loops triggered by LoopAudioTriggerOffset ✅
- **Event subscription safety:** Proper subscribe/unsubscribe without memory leaks ✅
- **Audio state management:** Reliable audio start/stop control ✅
- **Repetitions + events:** Event system works with repetitions visual system ✅

### 📐 CONFIGURATION OPTIONS
**FINE-TUNING LOOP SYNC:** Adjustable trigger offset for perfect timing

    [Header("Audio Loop Trigger Settings")]
    [Tooltip("Cubes before focal point to trigger audio loops")]
    public int loopAudioTriggerOffset = 1;

**Usage Examples:**
- **loopAudioTriggerOffset = 0:** Next loop starts exactly when current loop ends
- **loopAudioTriggerOffset = 1:** Next loop starts 1 cube before current loop ends (seamless transition)

### 🎯 SUCCESS CRITERIA MET
**MAJOR MILESTONE ACHIEVED:** Event-based audio control system complete
- **Perfect loop transitions:** Audio loops triggered by visual repetition system ✅
- **Clean architecture:** Clear separation between audio control and visual system ✅
- **Maintainable code:** Event pattern easier to debug and extend ✅
- **Configuration flexibility:** Adjustable trigger offsets for different use cases ✅

**STATUS:** Event-based audio triggering system successfully implemented and working! 🚀

## Day 6 Achievements 🎯

### 1. ✅ CRITICAL BREAKTHROUGH: Repetitions System Implemented
**MAJOR ARCHITECTURE CHANGE:** Native recordings now use repetitions instead of maxCubes limit
- **Multiple repetitions visible:** 5 complete audio loops shown simultaneously
- **Silence gaps between repetitions:** Visual breathing pauses between each loop
- **Infinite scrolling:** Old repetitions removed left, new ones added right
- **No maxCubes limit:** Audio length no longer constrained by arbitrary cube count
- **Perfect for short audio:** 3.9s clips now show properly without off-screen issues

#### Key Technical Implementation:
- **RepetitionData class:** Manages cubes for each complete audio loop + silence
- **Dynamic repetition management:** Add/remove repetitions as they scroll
- **Silence cubes:** Gray cubes represent breathing pauses between repetitions
- **Smooth scrolling:** All repetitions move together as continuous timeline

### 2. ✅ QUANTIZED SILENCE SYNCHRONIZATION
**PERFECT AUDIO-VISUAL SYNC:** Mathematically precise silence duration matching
- **Quantization formula:** round(requestedSilence / analysisInterval) × analysisInterval
- **Example:** 0.67s → round(0.67/0.1) × 0.1 = 0.7s (exactly 7 cube intervals)
- **Single source of truth:** ChorusingManager owns silence, passes to visualizer
- **Update-based audio loop:** No coroutines, safer timing control

#### Architecture Changes:
- **ChorusingManager:** Calculates quantized silence, manages audio timing with real pauses
- **PitchVisualizer:** Receives silence as parameter, creates matching visual cubes
- **Removed silenceBetweenReps:** No longer in VisualizationSettings, centralized in ChorusingManager
- **Zero settings conflicts:** Clean separation between audio control and visual display

### 3. ⚠️ CURRENT ISSUE: Gradual Sync Drift
**SYMPTOM:** Over time, visual cubes start before audio restarts after silence
**SUSPECTED CAUSES:**
- Different analysis intervals between ChorusingManager vs PitchVisualizer
- Frame rate dependency in visual scrolling vs exact audio timing
- Rounding errors accumulating over multiple loops
- Audio uses Time.time precision, visual uses modulo operations

#### Debug Investigation Needed:

    // Add this to ChorusingManager.UpdateNativeVisualization():
    if (enableDebugLogging && Time.time % 5f < 0.1f)
    {
        float audioTotalLoop = nativeClip.length + quantizedSilenceDuration;
        float audioLoopPos = playbackTime % audioTotalLoop;
        DebugLog($"DRIFT: audioLoopPos={audioLoopPos:F3}s, silence={isInSilencePeriod}, elapsed={Time.time - chorusingStartTime:F1}s");
    }

### 4. 📋 ARCHITECTURE STATE

#### ChorusingManager (Audio Control):
- **Owns silence duration:** requestedSilenceDuration → quantizedSilenceDuration
- **Update-based timing:** No coroutines, safer state management
- **Quantization logic:** Ensures cube-perfect silence duration
- **Audio loop control:** Manual looping with precise silence pauses

#### PitchVisualizer (Visual System):
- **Repetitions system:** Shows 5 complete audio loops simultaneously
- **External silence parameter:** Receives quantized silence via PreRenderNativeTrack()
- **Stores currentSilenceDuration:** For dynamic repetition creation in ManageRepetitions()
- **Clean separation:** No longer owns silence timing, only visual representation

### 5. ✅ WORKING FEATURES CONFIRMED
**SOLID FOUNDATION:** Core systems working with new architecture
- **Repetitions visualization:** Multiple loops with silence gaps ✅
- **Quantized silence:** Mathematical precision between audio/visual ✅
- **Personal pitch range system:** Individual voice calibration ✅
- **Update-based audio:** No coroutine timing issues ✅
- **Clean architecture:** Single source of truth for silence ✅

## 🎯 NEXT PRIORITIES for Fresh Session

### Priority 1: CRITICAL - Test Voice Calibration System
**GOAL:** Validate complete calibration workflow from English phrases to Japanese training

#### Testing Steps:
1. **Create CalibrationScene:** Set up UI with microphone dropdown, buttons, progress display
2. **Wire UI components:** Connect TMP_Dropdown, TextMeshPro fields, Slider, Buttons to VoiceRangeCalibrator
3. **Test microphone selection:** Verify dropdown populates with real microphones, filters virtual devices
4. **Test calibration workflow:** English phrases → pitch collection → statistical analysis → settings save
5. **Test scene transition:** CalibrationScene → TestScene2 with settings persistence
6. **Validate settings application:** Verify PersonalPitchRange affects both cube height and color

### Priority 2: CRITICAL - Fix Sync Drift Issue
**GOAL:** Eliminate gradual timing drift between audio and visual

#### Investigation Steps:
1. **Verify analysis intervals match:** ChorusingManager.analysisInterval = PitchVisualizer.settings.analysisInterval
2. **Add drift measurement logging:** Track audioLoopPos vs expected timing over time
3. **Test frame-independent timing:** Consider FixedUpdate() for visual scrolling
4. **Measure drift accumulation rate:** How much drift per minute/loop?

#### Potential Solutions:
- **Option A:** Make visual system time-based instead of frame-based
- **Option B:** Synchronize both systems to same Update frequency
- **Option C:** Add periodic re-sync mechanism to correct drift

### Priority 3: Code Cleanup & Optimization
**GOAL:** Remove legacy code and optimize performance

#### Cleanup Tasks:
- **Remove legacy preRenderedCubes system:** Clean up old maxCubes-based code
- **Clean up unused variables:** Remove nativeCubeOffset and other legacy vars
- **Optimize repetition management:** Ensure efficient cube creation/destruction
- **Update documentation:** Clean up comments and context

### Priority 4: Long-term Stability Testing
**GOAL:** Test with various audio lengths and extended sessions

#### Testing Scenarios:
- **Short audio (2-5s):** Verify repetitions work correctly
- **Long audio (30s+):** Test with longer clips
- **Extended sessions (10+ minutes):** Measure drift over time
- **Different frame rates:** Test performance impact

## LESSONS LEARNED: Voice Calibration Architecture

### ✅ What WORKED:
- **English-based approach:** Practical solution for users without Japanese experience
- **Event-driven design:** More robust than direct API dependencies
- **Statistical validation:** Outlier removal and quality scoring ensure reliable results
- **Cross-platform thinking:** PlayerPrefs provides universal storage solution
- **PersonalPitchRange colors:** Consistent mapping improves user experience

### 🎯 What IMPROVED:
- **Visual continuity:** Extended cube lifetime prevents abrupt disappearing
- **Color resolution:** Personal pitch range provides better color differentiation
- **Modern architecture:** Event-based, modular, maintainable calibration system
- **Settings persistence:** Seamless cross-platform voice range storage

### 🎯 Success Criteria for Next Session:
- **Calibration testing:** Complete workflow validation from UI to settings
- **Zero drift:** Audio and visual stay synchronized indefinitely
- **Clean code:** No legacy systems or unused variables
- **Performance:** Smooth operation with any audio length
- **Documentation:** Clear architecture notes for future development

**STATUS:** Voice calibration system architecture complete, ready for comprehensive testing! 🎙️