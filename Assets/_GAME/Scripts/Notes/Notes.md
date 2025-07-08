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

## LATEST UPDATE - Day 4 Achievements 🎯

### 1. ✅ MAJOR: Personal Pitch Range System Implemented
**BREAKTHROUGH:** Individual voice calibration system for fair scoring and learning
- **PersonalPitchRange class:** Individual min/max pitch settings per visualizer
- **Manual calibration only:** NO automatic adaptation that could learn user mistakes
- **Visual mapping:** Personal pitch range maps to cube height range (0-100%)
- **Out-of-range handling:** Clamp, Hide, or Highlight pitches outside range
- **Voice type presets:**