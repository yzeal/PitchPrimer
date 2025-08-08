using UnityEngine;
using UnityEngine.UI;
using TMPro;

// COPILOT CONTEXT: UI controller for scoring screen in Japanese pitch accent trainer
// Displays side-by-side visualization comparison with audio controls and scores
// Integrates with ScoringManager via events for real-time updates

public class ScoringUI : MonoBehaviour
{
    [Header("?? SCORING UI - Results Display & Audio Controls")]
    [Space(10)]
    
    [Header("Score Display")]
    [SerializeField] private TextMeshProUGUI pitchScoreText;
    [SerializeField] private TextMeshProUGUI rhythmScoreText;
    [SerializeField] private TextMeshProUGUI overallScoreText;
    [SerializeField] private Slider pitchScoreSlider;
    [SerializeField] private Slider rhythmScoreSlider;
    [SerializeField] private Slider overallScoreSlider;
    
    [Header("Visualization Areas")]
    [SerializeField] private Transform nativeVisualizationArea;
    [SerializeField] private Transform userVisualizationArea;
    [SerializeField] private TextMeshProUGUI nativeClipLabel;
    [SerializeField] private TextMeshProUGUI userClipLabel;
    
    [Header("Audio Controls - Native Clip")]
    [SerializeField] private Button nativePlayButton;
    [SerializeField] private Button nativeStopButton;
    [SerializeField] private TextMeshProUGUI nativePlayButtonText;
    [SerializeField] private Image nativePlayButtonIcon;
    [SerializeField] private Sprite playIcon;
    [SerializeField] private Sprite stopIcon;
    
    [Header("Audio Controls - User Clip")]
    [SerializeField] private Button userPlayButton;
    [SerializeField] private Button userStopButton;
    [SerializeField] private TextMeshProUGUI userPlayButtonText;
    [SerializeField] private Image userPlayButtonIcon;
    
    [Header("Navigation Controls")]
    [SerializeField] private Button againButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TextMeshProUGUI againButtonText;
    [SerializeField] private TextMeshProUGUI nextButtonText;
    
    [Header("Status Display")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private Slider progressSlider;
    
    [Header("Visual Settings")]
    [SerializeField] private Color goodScoreColor = Color.green;
    [SerializeField] private Color averageScoreColor = Color.yellow;
    [SerializeField] private Color poorScoreColor = Color.red;
    [SerializeField] private float scoreThresholdGood = 75f;
    [SerializeField] private float scoreThresholdAverage = 50f;
    
    [Header("Animation Settings")]
    [SerializeField] private bool enableScoreAnimations = true;
    [SerializeField] private float scoreAnimationDuration = 1f;
    [SerializeField] private AnimationCurve scoreAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = true;
    
    [Header("Error Display")]
    [SerializeField] private GameObject errorPanel; // NEW: Panel for error messages
    [SerializeField] private TextMeshProUGUI errorMessageText; // NEW: Error message text
    [SerializeField] private Button errorRetryButton; // NEW: Retry button in error panel
    [SerializeField] private GameObject scorePanel; // Reference to normal score display panel
    
    // Component references
    private ScoringManager scoringManager;
    private GameStateManager gameStateManager;
    
    // Internal state
    private bool isInitialized = false;
    private bool isNativeClipPlaying = false;
    private bool isUserClipPlaying = false;
    private ScoringResults currentScores;
    
    // Animation state
    private Coroutine scoreAnimationCoroutine;
    
    // FIXED: Add state tracking for proper button management
    private bool isLoadingActive = false;
    private bool hasReceivedScoreOrError = false;
    
    void Start()
    {
        InitializeComponents();
        InitializeUI();
        SubscribeToEvents();
    }
    
    private void InitializeComponents()
    {
        // Auto-find managers
        if (scoringManager == null)
            scoringManager = FindFirstObjectByType<ScoringManager>();
        
        if (gameStateManager == null)
            gameStateManager = FindFirstObjectByType<GameStateManager>();
        
        DebugLog("ScoringUI components initialized");
    }
    
    private void InitializeUI()
    {
        // Setup button listeners
        SetupButtonListeners();
        
        // Initialize UI state
        ResetUIState();
        
        // Setup default text
        SetupDefaultText();
        
        DebugLog("ScoringUI interface initialized");
    }
    
    private void SetupButtonListeners()
    {
        // Native clip controls
        if (nativePlayButton != null)
            nativePlayButton.onClick.AddListener(OnNativePlayButtonClicked);
        
        if (nativeStopButton != null)
            nativeStopButton.onClick.AddListener(OnNativeStopButtonClicked);
        
        // User clip controls
        if (userPlayButton != null)
            userPlayButton.onClick.AddListener(OnUserPlayButtonClicked);
        
        if (userStopButton != null)
            userStopButton.onClick.AddListener(OnUserStopButtonClicked);
        
        // Navigation controls
        if (againButton != null)
            againButton.onClick.AddListener(OnAgainButtonClicked);
        
        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextButtonClicked);
        
        // NEW: Error panel retry button
        if (errorRetryButton != null)
            errorRetryButton.onClick.AddListener(OnErrorRetryButtonClicked);
        
        DebugLog("Button listeners setup complete");
    }
    
