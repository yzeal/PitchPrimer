using UnityEngine;
using System.Collections.Generic;

// COPILOT CONTEXT: Advanced scoring algorithms for Japanese pitch accent trainer
// Replaces placeholder scoring with DTW-based pitch analysis and energy-based rhythm analysis
// Maintains existing ScoringManager integration while providing sophisticated prosodic analysis

namespace JapanesePitchTrainer.Scoring.Advanced
{
    public static class AdvancedScoringAlgorithms
    {
        [System.Serializable]
        public struct AdvancedScoringSettings
        {
            [Header("DTW Pitch Analysis")]
            public DTWPitchAnalyzer.DTWSettings dtwSettings;
            
            [Header("Energy-based Rhythm Analysis")]
            public EnergyBasedSegmentation.SegmentationSettings segmentationSettings;
            
            [Header("Score Combination")]
            [Range(0.0f, 1.0f)] public float pitchWeight;
            [Range(0.0f, 1.0f)] public float rhythmWeight;
            
            [Header("Debug")]
            public bool enableDetailedLogging;
            
            public static AdvancedScoringSettings Default => new AdvancedScoringSettings
            {
                dtwSettings = DTWPitchAnalyzer.DTWSettings.Default,
                segmentationSettings = EnergyBasedSegmentation.SegmentationSettings.Default,
                pitchWeight = 0.6f,
                rhythmWeight = 0.4f,
                enableDetailedLogging = false
            };
        }
        
        /// <summary>
        /// Calculate DTW-based pitch score - replaces ScoringManager.CalculatePitchScore()
        /// </summary>
        public static float CalculateDTWPitchScore(List<PitchDataPoint> native, List<PitchDataPoint> user, 
                                                  AdvancedScoringSettings settings = default)
        {
            if (settings.Equals(default(AdvancedScoringSettings)))
                settings = AdvancedScoringSettings.Default;
            
            try
            {
                float score = DTWPitchAnalyzer.CalculateDTWPitchScore(native, user, settings.dtwSettings);
                
                if (settings.enableDetailedLogging)
                {
                    var detailedResult = DTWPitchAnalyzer.GetDetailedDTWResult(native, user, settings.dtwSettings);
                    Debug.Log($"[AdvancedScoring] Detailed DTW Pitch Result: {detailedResult}");
                }
                
                Debug.Log($"[AdvancedScoring] DTW Pitch Score: {score:F1}%");
                return score;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AdvancedScoring] Error in DTW pitch analysis: {e.Message}");
                return 0f;
            }
        }
        
        /// <summary>
        /// Calculate energy-based rhythm score - replaces ScoringManager.CalculateRhythmScore()
        /// </summary>
        public static float CalculateProsodicRhythmScore(List<PitchDataPoint> native, List<PitchDataPoint> user, 
                                                        AdvancedScoringSettings settings = default)
        {
            if (settings.Equals(default(AdvancedScoringSettings)))
                settings = AdvancedScoringSettings.Default;
            
            try
            {
                // Analyze rhythm patterns for both recordings
                var nativeRhythm = EnergyBasedSegmentation.AnalyzeRhythm(native, settings.segmentationSettings);
                var userRhythm = EnergyBasedSegmentation.AnalyzeRhythm(user, settings.segmentationSettings);
                
                // Compare rhythm patterns
                float similarity = EnergyBasedSegmentation.CompareRhythm(nativeRhythm, userRhythm);
                float score = similarity * 100f;
                
                if (settings.enableDetailedLogging)
                {
                    Debug.Log($"[AdvancedScoring] Native Rhythm: {nativeRhythm}");
                    Debug.Log($"[AdvancedScoring] User Rhythm: {userRhythm}");
                }
                
                Debug.Log($"[AdvancedScoring] Prosodic Rhythm Score: {score:F1}%");
                return score;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AdvancedScoring] Error in rhythm analysis: {e.Message}");
                return 0f;
            }
        }
        
        /// <summary>
        /// Combined advanced scoring - can replace entire scoring calculation if needed
        /// </summary>
        public static (float pitchScore, float rhythmScore, float overallScore) CalculateCombinedScore(
            List<PitchDataPoint> native, List<PitchDataPoint> user, 
            AdvancedScoringSettings settings = default)
        {
            if (settings.Equals(default(AdvancedScoringSettings)))
                settings = AdvancedScoringSettings.Default;
            
            float pitchScore = CalculateDTWPitchScore(native, user, settings);
            float rhythmScore = CalculateProsodicRhythmScore(native, user, settings);
            
            // Weighted combination
            float totalWeight = settings.pitchWeight + settings.rhythmWeight;
            if (totalWeight > 0f)
            {
                float overallScore = (pitchScore * settings.pitchWeight + rhythmScore * settings.rhythmWeight) / totalWeight;
                
                Debug.Log($"[AdvancedScoring] Combined Score: Pitch={pitchScore:F1}% (weight: {settings.pitchWeight}), " +
                         $"Rhythm={rhythmScore:F1}% (weight: {settings.rhythmWeight}), " +
                         $"Overall={overallScore:F1}%");
                
                return (pitchScore, rhythmScore, overallScore);
            }
            else
            {
                // Fallback to simple average
                float overallScore = (pitchScore + rhythmScore) / 2f;
                return (pitchScore, rhythmScore, overallScore);
            }
        }
        
        #region Testing & Validation
        
        /// <summary>
        /// Test advanced scoring algorithms with sample data
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void TestAdvancedScoring()
        {
            Debug.Log("[AdvancedScoring] Running algorithm tests...");
            
            // Test DTW
            DTWPitchAnalyzer.TestDTW();
            
            // Test with sample pitch data
            var sampleNative = GenerateSamplePitchData(5f, 150f);
            var sampleUser = GenerateSamplePitchData(5.2f, 180f); // Slightly slower, higher pitch
            
            var result = CalculateCombinedScore(sampleNative, sampleUser);
            
            Debug.Log($"[AdvancedScoring] Test result: Pitch={result.pitchScore:F1}%, " +
                     $"Rhythm={result.rhythmScore:F1}%, Overall={result.overallScore:F1}%");
        }
        
        /// <summary>
        /// Generate sample pitch data for testing
        /// </summary>
        private static List<PitchDataPoint> GenerateSamplePitchData(float duration, float basePitch)
        {
            var data = new List<PitchDataPoint>();
            float interval = 0.1f;
            int pointCount = Mathf.RoundToInt(duration / interval);
            
            for (int i = 0; i < pointCount; i++)
            {
                float time = i * interval;
                
                // Generate pitch pattern: rise, plateau, fall
                float pitchModifier = 0f;
                if (i < pointCount * 0.3f) // Rising
                    pitchModifier = (float)i / (pointCount * 0.3f) * 20f;
                else if (i < pointCount * 0.7f) // Plateau
                    pitchModifier = 20f;
                else // Falling
                    pitchModifier = 20f * (1f - (float)(i - pointCount * 0.7f) / (pointCount * 0.3f));
                
                float pitch = basePitch + pitchModifier;
                float energy = 0.1f + 0.3f * Mathf.Sin(time * 2f); // Simulated energy variation
                
                data.Add(new PitchDataPoint(time, pitch, 0.8f, energy));
            }
            
            return data;
        }
        
        #endregion
    }
}