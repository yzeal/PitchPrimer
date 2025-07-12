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

## LATEST UPDATE - Day 6: Repetitions System & Quantized Silence 🎯

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

### Priority 1: CRITICAL - Fix Sync Drift Issue
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

### Priority 2: Code Cleanup & Optimization
**GOAL:** Remove legacy code and optimize performance

#### Cleanup Tasks:
- **Remove legacy preRenderedCubes system:** Clean up old maxCubes-based code
- **Clean up unused variables:** Remove nativeCubeOffset and other legacy vars
- **Optimize repetition management:** Ensure efficient cube creation/destruction
- **Update documentation:** Clean up comments and context

### Priority 3: Long-term Stability Testing
**GOAL:** Test with various audio lengths and extended sessions

#### Testing Scenarios:
- **Short audio (2-5s):** Verify repetitions work correctly
- **Long audio (30s+):** Test with longer clips
- **Extended sessions (10+ minutes):** Measure drift over time
- **Different frame rates:** Test performance impact

## LESSONS LEARNED: Quantized Silence Implementation

### ✅ What WORKED:
- **Mathematical precision:** Quantization ensures perfect cube alignment
- **Centralized control:** Single source of truth eliminates conflicts
- **Update-based timing:** More reliable than coroutines
- **External parameter passing:** Clean separation of concerns

### ⚠️ What NEEDS FIXING:
- **Timing precision:** Small accumulated errors cause drift
- **Frame dependency:** Visual system tied to Update() frequency
- **Analysis interval sync:** Ensure both systems use identical values

### 🎯 Success Criteria for Next Session:
- **Zero drift:** Audio and visual stay synchronized indefinitely
- **Clean code:** No legacy systems or unused variables
- **Performance:** Smooth operation with any audio length
- **Documentation:** Clear architecture notes for future development

**STATUS:** Repetitions system implemented, quantized silence working, sync drift investigation needed! 🚀

## Day 5 Achievements 🎯 (SUPERSEDED by Day 6)

### 1. ✅ CRITICAL FIX: Audio-Visual Synchronization Perfected
**BREAKTHROUGH:** Cube at focal point now represents currently playing audio
- **Fixed initial positioning:** Cube 0 (timestamp=0) now correctly positioned at focal point
- **Consistent cube spacing:** Eliminated gaps between first and subsequent loops
- **Short audio handling:** Fixed positioning issues for audio shorter than maxCubes
- **Perfect synchronization:** Focal point cube = current audio being played

#### Key Technical Fixes Applied:
- **CreateInitialNativeWindow():** Position cubes relative to focal point instead of array indices
- **AddSimpleNativeCube():** Use actual data length for short audio instead of maxCubes
- **CreateCube():** Separate positioning logic for user vs pre-rendered cubes
- **Eliminated double positioning:** Removed conflicting positioning systems

### 2. ❌ SILENCE IMPLEMENTATION: Failed Attempt (FULLY REVERTED)
**LESSON LEARNED:** Complex timing manipulation causes synchronization issues

#### What We Tried (and failed):
- **Extended data system:** Added virtual silence cubes to pitch data
- **Custom looping with real audio silence:** ChorusingManager modifications
- **Silence mode tracking:** Complex state management for visual/audio phases
- **Timing manipulation:** Modified core visualization timing during silence
- **Result:** Sync issues, freezing after first loop, complex debugging

#### Problems with Complex Approach:
- **Timing conflicts:** Audio timing vs visualization timing confusion
- **State management:** Complex silence mode caused edge cases
- **Synchronization drift:** Extended data vs real audio timing mismatch
- **Maintenance issues:** Too many interconnected systems to debug

#### Successfully Reverted to Clean State:
- **PitchVisualizer.cs:** Removed ALL silence-related code (isSilenceMode, SetSilenceMode(), ShowSilenceAtFocalPoint())
- **ChorusingManager.cs:** Removed ALL custom looping and silence code
- **Clean foundation:** Back to working basic looping without any silence features

### 3. 🎯 FUTURE PLAN: Simple Silence Implementation Strategy (SUPERSEDED)
**NOTE:** Day 6 successfully implemented this plan with repetitions system

#### The Plan That Was Implemented:

**Step 1: Revert Complete (✅ DONE)**
- ✅ PitchVisualizer.cs: Remove isSilenceMode variable, SetSilenceMode() method, ShowSilenceAtFocalPoint() method, complex timing logic
- ✅ ChorusingManager.cs: Remove extended data system, custom looping, SetSilenceMode() calls
- ✅ Back to basic AudioSource.loop = true system

**Step 2: Keep Working Foundation (✅ DONE)**
- ✅ Perfect synchronization: Keep fixed cube positioning
- ✅ Clean architecture: Maintain separation of concerns
- ✅ Basic looping: Standard Unity AudioSource looping

**Step 3: Implement Simple Silence (✅ IMPLEMENTED in Day 6)**
- ✅ Add configurable silence between loops (real audio pause)
- ✅ Add simple visual overlay during silence periods (silence cubes)
- ✅ No changes to core scrolling/timing logic
- ✅ Keep it simple and separate from core systems

### 4. ✅ WORKING FEATURES CONFIRMED
**SOLID FOUNDATION:** Core systems working perfectly
- **Personal pitch range system:** Individual voice calibration ✅
- **Focal point visualization:** Perfect cube positioning ✅
- **Audio synchronization:** Cube at focal = current audio ✅
- **Basic looping:** Standard Unity AudioSource.loop ✅
- **Debug logging:** Native recording pitch range analysis ✅

## Day 4 Achievements 🎯

### 1. ✅ MAJOR: Personal Pitch Range System Implemented
**BREAKTHROUGH:** Individual voice calibration system for fair scoring and learning
- **PersonalPitchRange class:** Individual min/max pitch settings per visualizer
- **Manual calibration only:** NO automatic adaptation that could learn user mistakes
- **Visual mapping:** Personal pitch range maps to cube height range (0-100%)
- **Out-of-range handling:** Clamp, Hide, or Highlight pitches outside range
- **Voice type presets:** Easy setup for male/female/child voices