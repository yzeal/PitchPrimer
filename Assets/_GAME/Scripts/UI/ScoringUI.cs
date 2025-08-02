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
            
            DebugLog("? Subscribed to ScoringManager events");
        }
        else
        {
            Debug.LogError("[ScoringUI] ScoringManager not found!");
        }
        
        if (gameStateManager != null)
        {
            gameStateManager.OnStateEntered += OnGameStateEntered;
            gameStateManager.OnStateExited += OnGameStateExited;
            
            DebugLog("? Subscribed to GameStateManager events");
        }
    }
    
    private void ResetUIState()
    {
        // Reset scores
        UpdateScoreDisplay(0, 0, 0);
        
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
        
        DebugLog("?? UI state reset for new scoring session");
    }
    
    // Event Handlers
    private void OnScoresCalculated(float pitchScore, float rhythmScore)
    {
        float overallScore = (pitchScore + rhythmScore) / 2f;
        
        DebugLog($"?? Scores received: Pitch={pitchScore:F1}, Rhythm={rhythmScore:F1}, Overall={overallScore:F1}");
        
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
        
        // FIXED: Enable UI controls after score calculation
        SetAudioControlsEnabled(true);
        
        if (againButton != null)
            againButton.interactable = true;
        
        if (nextButton != null)
            nextButton.interactable = true;
    }
    
    private void OnClipsLoaded(AudioClip nativeClip, AudioClip userClip)
    {
        DebugLog($"?? Clips loaded: Native={nativeClip?.name}, User={userClip?.name}");
        
        // Update clip labels with duration info
        if (nativeClipLabel != null && nativeClip != null)
            nativeClipLabel.text = $"Native Recording ({nativeClip.length:F1}s)";
        
        if (userClipLabel != null && userClip != null)
            userClipLabel.text = $"Your Recording ({userClip.length:F1}s)";
        
        // Enable audio controls
        SetAudioControlsEnabled(true);
        
        // Update status
        if (statusText != null)
            statusText.text = "Audio loaded - Click play buttons to compare";
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
    
    private void OnGameStateEntered(GameState state)
    {
        if (state == GameState.Scoring)
        {
            DebugLog("?? Entering scoring state");
            
            // FIXED: Initialize UI state for scoring
            ResetUIState();
            SetLoadingState(true);
            
            if (statusText != null)
                statusText.text = "Analyzing your performance...";
            
            // FIXED: Ensure controls are initially disabled
            SetAudioControlsEnabled(false);
            
            if (againButton != null)
                againButton.interactable = false;
            
            if (nextButton != null)
                nextButton.interactable = false;
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
        
        DebugLog($"??? Audio controls: {(enabled ? "Enabled" : "Disabled")}");
    }
    
    private void SetLoadingState(bool isLoading)
    {
        if (loadingIndicator != null)
            loadingIndicator.SetActive(isLoading);
        
        if (progressSlider != null)
            progressSlider.gameObject.SetActive(isLoading);
        
        // Disable controls during loading
        SetAudioControlsEnabled(!isLoading);
        
        if (againButton != null)
            againButton.interactable = !isLoading;
        
        if (nextButton != null)
            nextButton.interactable = !isLoading;
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