    private void SetupDefaultText()
    {
        // Labels
        if (nativeClipLabel != null)
            nativeClipLabel.text = "Native Recording";
        
        if (userClipLabel != null)
            userClipLabel.text = "Your Recording";
        
        // Button text
        if (nativePlayButtonText != null)
            nativePlayButtonText.text = "Play Native";
        
        if (userPlayButtonText != null)
            userPlayButtonText.text = "Play User";
        
        if (againButtonText != null)
            againButtonText.text = "Try Again";
        
        if (nextButtonText != null)
            nextButtonText.text = "Next Exercise";
        
        // Status
        if (statusText != null)
            statusText.text = "Ready for scoring...";
    }
    
    private void SubscribeToEvents()
    {
        if (scoringManager != null)
        {
            scoringManager.OnScoresCalculated += OnScoresCalculated;
            scoringManager.OnClipsLoaded += OnClipsLoaded;
            scoringManager.OnNativeClipPlaybackChanged += OnNativePlaybackChanged;
            scoringManager.OnUserClipPlaybackChanged += OnUserPlaybackChanged;
            scoringManager.OnScoringError += OnScoringError; // NEW: Subscribe to error events
            
            DebugLog("?? Subscribed to ScoringManager events");
        }
        else
        {
            Debug.LogError("[ScoringUI] ScoringManager not found!");
        }
        
        if (gameStateManager != null)
        {
            gameStateManager.OnStateEntered += OnGameStateEntered;
            gameStateManager.OnStateExited += OnGameStateExited;
            
            DebugLog("?? Subscribed to GameStateManager events");
        }
    }
    
    private void ResetUIState()
    {
        // Reset scores
        UpdateScoreDisplay(0, 0, 0);
        
        // Show score panel, hide error panel
        ShowScoreState();
        
        // Reset status
        if (statusText != null)
            statusText.text = "Ready for scoring...";
        
        // Stop any running animations
        if (scoreAnimationCoroutine != null)
        {
            StopCoroutine(scoreAnimationCoroutine);
            scoreAnimationCoroutine = null;
        }
        
        // Reset button states
        UpdateNativePlayButton(false);
        UpdateUserPlayButton(false);
        
        // FIXED: Reset state tracking
        isLoadingActive = false;
        hasReceivedScoreOrError = false;
        
        DebugLog("?? UI state reset for new scoring session");
    }
    
    // Event Handlers
    private void OnScoresCalculated(float pitchScore, float rhythmScore)
    {
        float overallScore = (pitchScore + rhythmScore) / 2f;
        
        DebugLog($"?? Scores received: Pitch={pitchScore:F1}, Rhythm={rhythmScore:F1}, Overall={overallScore:F1}");
        
        // FIXED: Mark that we received a result
        hasReceivedScoreOrError = true;
        
        // NEW: Show score panel (hide error panel if it was showing)
        ShowScoreState();
        
        // FIXED: Ensure canvas is active before starting animation
        if (enableScoreAnimations && gameObject.activeInHierarchy)
        {
            AnimateScoreDisplay(pitchScore, rhythmScore, overallScore);
        }
        else
        {
            DebugLog("?? Canvas inactive or animations disabled - using direct score update");
            UpdateScoreDisplay(pitchScore, rhythmScore, overallScore);
        }
        
        // Update status
        if (statusText != null)
            statusText.text = "Scoring complete!";
        
        // FIXED: End loading state and enable all controls
        SetLoadingState(false);
        
        DebugLog("? Score display complete - all controls should be enabled");
    }
    
