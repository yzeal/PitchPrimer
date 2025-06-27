# Japanese Pitch Accent Trainer

## Project Overview
Unity-based minigame for learning Japanese pronunciation with focus on pitch accent and rhythm through chorusing exercises.

## Current Implementation Status

### ✅ Completed Features
- Real-time microphone input capture
- Pitch detection using autocorrelation algorithm
- Real-time visualization with colored cubes
- Microphone selection UI
- Cross-platform compatibility (PC, Mac, iOS, Android)

### 🔧 Core Scripts

#### MicAnalysis.cs
- **Purpose**: Real-time pitch analysis and visualization
- **Key Parameters**:
  - `minFrequency`: 80Hz (minimum human voice)
  - `maxFrequency`: 800Hz (optimized for Japanese pitch accents)
  - `pitchScaleMultiplier`: 1.5f (visual scaling factor)
  - `correlationThreshold`: 0.1f (pitch detection sensitivity)
- **Visualization**: 
  - Color mapping: Red (low) → Violet (high)
  - Height represents pitch using logarithmic scale
  - 30 cubes maximum, 0.8f spacing

#### MicrophoneSelector.cs
- **Purpose**: UI for microphone device selection
- **Features**: Filters out virtual audio devices (Oculus, VR, etc.)
- **UI Components**: Dropdown, buttons, status text

### 🎯 Current Working State
- Pitch detection functional with autocorrelation algorithm
- Real-time cube visualization working
- Microphone selection prevents virtual devices
- Optimized parameters for Japanese speech (80-800Hz range)

### 🔄 Next Development Steps
1. Audio clip comparison system
2. Japanese native speaker sample integration
3. Scoring/feedback mechanism
4. Gamification elements
5. UI/UX improvements

### 🐛 Known Issues
- Initial pitch scale multiplier was too small (0.01 → 1.5f)
- Debug logging can be reduced for production

### 🔧 Technical Notes
- Uses Unity 6.1
- C# 9.0, .NET Framework 4.7.1
- RequireComponent(AudioSource) for MicAnalysis
- Logarithmic pitch scaling for better perception
- Hann windowing for spectral analysis