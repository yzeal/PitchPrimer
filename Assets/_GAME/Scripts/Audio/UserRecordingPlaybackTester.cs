using UnityEngine;
using System.IO;

// COPILOT CONTEXT: Simplified playback tester for saved user recordings
// Editor-based UI setup instead of programmatic button references
// Clean integration with UserAudioRecorder events

[RequireComponent(typeof(AudioSource))]
public class UserRecordingPlaybackTester : MonoBehaviour
{
    [Header("?? USER RECORDING PLAYBACK TESTER")]
    [Space(10)]
    [Header("Components")]
    [SerializeField] private UserAudioRecorder userRecorder;
    [SerializeField] private AudioSource playbackAudioSource;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = true;
    
    // State
    private AudioClip loadedRecording;
    private bool isPlaying = false;
    
    void Start()
    {
        InitializeComponents();
        RefreshRecordingStatus();
    }
    
    void Update()
    {
        UpdatePlaybackStatus();
    }
    
    private void InitializeComponents()
    {
        // Find UserAudioRecorder if not assigned
        if (userRecorder == null)
        {
            userRecorder = FindFirstObjectByType<UserAudioRecorder>();
        }
        
        // Setup AudioSource
        if (playbackAudioSource == null)
        {
            playbackAudioSource = GetComponent<AudioSource>();
        }
        
        playbackAudioSource.playOnAwake = false;
        playbackAudioSource.loop = false;
        
        // Subscribe to recorder events
        if (userRecorder != null)
        {
            userRecorder.OnRecordingSaved += OnRecordingSaved;
            DebugLog("? Subscribed to UserAudioRecorder events");
        }
        
        DebugLog("UserRecordingPlaybackTester initialized");
    }
    
    private void OnRecordingSaved(string filePath)
    {
        DebugLog($"?? New recording saved: {filePath}");
        RefreshRecordingStatus();
    }
    
    // ? PUBLIC METHODS FOR UI BUTTONS (Editor setup)
    public void PlayRecording()
    {
        if (isPlaying)
        {
            DebugLog("Already playing - stopping first");
            StopRecording();
        }
        
        if (userRecorder == null || !userRecorder.HasSavedRecording)
        {
            DebugLog("?? No saved recording available to play");
            return;
        }
        
        string recordingPath = userRecorder.RecordingFilePath;
        DebugLog($"?? Loading recording from: {recordingPath}");
        
        LoadAndPlayRecording(recordingPath);
    }
    
    public void StopRecording()
    {
        if (playbackAudioSource != null && playbackAudioSource.isPlaying)
        {
            playbackAudioSource.Stop();
            DebugLog("?? Playback stopped");
        }
        
        isPlaying = false;
    }
    
    private void LoadAndPlayRecording(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"[UserRecordingPlaybackTester] Recording file not found: {filePath}");
            return;
        }
        
        // Load WAV file as AudioClip
        StartCoroutine(LoadWAVFile(filePath));
    }
    
    private System.Collections.IEnumerator LoadWAVFile(string filePath)
    {
        // Unity's UnityWebRequestMultimedia for loading WAV files
        string fileURL = "file://" + filePath;
        
        using (var request = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(fileURL, UnityEngine.AudioType.WAV))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                loadedRecording = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(request);
                
                if (loadedRecording != null)
                {
                    playbackAudioSource.clip = loadedRecording;
                    playbackAudioSource.Play();
                    isPlaying = true;
                    
                    var audioInfo = WAVExporter.GetWAVInfo(filePath);
                    string infoText = audioInfo != null ? audioInfo.ToString() : "Unknown format";
                    
                    DebugLog($"?? Playing recording: {infoText}");
                }
                else
                {
                    Debug.LogError("[UserRecordingPlaybackTester] Failed to load AudioClip from WAV file");
                }
            }
            else
            {
                Debug.LogError($"[UserRecordingPlaybackTester] Failed to load WAV: {request.error}");
            }
        }
    }
    
    private void UpdatePlaybackStatus()
    {
        if (isPlaying && playbackAudioSource != null && !playbackAudioSource.isPlaying)
        {
            // Playback finished
            isPlaying = false;
            DebugLog("? Playback finished");
        }
    }
    
    private void RefreshRecordingStatus()
    {
        if (userRecorder == null)
        {
            DebugLog("? No recorder found");
            return;
        }
        
        if (userRecorder.HasSavedRecording)
        {
            var audioInfo = WAVExporter.GetWAVInfo(userRecorder.RecordingFilePath);
            if (audioInfo != null)
            {
                DebugLog($"?? Recording available: {audioInfo}");
            }
            else
            {
                DebugLog("?? Recording file available");
            }
        }
        else
        {
            DebugLog("?? No recording available");
        }
    }
    
    // Public properties for external queries
    public bool IsPlaying => isPlaying;
    public bool HasRecording => userRecorder != null && userRecorder.HasSavedRecording;
    public string RecordingInfo => userRecorder != null ? WAVExporter.GetWAVInfo(userRecorder.RecordingFilePath)?.ToString() : "No recorder";
    
    private void DebugLog(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[UserRecordingPlaybackTester] {message}");
        }
    }
    
    void OnDestroy()
    {
        // Cleanup subscriptions
        if (userRecorder != null)
        {
            userRecorder.OnRecordingSaved -= OnRecordingSaved;
        }
        
        // Stop playback
        if (isPlaying)
        {
            StopRecording();
        }
    }
}