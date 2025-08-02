using UnityEngine;

// COPILOT CONTEXT: Scoring results data structure for Japanese pitch accent trainer
// Used by ScoringManager to store and communicate scoring results

[System.Serializable]
public struct ScoringResults
{
    public float pitchScore;
    public float rhythmScore;
    public float overallScore;
    public int nativeDataPoints;
    public int userDataPoints;
    
    public override string ToString()
    {
        return $"Scores: Pitch={pitchScore:F1}, Rhythm={rhythmScore:F1}, Overall={overallScore:F1} " +
               $"(Native={nativeDataPoints}, User={userDataPoints} points)";
    }
}