    private void OnClipsLoaded(AudioClip nativeClip, AudioClip userClip)
    {
        DebugLog($"?? Clips loaded: Native={nativeClip?.name}, User={userClip?.name}");
        
        // Update clip labels with duration info
        if (nativeClipLabel != null && nativeClip != null)
            nativeClipLabel.text = $"Native Recording ({nativeClip.length:F1}s)";
        
        if (userClipLabel != null && userClip != null)
            userClipLabel.text = $"Your Recording ({userClip.length:F1}s)";
        
        // Note: Don't enable audio controls here - wait for scores or error
        
        // Update status
        if (statusText != null)
            statusText.text = "Audio loaded - analyzing...";
    }
    
    private void OnNativePlaybackChanged(bool isPlaying)
    {
        isNativeClipPlaying = isPlaying;
        UpdateNativePlayButton(isPlaying);
        
        DebugLog($"?? Native playback: {(isPlaying ? "Started" : "Stopped")}");
    }
    
    private void OnUserPlaybackChanged(bool isPlaying)
    {
        isUserClipPlaying = isPlaying;
        UpdateUserPlayButton(isPlaying);
        
        DebugLog($"?? User playback: {(isPlaying ? "Started" : "Stopped")}");
    }
    
    // ??? TARGETED FIX: Comprehensive defensive check to prevent race condition
    private void OnGameStateEntered(GameState state)
    {
        if (state == GameState.Scoring)
        {
            DebugLog("?? Entering scoring state");
            
            // ? COMPREHENSIVE DEFENSIVE CHECK: Don't reset if we already have results
            if (hasReceivedScoreOrError)
            {
                DebugLog("?? Results already received - preserving current state (race condition protection)");
                // Ensure loading indicators are hidden since we have results
                if (loadingIndicator != null)
                    loadingIndicator.SetActive(false);
                if (progressSlider != null)
                    progressSlider.gameObject.SetActive(false);
                return; // Exit early - don't reset UI
            }
            
            // ? Only reset UI if no results have been received yet
            ResetUIState();
            SetLoadingState(true);
            
            if (statusText != null)
                statusText.text = "Analyzing your performance...";
        }
    }
    
    private void OnGameStateExited(GameState state)
    {
        if (state == GameState.Scoring)
        {
            DebugLog("?? Exiting scoring state");
            ResetUIState();
        }
    }
    
    // NEW: Handle scoring errors (like recording too short)
    private void OnScoringError(string errorMessage)
    {
        DebugLog($"? Scoring error received: {errorMessage}");
        
        // FIXED: Mark that we received a result
        hasReceivedScoreOrError = true;
        
        // Show error panel and hide score panel
        ShowErrorState(errorMessage);
        
        // Update status
        if (statusText != null)
            statusText.text = "Analysis failed - see error message";
        
        // FIXED: End loading state but keep audio controls disabled (no valid data)
        SetLoadingState(false);
        SetAudioControlsEnabled(false);
        
        DebugLog("? Error state displayed with navigation buttons enabled");
    }
    
    // NEW: Show error state instead of scores
    private void ShowErrorState(string errorMessage)
    {
        // Hide score panel
        if (scorePanel != null)
            scorePanel.SetActive(false);
        
        // Show error panel
        if (errorPanel != null)
            errorPanel.SetActive(true);
        
        // Set error message
        if (errorMessageText != null)
            errorMessageText.text = errorMessage;
        
        DebugLog($"? Error state displayed: {errorMessage}");
    }
    
    // NEW: Show score state (normal successful scoring)
    private void ShowScoreState()
    {
        // Show score panel
        if (scorePanel != null)
            scorePanel.SetActive(true);
        
        // Hide error panel
        if (errorPanel != null)
            errorPanel.SetActive(false);
        
        DebugLog("?? Score state displayed");
    }
    
    // NEW: Error retry button handler
    private void OnErrorRetryButtonClicked()
    {
        DebugLog("?? Error retry requested by user");
        
        // Same as regular retry
        OnAgainButtonClicked();
    }
    
    // Button Event Handlers
    private void OnNativePlayButtonClicked()
    {
        if (scoringManager == null) return;
        
        if (isNativeClipPlaying)
        {
            scoringManager.StopNativeClip();
        }
        else
        {
            scoringManager.PlayNativeClip();
        }
    }
    
    private void OnNativeStopButtonClicked()
    {
        if (scoringManager != null)
        {
            scoringManager.StopNativeClip();
        }
    }
    
    private void OnUserPlayButtonClicked()
    {
        if (scoringManager == null) return;
        
        if (isUserClipPlaying)
        {
            scoringManager.StopUserClip();
        }
        else
        {
            scoringManager.PlayUserClip();
        }
    }
    
