// COPILOT CONTEXT: Game state enumeration for Japanese pitch accent trainer
// Defines all possible application states for the GameStateManager

public enum GameState
{
    MainMenu,      // Main menu screen
    Chorusing,     // Active chorusing exercise with native audio playback
    Scoring,       // Results screen comparing user recording to native
    Settings,      // Application settings and configuration
    Calibration    // Voice calibration screen for personalizing pitch ranges
}