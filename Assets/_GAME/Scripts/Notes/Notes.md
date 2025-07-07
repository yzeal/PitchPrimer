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

## LATEST UPDATE - Day 3 Achievements 🎯

### 1. ✅ MAJOR: Focal Point Visualization System Implemented
**BREAKTHROUGH:** Complete redesign of visualization system with focal point concept
- **GameObject-based focal point:** Visual focal point positioning in Scene view
- **Intuitive setup:** Drag GameObject to set focal point (no magic numbers)
- **Visual indicator:** Yellow sphere shows exact focal point location
- **Perfect for chorusing:** Users focus on one point for pitch comparison

#### New Focal Point Settings in VisualizationSettings:
    focalPointTransform: Transform (drag focal point GameObject here)
    showFocalIndicator: bool (yellow sphere visibility)
    focalIndicatorPrefab: GameObject (optional custom indicator)

### 2. ✅ Synchronized Movement System Fixed
**CRITICAL FIX:** Both tracks now move in same direction for proper synchronization
- **User cubes:** Spawn at focal point, scroll left (newest at focal point)
- **Native cubes:** Scroll left, activate at focal point during playback
- **Same timeline:** Both tracks use consistent left-scrolling movement
- **No more confusion:** Eliminated opposite movement directions

#### Movement Logic:
    User Track: [Past] ← [FOCAL POINT = Newest] ← [Spawning]
    Native:     [Past] ← [FOCAL POINT = Current] ← [Future preview]

### 3. ✅ Three-State Native Cube System
**ENHANCED:** Native cubes show temporal state relative to playback
- **Played (Left of focal):** Full brightness, already played audio
- **Current (At focal):** Extra bright, currently playing audio  
- **Future (Right of focal):** Dim, upcoming audio preview

#### Native Track State Settings:
    playedBrightness: 1.0f (full brightness for past)
    currentBrightness: 1.2f (extra bright for current)
    futureBrightness: 0.3f (dim for future)
    playedAlpha: 0.9f, currentAlpha: 1.0f, futureAlpha: 0.4f

### 4. ✅ Component Architecture Debugging Resolved
**FIXED:** Multiple critical setup issues identified and resolved
- **RefactoredTestManager interference:** Was overriding user visualization settings
- **Cube prefab assignment:** Fixed missing cube prefab after component reset
- **Settings synchronization:** All visualizers now use correct Inspector settings
- **Movement synchronization:** Fixed speed mismatch between user/native tracks

### 5. ✅ Perfect Timeline Synchronization Achieved
**SUCCESS:** User and native recordings now move together seamlessly
- **Same movement speed:** Both tracks scroll at identical rates
- **Synchronized timing:** Analysis intervals properly matched
- **Focal point alignment:** Both tracks reference same focal point position
- **Visual comparison:** Users can directly compare pitches side-by-side

### 6. ✅ Enhanced Scene Setup Process
**IMPROVED:** Clear GameObject-based setup workflow
- **FocalPointMarker:** Empty GameObject for positioning focal point
- **Visual in Scene view:** See exactly where comparison happens
- **Easy adjustment:** Move GameObject to change focal point location
- **Both tracks reference:** Same focal point GameObject for consistency

#### Required Scene Setup:
    FocalPointMarker (Empty GameObject) → Position where comparison happens
    UserVisualization.focalPointTransform → Drag FocalPointMarker
    NativeVisualization.focalPointTransform → Drag same FocalPointMarker
    Yellow indicator sphere automatically shows focal point location

## Previous Achievements Summary

### Day 2 Achievements
- ✅ Complete Test Scene Setup with separated GameObjects
- ✅ Architecture Improvements (removed duplicate settings)
- ✅ Enhanced Noise Gate Controls with full Inspector exposure
- ✅ Refined Component Integration with proper event systems

### Day 1 Achievements (Core Refactoring)
- ✅ Vollständige Code-Architektur Refactoring
- ✅ PitchAnalyzer.cs (shared analysis engine)
- ✅ PitchVisualizer.cs (modular visualization)
- ✅ Event-basierte MicAnalysisRefactored.cs
- ✅ ChorusingManager.cs coordination system

## 🔄 NEXT PRIORITIES for Work Computer

### 1. 🎯 High Priority: Frequency Range Limiting
**IDENTIFIED ISSUE:** Microphone detecting false high pitches from noise
- **Problem:** s/sh sounds creating harmonics above speech range
- **Solution needed:** Limit pitch detection to speech-relevant frequencies
- **Target range:** 80-400Hz (tighter than current 80-800Hz)
- **Implementation:** Add frequency filtering to PitchAnalyzer