    private void OnUserStopButtonClicked()
    {
        if (scoringManager != null)
        {
            scoringManager.StopUserClip();
        }
    }
    
    private void OnAgainButtonClicked()
    {
        DebugLog("?? Retry requested by user");
        
        if (gameStateManager != null)
        {
            gameStateManager.RetryExercise();
        }
        else if (scoringManager != null)
        {
            scoringManager.RequestRetry();
        }
    }
    
    private void OnNextButtonClicked()
    {
        DebugLog("?? Next exercise requested by user");
        
        if (gameStateManager != null)
        {
            gameStateManager.NextExercise();
        }
        else if (scoringManager != null)
        {
            scoringManager.RequestNext();
        }
    }
    
    // UI Update Methods
    private void UpdateScoreDisplay(float pitchScore, float rhythmScore, float overallScore)
    {
        // Update text displays
        if (pitchScoreText != null)
            pitchScoreText.text = $"Pitch: {pitchScore:F1}%";
        
        if (rhythmScoreText != null)
            rhythmScoreText.text = $"Rhythm: {rhythmScore:F1}%";
        
        if (overallScoreText != null)
            overallScoreText.text = $"Overall: {overallScore:F1}%";
        
        // Update sliders
        if (pitchScoreSlider != null)
        {
            pitchScoreSlider.value = pitchScore / 100f;
            pitchScoreSlider.fillRect.GetComponent<Image>().color = GetScoreColor(pitchScore);
        }
        
        if (rhythmScoreSlider != null)
        {
            rhythmScoreSlider.value = rhythmScore / 100f;
            rhythmScoreSlider.fillRect.GetComponent<Image>().color = GetScoreColor(rhythmScore);
        }
        
        if (overallScoreSlider != null)
        {
            overallScoreSlider.value = overallScore / 100f;
            overallScoreSlider.fillRect.GetComponent<Image>().color = GetScoreColor(overallScore);
        }
        
        // Update text colors
        Color pitchColor = GetScoreColor(pitchScore);
        Color rhythmColor = GetScoreColor(rhythmScore);
        Color overallColor = GetScoreColor(overallScore);
        
        if (pitchScoreText != null) pitchScoreText.color = pitchColor;
        if (rhythmScoreText != null) rhythmScoreText.color = rhythmColor;
        if (overallScoreText != null) overallScoreText.color = overallColor;
    }
    
    // Neue Methode: Safe Animation Start
    private void AnimateScoreDisplay(float targetPitch, float targetRhythm, float targetOverall)
    {
        if (scoreAnimationCoroutine != null)
            StopCoroutine(scoreAnimationCoroutine);
        
        // FIXED: Verify canvas is active and component is enabled
        if (gameObject.activeInHierarchy && enabled)
        {
            scoreAnimationCoroutine = StartCoroutine(AnimateScoresCoroutine(targetPitch, targetRhythm, targetOverall));
        }
        else
        {
            DebugLog("?? Cannot start animation - GameObject inactive or component disabled");
            UpdateScoreDisplay(targetPitch, targetRhythm, targetOverall);
        }
    }
    
    private System.Collections.IEnumerator AnimateScoresCoroutine(float targetPitch, float targetRhythm, float targetOverall)
    {
        float elapsed = 0f;
        
        while (elapsed < scoreAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / scoreAnimationDuration;
            float easedProgress = scoreAnimationCurve.Evaluate(progress);
            
            float currentPitch = targetPitch * easedProgress;
            float currentRhythm = targetRhythm * easedProgress;
            float currentOverall = targetOverall * easedProgress;
            
            UpdateScoreDisplay(currentPitch, currentRhythm, currentOverall);
            
            yield return null;
        }
        
        // Ensure final values are exact
        UpdateScoreDisplay(targetPitch, targetRhythm, targetOverall);
    }
    
    private Color GetScoreColor(float score)
    {
        if (score >= scoreThresholdGood)
            return goodScoreColor;
        else if (score >= scoreThresholdAverage)
            return averageScoreColor;
        else
            return poorScoreColor;
    }
    
    private void UpdateNativePlayButton(bool isPlaying)
    {
        if (nativePlayButtonText != null)
            nativePlayButtonText.text = isPlaying ? "Stop Native" : "Play Native";
        
        if (nativePlayButtonIcon != null)
            nativePlayButtonIcon.sprite = isPlaying ? stopIcon : playIcon;
        
        // Update stop button visibility
        if (nativeStopButton != null)
            nativeStopButton.gameObject.SetActive(isPlaying);
    }
    
