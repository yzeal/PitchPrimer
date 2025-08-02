using UnityEngine;
using System.Collections;

// COPILOT CONTEXT: Central game state management for Japanese pitch accent trainer
// Handles smooth transitions between Chorusing and Scoring states
// Manages Canvas visibility and component coordination
// Integrates with ScoringManager and ChorusingManager via events

public class GameStateManager : MonoBehaviour
{
    [Header("?? GAME STATE MANAGER - Central State Control")]
    [Space(10)]
    
    [Header("UI Canvases")]
    [SerializeField] private Canvas mainMenuCanvas;
    [SerializeField] private Canvas chorusingCanvas;
    [SerializeField] private Canvas scoringCanvas;
    [SerializeField] private Canvas settingsCanvas;
    [SerializeField] private Canvas calibrationCanvas;
    
    [Header("Core Managers")]
    [SerializeField] private ChorusingManager chorusingManager;
    [SerializeField] private ScoringManager scoringManager;
    
    [Header("State Configuration")]
    [SerializeField] private GameState initialState = GameState.MainMenu; // Sicherstellen dass es MainMenu ist
    [SerializeField] private bool autoStartChorusing = false; // Sollte false sein

    [Header("Transition Settings")]
    [SerializeField] private float transitionDelay = 0.1f;
    [SerializeField] private bool enableSmoothTransitions = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = true;
    
    // Events for external systems
    public System.Action<GameState, GameState> OnStateChanged; // oldState, newState
    public System.Action<GameState> OnStateEntered;
    public System.Action<GameState> OnStateExited;
    
    // Internal state
    private GameState currentState = GameState.MainMenu;
    private GameState previousState = GameState.MainMenu;
    private bool isTransitioning = false;
    
    void Start()
    {
        InitializeComponents();
        InitializeCanvases();
        
        // Start with initial state
        StartCoroutine(DelayedInitialStateTransition());
    }
    
    private IEnumerator DelayedInitialStateTransition()
    {
        // Small delay to ensure all components are initialized
        yield return new WaitForSeconds(0.1f);
        
        // FIXED: Nur auto-start wenn explizit aktiviert UND nicht im Editor-Test-Modus
        if (autoStartChorusing && Application.isPlaying)
        {
            DebugLog("?? Auto-starting Chorusing (enabled in Inspector)");
            TransitionToState(GameState.Chorusing);
        }
        else
        {
            DebugLog($"?? Starting with initial state: {initialState}");
            TransitionToState(initialState);
        }
    }
    
    private void InitializeComponents()
    {
        // Auto-find managers if not assigned
        if (chorusingManager == null)
            chorusingManager = FindFirstObjectByType<ChorusingManager>();
        
        if (scoringManager == null)
            scoringManager = FindFirstObjectByType<ScoringManager>();
        
        // Subscribe to manager events
        if (scoringManager != null)
        {
            scoringManager.OnScoringComplete += () => TransitionToState(GameState.Scoring);
            scoringManager.OnRetryRequested += () => TransitionToState(GameState.Chorusing);
            scoringManager.OnNextRequested += OnNextExerciseRequested;
            
            DebugLog("? Subscribed to ScoringManager events");
        }
        else
        {
            Debug.LogError("[GameStateManager] ScoringManager not found!");
        }
        
        DebugLog("GameStateManager components initialized");
    }
    
    private void InitializeCanvases()
    {
        // Auto-find canvases if not assigned
        if (mainMenuCanvas == null)
            mainMenuCanvas = GameObject.Find("MainMenuCanvas")?.GetComponent<Canvas>();
        
        if (chorusingCanvas == null)
            chorusingCanvas = GameObject.Find("ChorusingCanvas")?.GetComponent<Canvas>();
        
        if (scoringCanvas == null)
            scoringCanvas = GameObject.Find("ScoringCanvas")?.GetComponent<Canvas>();
        
        if (settingsCanvas == null)
            settingsCanvas = GameObject.Find("SettingsCanvas")?.GetComponent<Canvas>();
        
        if (calibrationCanvas == null)
            calibrationCanvas = GameObject.Find("CalibrationCanvas")?.GetComponent<Canvas>();
        
        // Initially hide all canvases
        SetCanvasVisibility(mainMenuCanvas, false);
        SetCanvasVisibility(chorusingCanvas, false);
        SetCanvasVisibility(scoringCanvas, false);
        SetCanvasVisibility(settingsCanvas, false);
        SetCanvasVisibility(calibrationCanvas, false);
        
        DebugLog("Canvases initialized and hidden");
    }
    
