using UnityEngine;
using UnityEngine.InputSystem;

// COPILOT CONTEXT: Input manager for user recording visibility control
// Handles cross-platform input (mouse, keyboard, touch) for showing/hiding user cubes
// Uses Unity Input System for flexibility and future customization

public class UserRecordingInputManager : MonoBehaviour
{
    [Header("Input Settings")]
    [SerializeField] private UserRecordingInputActions inputActions;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = false;
    [SerializeField] private bool showInputStatus = false; // For OnGUI debug display
    
    // Events for clean decoupling
    public System.Action OnRecordingStarted;
    public System.Action OnRecordingStopped;
    
    // Input state
    private bool isRecordingPressed = false;
    private bool wasRecordingPressed = false;
    
    // Input action reference
    private InputAction startRecordingAction;
    
    void Awake()
    {
        // Initialize input actions
        if (inputActions == null)
        {
            inputActions = new UserRecordingInputActions();
        }
    }
    
    void OnEnable()
    {
        // Enable input actions and subscribe to events
        inputActions?.Enable();
        
        startRecordingAction = inputActions?.Recording.StartRecording;
        if (startRecordingAction != null)
        {
            startRecordingAction.started += OnRecordingInputStarted;
            startRecordingAction.canceled += OnRecordingInputCanceled;
        }
        
        DebugLog("Input system enabled");
    }
    
    void OnDisable()
    {
        // Clean up subscriptions
        if (startRecordingAction != null)
        {
            startRecordingAction.started -= OnRecordingInputStarted;
            startRecordingAction.canceled -= OnRecordingInputCanceled;
        }
        
        inputActions?.Disable();
        DebugLog("Input system disabled");
    }
    
    void Update()
    {
        // Track state changes for event firing
        wasRecordingPressed = isRecordingPressed;
        
        // Update current state from input system
        isRecordingPressed = startRecordingAction?.IsPressed() ?? false;
        
        // Fire events on state changes
        if (isRecordingPressed && !wasRecordingPressed)
        {
            OnRecordingStarted?.Invoke();
            DebugLog("Recording started via input");
        }
        else if (!isRecordingPressed && wasRecordingPressed)
        {
            OnRecordingStopped?.Invoke();
            DebugLog("Recording stopped via input");
        }
    }
    
    private void OnRecordingInputStarted(InputAction.CallbackContext context)
    {
        DebugLog($"Recording input started: {context.control.name}");
    }
    
    private void OnRecordingInputCanceled(InputAction.CallbackContext context)
    {
        DebugLog($"Recording input canceled: {context.control.name}");
    }
    
    // Public properties for external queries
    public bool IsRecordingPressed => isRecordingPressed;
    public bool IsInputSystemActive => inputActions != null && inputActions.asset.enabled;
    
    // Manual control methods for testing/debugging
    public void SimulateRecordingStart()
    {
        if (!isRecordingPressed)
        {
            isRecordingPressed = true;
            OnRecordingStarted?.Invoke();
            DebugLog("Recording started via simulation");
        }
    }
    
    public void SimulateRecordingStop()
    {
        if (isRecordingPressed)
        {
            isRecordingPressed = false;
            OnRecordingStopped?.Invoke();
            DebugLog("Recording stopped via simulation");
        }
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[UserRecordingInputManager] {message}");
        }
    }
    
    // Debug display
    void OnGUI()
    {
        if (showInputStatus)
        {
            GUILayout.BeginArea(new Rect(10, Screen.height - 120, 300, 100));
            GUILayout.Label("=== Recording Input Status ===");
            GUILayout.Label($"Recording Pressed: {isRecordingPressed}");
            GUILayout.Label($"Input System Active: {IsInputSystemActive}");
            GUILayout.Label($"Current Control: {startRecordingAction?.activeControl?.name ?? "None"}");
            GUILayout.EndArea();
        }
    }
    
    void OnDestroy()
    {
        inputActions?.Dispose();
    }
}