    private void UpdateUserPlayButton(bool isPlaying)
    {
        if (userPlayButtonText != null)
            userPlayButtonText.text = isPlaying ? "Stop User" : "Play User";
        
        if (userPlayButtonIcon != null)
            userPlayButtonIcon.sprite = isPlaying ? stopIcon : playIcon;
        
        // Update stop button visibility
        if (userStopButton != null)
            userStopButton.gameObject.SetActive(isPlaying);
    }
    
    private void SetAudioControlsEnabled(bool enabled)
    {
        if (nativePlayButton != null)
            nativePlayButton.interactable = enabled;
        
        if (nativeStopButton != null)
            nativeStopButton.interactable = enabled;
        
        if (userPlayButton != null)
            userPlayButton.interactable = enabled;
        
        if (userStopButton != null)
            userStopButton.interactable = enabled;
        
        DebugLog($"?? Audio controls: {(enabled ? "Enabled" : "Disabled")}");
    }
    
    // FIXED: Improved loading state management
    private void SetLoadingState(bool isLoading)
    {
        isLoadingActive = isLoading;
        
        // Show/hide loading indicators
        if (loadingIndicator != null)
            loadingIndicator.SetActive(isLoading);
        
        if (progressSlider != null)
            progressSlider.gameObject.SetActive(isLoading);
        
        if (isLoading)
        {
            // Disable all controls during loading
            SetAudioControlsEnabled(false);
            SetNavigationButtonsEnabled(false);
            
            DebugLog("? Loading state: ACTIVE - all controls disabled");
        }
        else
        {
            // Only enable controls if we have received a result
            if (hasReceivedScoreOrError)
            {
                SetNavigationButtonsEnabled(true);
                
                // Only enable audio controls for successful scoring (not errors)
                if (scorePanel != null && scorePanel.activeInHierarchy)
                {
                    SetAudioControlsEnabled(true);
                }
                
                DebugLog("? Loading state: INACTIVE - controls enabled based on result type");
            }
            else
            {
                DebugLog("? Loading state: INACTIVE but waiting for results");
            }
        }
    }
    
    // FIXED: Separate navigation button control
    private void SetNavigationButtonsEnabled(bool enabled)
    {
        if (againButton != null)
        {
            againButton.interactable = enabled;
            DebugLog($"?? Again button: {(enabled ? "Enabled" : "Disabled")}");
        }
        
        if (nextButton != null)
        {
            nextButton.interactable = enabled;
            DebugLog($"?? Next button: {(enabled ? "Enabled" : "Disabled")}");
        }
    }
    
    // Public API for external control
    public void SetVisualizationAreas(Transform nativeArea, Transform userArea)
    {
        nativeVisualizationArea = nativeArea;
        userVisualizationArea = userArea;
        
        DebugLog("?? Visualization areas assigned");
    }
    
    public void ShowDetailedResults(ScoringResults results)
    {
        currentScores = results;
        
        // Could show detailed breakdown in future
        if (statusText != null)
        {
            statusText.text = $"Analysis complete: {results.nativeDataPoints} vs {results.userDataPoints} data points";
        }
    }
    
    // Testing and Debug Methods
    [ContextMenu("Test Score Animation")]
    public void TestScoreAnimation()
    {
        OnScoresCalculated(75f, 68f);
    }
    
    [ContextMenu("Reset UI")]
    public void TestResetUI()
    {
        ResetUIState();
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[ScoringUI] {message}");
        }
    }
    
    void OnDestroy()
    {
        // Cleanup subscriptions
        if (scoringManager != null)
        {
            scoringManager.OnScoresCalculated -= OnScoresCalculated;
            scoringManager.OnClipsLoaded -= OnClipsLoaded;
            scoringManager.OnNativeClipPlaybackChanged -= OnNativePlaybackChanged;
            scoringManager.OnUserClipPlaybackChanged -= OnUserPlaybackChanged;
            scoringManager.OnScoringError -= OnScoringError; // NEW: Cleanup error event
        }
        
        if (gameStateManager != null)
        {
            gameStateManager.OnStateEntered -= OnGameStateEntered;
            gameStateManager.OnStateExited -= OnGameStateExited;
        }
        
        // Stop animations
        if (scoreAnimationCoroutine != null)
            StopCoroutine(scoreAnimationCoroutine);
    }
}