    public void TransitionToState(GameState newState)
    {
        if (isTransitioning)
        {
            DebugLog($"?? Transition already in progress, ignoring request to {newState}");
            return;
        }
        
        if (newState == currentState)
        {
            DebugLog($"?? Already in state {newState}, ignoring transition request");
            return;
        }
        
        DebugLog($"?? State transition: {currentState} ? {newState}");
        
        if (enableSmoothTransitions)
        {
            StartCoroutine(SmoothStateTransition(newState));
        }
        else
        {
            ExecuteStateTransition(newState);
        }
    }
    
    private IEnumerator SmoothStateTransition(GameState newState)
    {
        isTransitioning = true;
        
        // Step 1: Exit current state
        ExitCurrentState();
        yield return new WaitForSeconds(transitionDelay);
        
        // Step 2: Update state
        previousState = currentState;
        currentState = newState;
        
        // Step 3: Enter new state
        EnterNewState();
        yield return new WaitForSeconds(transitionDelay);
        
        // Step 4: Notify completion
        OnStateChanged?.Invoke(previousState, currentState);
        OnStateEntered?.Invoke(currentState);
        
        isTransitioning = false;
        
        DebugLog($"? State transition complete: {previousState} ? {currentState}");
    }
    
    private void ExecuteStateTransition(GameState newState)
    {
        isTransitioning = true;
        
        ExitCurrentState();
        
        previousState = currentState;
        currentState = newState;
        
        EnterNewState();
        
        OnStateChanged?.Invoke(previousState, currentState);
        OnStateEntered?.Invoke(currentState);
        
        isTransitioning = false;
        
        DebugLog($"? Instant state transition: {previousState} ? {currentState}");
    }
    
    private void ExitCurrentState()
    {
        DebugLog($"?? Exiting state: {currentState}");
        
        OnStateExited?.Invoke(currentState);
        
        switch (currentState)
        {
            case GameState.MainMenu:
                ExitMainMenu();
                break;
            case GameState.Chorusing:
                ExitChorusing();
                break;
            case GameState.Scoring:
                ExitScoring();
                break;
            case GameState.Settings:
                ExitSettings();
                break;
            case GameState.Calibration:
                ExitCalibration();
                break;
        }
        
        // Hide current canvas
        HideCurrentCanvas();
    }
    
    private void EnterNewState()
    {
        DebugLog($"?? Entering state: {currentState}");
        
        switch (currentState)
        {
            case GameState.MainMenu:
                EnterMainMenu();
                break;
            case GameState.Chorusing:
                EnterChorusing();
                break;
            case GameState.Scoring:
                EnterScoring();
                break;
            case GameState.Settings:
                EnterSettings();
                break;
            case GameState.Calibration:
                EnterCalibration();
                break;
        }
        
        // Show new canvas
        ShowCurrentCanvas();
    }
    
    // State Exit Methods
    private void ExitMainMenu()
    {
        DebugLog("?? Exiting Main Menu");
        // Main menu cleanup if needed
    }
    
    private void ExitChorusing()
    {
        DebugLog("?? Exiting Chorusing");
        
        if (chorusingManager != null)
        {
            chorusingManager.StopChorusing();
        }
    }
    
    private void ExitScoring()
    {
        DebugLog("?? Exiting Scoring");
        
        if (scoringManager != null)
        {
            scoringManager.StopScoring();
        }
    }
    
    private void ExitSettings()
    {
        DebugLog("?? Exiting Settings");
        // Settings cleanup if needed
    }
    
    private void ExitCalibration()
    {
        DebugLog("??? Exiting Calibration");
        // Calibration cleanup if needed
    }
    
    // State Enter Methods
    private void EnterMainMenu()
    {
        DebugLog("?? Entering Main Menu");
        // Main menu initialization if needed
    }
    
    private void EnterChorusing()
    {
        DebugLog("?? Entering Chorusing");
        
        if (chorusingManager != null)
        {
            chorusingManager.StartChorusing();
        }
        else
        {
            Debug.LogError("[GameStateManager] Cannot start chorusing - ChorusingManager not found!");
        }
    }
    
    private void EnterScoring()
    {
        DebugLog("?? Entering Scoring");
        
        // Scoring setup is handled by ScoringManager automatically
        // when OnScoringComplete event is fired
    }
    
