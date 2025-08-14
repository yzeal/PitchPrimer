using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ChorusingManager))]
public class ChorusingManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        ChorusingManager manager = (ChorusingManager)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("?? Delay Testing Controls", EditorStyles.boldLabel);
        
        // Status anzeigen
        bool isActive = Application.isPlaying && manager.IsChorusingActive;
        string statusText = Application.isPlaying ? 
            (isActive ? "? Chorusing Active" : "?? Chorusing Inactive") :
            "?? Not in Play Mode";
        
        EditorGUILayout.HelpBox(statusText, isActive ? MessageType.Info : MessageType.Warning);
        
        // Aktuelle Werte anzeigen
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Current Values:");
            EditorGUILayout.LabelField($"Initial Delay: {manager.GetInitialAudioDelay():F3}s ({manager.GetInitialDelayCubeCount()} cubes)");
            EditorGUILayout.LabelField($"Loop Delay: {manager.GetLoopAudioDelay():F3}s ({manager.GetLoopDelayCubeCount()} cubes)");
        }
        
        EditorGUILayout.Space();
        
        // Restart Button
        GUI.enabled = Application.isPlaying && isActive;
        
        if (GUILayout.Button("?? Restart Chorusing\n(Apply Current Inspector Values)", GUILayout.Height(50)))
        {
            manager.RestartChorusing();
            Debug.Log("?? Chorusing restarted via Editor button");
        }
        
        GUI.enabled = true;
        
        EditorGUILayout.Space();
        
        // Instructions
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("?? Instructions:\n" +
                                  "1. Enter Play Mode\n" +
                                  "2. Start Chorusing in game\n" +
                                  "3. Adjust Initial/Loop Delay values above\n" +
                                  "4. Click 'Restart Chorusing' to apply changes", MessageType.Info);
        }
        else if (!isActive)
        {
            EditorGUILayout.HelpBox("?? Start chorusing in the game first, then use the restart button to test delay changes!", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("?? Testing Active!\n" +
                                  "• Modify delay values in inspector above\n" +
                                  "• Click 'Restart' to apply instantly\n" +
                                  "• Listen for audio-visual sync changes", MessageType.Info);
        }
    }
}