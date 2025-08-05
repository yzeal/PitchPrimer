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

## LATEST UPDATE - Day 12: Integration Testing & UI Polish Planning 🎨

### 🎯 INTEGRATION TESTING PHASE
**COMPREHENSIVE SYSTEM VALIDATION:** Complete end-to-end testing of all implemented systems
- **Full workflow testing:** Chorusing → Recording → Scoring → Navigation loop
- **Edge case validation:** Short recordings, missing files, invalid states
- **Cross-component integration:** Verify all managers work together seamlessly
- **Performance profiling:** Audio latency, UI responsiveness, memory usage

#### Planned Testing Areas:

    End-to-End Workflow:
    1. GameStateManager startup → MainMenu state
    2. Transition to Chorusing → Audio playback + visualization
    3. User recording → Audio capture + file saving
    4. Automatic scoring transition → Canvas switch + analysis
    5. Score display → Pitch/rhythm scores + error handling
    6. Navigation controls → Again/Next functionality

### 🎨 UI POLISH PLANNING
**PROFESSIONAL VISUAL DESIGN:** Enhance user interface for production quality
- **Visual hierarchy:** Clear separation between native and user visualizations
- **Color schemes:** Consistent theming across all canvases
- **Animation polish:** Smooth transitions and professional easing curves
- **Responsive design:** Proper layout for different screen sizes
- **Accessibility:** Clear labels, adequate contrast, intuitive controls

#### Planned UI Enhancements:

    ScoringCanvas Layout:
    - Header: "PITCH ACCENT ANALYSIS"
    - Native section: Clear labeling with audio duration
    - User section: Recording quality indicators
    - Score area: Color-coded results with progress bars
    - Navigation: Prominent Again/Next buttons
    - Status: Clear loading and error messaging

### 📊 PERFORMANCE OPTIMIZATION PLANNING
**PRODUCTION-READY PERFORMANCE:** Optimize for smooth real-time operation
- **Audio processing:** Minimize latency in pitch analysis pipeline
- **Visualization rendering:** Efficient cube management and GPU optimization
- **Memory management:** Proper cleanup of audio clips and temporary data
- **State transitions:** Fast canvas switching without frame drops

#### Performance Targets:

    Audio Latency: <50ms from microphone to visualization
    Frame Rate: Stable 60fps during all operations
    Memory Usage: <200MB total allocation
    State Transitions: <100ms canvas switching
    File Operations: <500ms for WAV loading and saving

### 🔧 CONFIGURATION SYSTEM PLANNING
**USER CUSTOMIZATION:** Settings panel for personalized experience
- **Audio settings:** Microphone device selection, input volume adjustment
- **Visualization settings:** Cube size, color mapping, and transition speed
- **Scoring settings:** Thresholds for pitch and rhythm scoring, enable/disable features

#### Planned Configuration Options:

    AudioSettings:
    - MicrophoneDevice (dropdown): Available devices
    - InputVolume (slider): 0% to 100%
    
    VisualizationSettings:
    - CubeSize (slider): 0.1 to 1.0
    - ColorMapping (dropdown): PersonalPitchRange, FixedRainbow
    - TransitionSpeed (slider): 0.1 to 5.0
    
    ScoringSettings:
    - EnablePitchScoring (toggle): On/Off
    - EnableRhythmScoring (toggle): On/Off
    - PitchThreshold (slider): 0 to 100
    - RhythmThreshold (slider): 0 to 100

### 📚 LESSONS LEARNED: Integration Testing and UI Polish
- **End-to-end testing critical:** Isolated testing missed key integration issues
- **UI consistency essential:** Common themes and layouts improve usability
- **Performance optimization pays off:** Smooth interactions prevent user frustration
- **User feedback is vital:** Early and frequent testing with real users uncovers issues
- **Event-driven architecture advantages:** Loose coupling simplifies testing and modification
- **Component isolation:** Independent components are easier to test and debug

#### Planned Testing Areas:
**STATUS:** Integration testing and UI polish planning complete! Next step: Implementation of planned enhancements and thorough testing 🛠️✨