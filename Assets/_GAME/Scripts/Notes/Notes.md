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

## LATEST UPDATE - Day 5 Achievements 🎯

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

### 3. 🎯 FUTURE PLAN: Simple Silence Implementation Strategy
**RECOMMENDED APPROACH:** Clean, simple visual overlay during breathing pauses

#### The New Plan (for future implementation):

**Step 1: Revert Complete (✅ DONE)**
- ✅ PitchVisualizer.cs: Remove isSilenceMode variable, SetSilenceMode() method, ShowSilenceAtFocalPoint() method, complex timing logic
- ✅ ChorusingManager.cs: Remove extended data system, custom looping, SetSilenceMode() calls
- ✅ Back to basic AudioSource.loop = true system

**Step 2: Keep Working Foundation (✅ DONE)**
- ✅ Perfect synchronization: Keep fixed cube positioning
- ✅ Clean architecture: Maintain separation of concerns
- ✅ Basic looping: Standard Unity AudioSource looping

**Step 3: Implement Simple Silence (FUTURE - NOT IMPLEMENTED)**
- **Add configurable silence between loops (real audio pause)**
- **Add simple visual overlay during silence periods**
- **No changes to core scrolling/timing logic**
- **Keep it simple and separate from core systems**

#### Simple Implementation Strategy (for future):
    Recommended Approach:
    1. ChorusingManager: Add custom looping with real audio silence
    2. PitchVisualizer: Add basic visual method for silence overlay (no timing changes)
    3. Use simple flag to indicate silence state
    4. Show silence cubes or dimmed display at focal point only
    5. Keep all timing logic in ChorusingManager
    6. PitchVisualizer only handles appearance, not timing

#### Why Simple Approach is Better:
- **Maintains working foundation:** Don't break what works
- **Minimal complexity:** Visual overlay without timing modification
- **Easy to debug:** Clear separation between audio and visual concerns
- **Robust:** No complex state management or timing synchronization

### 4. ✅ WORKING FEATURES CONFIRMED
**SOLID FOUNDATION:** Core systems working perfectly
- **Personal pitch range system:** Individual voice calibration ✅
- **Focal point visualization:** Perfect cube positioning ✅
- **Audio synchronization:** Cube at focal = current audio ✅
- **Basic looping:** Standard Unity AudioSource.loop ✅
- **Debug logging:** Native recording pitch range analysis ✅

## TECHNICAL STATE for Home Computer

### Current Code Status:
- **PitchVisualizer.cs:** Perfect synchronization, no gaps, clean positioning, NO silence code
- **ChorusingManager.cs:** Basic functionality, standard Unity looping, NO custom silence code
- **Clean foundation:** ALL silence implementation code removed

### Working Features:
- **Audio looping:** Standard Unity AudioSource.loop = true
- **Visual synchronization:** Perfect cube-to-audio alignment
- **No silence pauses:** Audio loops immediately without breaks
- **Stable system:** No complex timing or state management

### Debug Features Available:
- **Native pitch range logging:** Shows actual vs. personal range for optimization
- **Cube positioning debug:** First 5 cubes logged with positions
- **Sync validation:** Audio time vs. visual time comparison
- **Range statistics:** Voice coverage analysis for range optimization

## 🎯 NEXT PRIORITIES for Home Computer

### Priority 1: Simple Silence Implementation (FUTURE FEATURE)
**GOAL:** Add breathing pauses without breaking current synchronization

#### Recommended Implementation:
    1. ChorusingManager: Add custom looping with real audio silence
       - Disable AudioSource.loop
       - Use coroutine to manage play → silence → repeat cycle
       - Configurable silence duration (600ms default)
    
    2. PitchVisualizer: Add simple visual overlay
       - ShowSilenceOverlay() - just visual appearance
       - HideSilenceOverlay() - restore normal appearance
       - NO timing modifications, NO complex state tracking
    
    3. Keep it simple and separate from core visualization timing

### Priority 2: Audio Quality Testing
**GOAL:** Test with high-quality Japanese speech recordings

#### Testing Workflow:
    1. Import high-quality WAV files (Japanese speech samples)
    2. Use debug logging to analyze native recording pitch ranges
    3. Optimize personal pitch ranges based on actual speaker data
    4. Test with various audio lengths and speaker types
    5. Validate synchronization remains perfect

### Priority 3: Parameter Optimization
**GOAL:** Fine-tune for real Japanese speech patterns

#### Areas to Optimize:
    - Personal pitch ranges based on real speaker data
    - Sibilant filtering (consider 80-400Hz limit for microphone)
    - Noise gate settings for clean Japanese speech
    - Analysis interval timing for different speech speeds

## LESSONS LEARNED: Silence Implementation

### ❌ What NOT to do:
- **Complex timing manipulation:** Don't modify core visualization timing
- **Extended data systems:** Don't add virtual silence to pitch data
- **State mode tracking:** Don't add complex silence state management
- **Timing synchronization:** Don't try to sync multiple timing systems

### ✅ What TO do:
- **Keep working foundation:** Don't break what works
- **Simple audio approach:** Custom looping with real audio silence
- **Simple visual overlay:** Add appearance-only changes
- **Single source of truth:** Keep timing logic in one place
- **Separation of concerns:** Audio timing ≠ visual appearance

**STATUS:** Clean foundation restored, NO silence features implemented, ready for simple approach! 🚀

## Day 4 Achievements 🎯

### 1. ✅ MAJOR: Personal Pitch Range System Implemented
**BREAKTHROUGH:** Individual voice calibration system for fair scoring and learning
- **PersonalPitchRange class:** Individual min/max pitch settings per visualizer
- **Manual calibration only:** NO automatic adaptation that could learn user mistakes
- **Visual mapping:** Personal pitch range maps to cube height range (0-100%)
- **Out-of-range handling:** Clamp, Hide, or Highlight pitches outside range
- **Voice type presets:** Easy setup for male/female/child voices