#### Suggested approach:
    In PitchAnalysisSettings:
    maxFrequency: 400f (reduced from 800f for cleaner detection)
    
    Alternative: Add separate speechMaxFrequency setting
    speechMaxFrequency: 400f (for microphone input)
    analysisMaxFrequency: 800f (for native audio analysis)

### 2. 🧪 Testing with Multiple Clips
**CONTINUE:** Test focal point system with various Japanese recordings
- **Verify consistency:** Different clip lengths and content
- **Check synchronization:** Various speech patterns and timing
- **Monitor performance:** Frame rate with different clip complexities
- **User experience:** Focal point positioning feels natural

### 3. 🎨 Visual Refinements
**POLISH:** Fine-tune focal point system based on testing
- **Focal point position:** Optimize for best user experience
- **State transitions:** Smooth brightness changes for native cubes
- **Indicator visibility:** Adjust focal point marker as needed
- **Color tuning:** Ensure good contrast between tracks

### 4. 🔧 Advanced Features Planning
**PREPARE:** Next-level functionality for pitch training
- **Accuracy scoring:** Compare user vs native pitch patterns
- **Visual feedback:** Show pitch matching quality in real-time
- **Training modes:** Different difficulty levels and exercises
- **Progress tracking:** Save and analyze user improvement

## 🎯 Current Technical Status

### ✅ WORKING PERFECTLY:
1. **Focal Point System:** GameObject-based positioning with visual indicator
2. **Synchronized Movement:** Both tracks scroll left consistently  
3. **Three-State Natives:** Past/Current/Future visual feedback
4. **User Experience:** Natural focal point for pitch comparison
5. **Component Setup:** Clean Inspector settings, no duplicate configs
6. **Scene Setup:** Visual, intuitive GameObject-based workflow

### 🔧 NEEDS ATTENTION:
1. **Frequency Filtering:** Limit mic input to speech range (80-400Hz)
2. **Noise Handling:** Better filtering of fricative sounds (s/sh)
3. **Testing Coverage:** More Japanese audio clips for validation
4. **Performance Monitoring:** Ensure smooth operation with complex audio

### 📋 SCENE SETUP CHECKLIST:
    ✅ UserVisualization + NativeVisualization GameObjects
    ✅ FocalPointMarker GameObject positioned in scene
    ✅ Both visualizers reference same focal point Transform
    ✅ Cube prefabs assigned to visualizers
    ✅ Native audio clip assigned to ChorusingManager
    ✅ UI buttons wired to Start/Stop chorusing
    ✅ Microphone selection working
    ✅ RefactoredTestManager disabled (to avoid interference)

## Implementation Notes for Work Computer

### Focal Point System Architecture:
    PitchVisualizer.UpdateFocalPoint() → Calculates local position from Transform
    PitchVisualizer.CreateFocalIndicator() → Yellow sphere at focal point
    User cubes: Spawn at focal point, scroll left
    Native cubes: Three-state system relative to focal point position

### Key Methods Added/Modified:
    UpdateUserCubePositions() → Fixed user cube positioning logic
    UpdateAllNativeCubeStates() → Three-state system for native cubes  
    ScrollNativeCubesDiscrete() → Consistent left movement
    SetNativeCubeStateByType() → Enum-based state management
    CubeState enum → {Played, Current, Future}

### Debug Resolution Process:
1. **Identified RefactoredTestManager interference:** Disabled in scene
2. **Fixed cube prefab assignment:** Reassigned after component reset
3. **Synchronized movement speeds:** Fixed analysis interval consistency
4. **Focal point calculation:** GameObject Transform to local coordinates

## 🎵 WORKING VISUALIZATION FLOW:

### User Track (Front, Colorful):
    Microphone Input → PitchAnalyzer → Real-time cubes at focal point → Scroll left

### Native Track (Back, Dimmed):  
    Audio File → Pre-analysis → Future cubes (dim) → Current cube (bright at focal) → Past cubes (bright left)

### Perfect Synchronization:
    Both tracks move left at same speed
    Focal point shows "now" moment for both tracks
    User sees exactly what pitch to match
    Natural eye focus point for training

**Status: ✅ Focal point system complete, ✅ Synchronization perfect, 🎯 Ready for frequency filtering improvements!**

## COPILOT CONTEXT for Work Computer:
The focal point visualization system is now fully implemented and working perfectly. Users can position a focal point GameObject in the scene where pitch comparison happens. Both user and native tracks are synchronized to move left consistently, with the focal point showing the "now" moment. The next major priority is implementing frequency range limiting to prevent false high-pitch detection from fricative sounds (s/sh) during microphone input. Current working range is 80-800Hz, but should be limited to 80-400Hz for cleaner speech detection.