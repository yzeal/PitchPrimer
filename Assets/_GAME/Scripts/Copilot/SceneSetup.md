# Scene Setup Instructions

## Required GameObjects:
1. **MicAnalysis GameObject**
   - Add MicAnalysis.cs script
   - AudioSource wird automatisch hinzugefügt
   
2. **MicrophoneSelector GameObject**
   - Add MicrophoneSelector.cs script
   - Reference MicAnalysis in Inspector
   
3. **UI Canvas**
   - TMP_Dropdown für Mikrofon-Auswahl
   - 2 Buttons (Refresh, Start Analysis)
   - TextMeshPro für Status
   
4. **Cube Prefab**
   - Standard Unity Cube
   - Als Prefab speichern
   
5. **PitchVisualization Parent**
   - Empty GameObject als Container

## Camera Position:
- Position: (15, 5, -10)
- Rotation: (0, 0, 0)
- Für optimale Sicht auf Würfel-Visualisierung