    private void EnterSettings()
    {
        DebugLog("?? Entering Settings");
        // Settings initialization if needed
    }
    
    private void EnterCalibration()
    {
        DebugLog("??? Entering Calibration");
        // Calibration initialization if needed
    }
    
    // Canvas Management
    private void HideCurrentCanvas()
    {
        switch (currentState)
        {
            case GameState.MainMenu:
                SetCanvasVisibility(mainMenuCanvas, false);
                break;
            case GameState.Chorusing:
                SetCanvasVisibility(chorusingCanvas, false);
                break;
            case GameState.Scoring:
                SetCanvasVisibility(scoringCanvas, false);
                break;
            case GameState.Settings:
                SetCanvasVisibility(settingsCanvas, false);
                break;
            case GameState.Calibration:
                SetCanvasVisibility(calibrationCanvas, false);
                break;
        }
    }
    
    private void ShowCurrentCanvas()
    {
        switch (currentState)
        {
            case GameState.MainMenu:
                SetCanvasVisibility(mainMenuCanvas, true);
                break;
            case GameState.Chorusing:
                SetCanvasVisibility(chorusingCanvas, true);
                break;
            case GameState.Scoring:
                SetCanvasVisibility(scoringCanvas, true);
                break;
            case GameState.Settings:
                SetCanvasVisibility(settingsCanvas, true);
                break;
            case GameState.Calibration:
                SetCanvasVisibility(calibrationCanvas, true);
                break;
        }
    }
    
    private void SetCanvasVisibility(Canvas canvas, bool visible)
    {
        if (canvas != null)
        {
            canvas.gameObject.SetActive(visible);
            DebugLog($"??? Canvas {canvas.name}: {(visible ? "Shown" : "Hidden")}");
        }
    }
    
    // Event Handlers
    private void OnNextExerciseRequested()
    {
        DebugLog("?? Next exercise requested");
        
        // For now, go back to chorusing
        // TODO: Implement exercise progression system
        TransitionToState(GameState.Chorusing);
    }
    
    // Public API for UI buttons
    public void GoToMainMenu()
    {
        TransitionToState(GameState.MainMenu);
    }
    
    public void StartChorusing()
    {
        TransitionToState(GameState.Chorusing);
    }
    
    public void OpenSettings()
    {
        TransitionToState(GameState.Settings);
    }
    
    public void StartCalibration()
    {
        TransitionToState(GameState.Calibration);
    }
    
    public void RetryExercise()
    {
        TransitionToState(GameState.Chorusing);
    }
    
    public void NextExercise()
    {
        OnNextExerciseRequested();
    }
    
    // Testing and Debug Methods
    [ContextMenu("Force Transition to Chorusing")]
    public void ForceTransitionToChorusing()
    {
        TransitionToState(GameState.Chorusing);
    }
    
    [ContextMenu("Force Transition to Scoring")]
    public void ForceTransitionToScoring()
    {
        TransitionToState(GameState.Scoring);
    }
    
    [ContextMenu("Show State Info")]
    public void ShowStateInfo()
    {
        Debug.Log($"[GameStateManager] Current State: {currentState}");
        Debug.Log($"[GameStateManager] Previous State: {previousState}");
        Debug.Log($"[GameStateManager] Is Transitioning: {isTransitioning}");
    }
    
    // Public Properties
    public GameState CurrentState => currentState;
    public GameState PreviousState => previousState;
    public bool IsTransitioning => isTransitioning;
    
    // Canvas Properties (for external access)
    public Canvas ChorusingCanvas => chorusingCanvas;
    public Canvas ScoringCanvas => scoringCanvas;
    public Canvas MainMenuCanvas => mainMenuCanvas;
    public Canvas SettingsCanvas => settingsCanvas;
    public Canvas CalibrationCanvas => calibrationCanvas;
    
    private void DebugLog(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[GameStateManager] {message}");
        }
    }
    
    void OnDestroy()
    {
        // Cleanup subscriptions
        if (scoringManager != null)
        {
            scoringManager.OnScoringComplete -= () => TransitionToState(GameState.Scoring);
            scoringManager.OnRetryRequested -= () => TransitionToState(GameState.Chorusing);
            scoringManager.OnNextRequested -= OnNextExerciseRequested;
        }
    }
}

// NOTE: GameState enum is now defined in separate GameState.cs file