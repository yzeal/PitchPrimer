using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// COPILOT CONTEXT: Dynamic Time Warping implementation for Japanese pitch accent trainer
// Provides tempo-flexible pitch curve comparison for scoring user recordings against native speech
// Focuses on prosodic similarity rather than absolute pitch or timing matching

namespace JapanesePitchTrainer.Scoring.Advanced
{
    public static class DTWPitchAnalyzer
    {
        [System.Serializable]
        public struct DTWSettings
        {
            [Header("DTW Configuration")]
            [Range(0.1f, 2.0f)] public float timeWarpingTolerance;
            [Range(10f, 100f)] public float pitchWarpingTolerance; // Hz
            [Range(0.1f, 1.0f)] public float stepPenalty;
            
            [Header("Semitone Analysis")]
            public bool useSemitoneConversion;
            public float referenceFrequency; // A4 = 440Hz
            
            [Header("Prosodic Features")]
            [Range(0.0f, 1.0f)] public float pitchContourWeight;
            [Range(0.0f, 1.0f)] public float pitchRangeWeight;
            [Range(0.0f, 1.0f)] public float pitchDirectionWeight;
            
            public static DTWSettings Default => new DTWSettings
            {
                timeWarpingTolerance = 0.5f,
                pitchWarpingTolerance = 50f,
                stepPenalty = 0.3f,
                useSemitoneConversion = true,
                referenceFrequency = 440f,
                pitchContourWeight = 0.5f,
                pitchRangeWeight = 0.3f,
                pitchDirectionWeight = 0.2f
            };
        }
        
        public struct DTWResult
        {
            public float alignmentScore;
            public float pitchSimilarity;
            public float contourSimilarity;
            public float rangeSimilarity;
            public float overallScore;
            public int alignmentLength;
            public List<(int native, int user)> alignmentPath;
            
            public override string ToString()
            {
                return $"DTW Score: {overallScore:F3} (Alignment: {alignmentScore:F3}, " +
                       $"Pitch: {pitchSimilarity:F3}, Contour: {contourSimilarity:F3}, " +
                       $"Range: {rangeSimilarity:F3}, Path: {alignmentLength} steps)";
            }
        }
        
        /// <summary>
        /// Main DTW-based pitch comparison for Japanese speech
        /// </summary>
        public static float CalculateDTWPitchScore(List<PitchDataPoint> native, List<PitchDataPoint> user, DTWSettings settings = default)
        {
            if (settings.Equals(default(DTWSettings)))
                settings = DTWSettings.Default;
            
            // Extract valid pitch sequences
            var nativePitches = ExtractValidPitches(native);
            var userPitches = ExtractValidPitches(user);
            
            if (nativePitches.Count < 2 || userPitches.Count < 2)
            {
                Debug.LogWarning("[DTWPitchAnalyzer] Insufficient pitch data for DTW analysis");
                return 0f;
            }
            
            // Convert to semitones if enabled
            List<float> nativeData = settings.useSemitoneConversion 
                ? ConvertToSemitones(nativePitches, settings.referenceFrequency)
                : nativePitches;
            
            List<float> userData = settings.useSemitoneConversion 
                ? ConvertToSemitones(userPitches, settings.referenceFrequency)
                : userPitches;
            
            // Normalize to relative pitch changes (removes absolute pitch differences)
            var nativeNormalized = NormalizeToRelativePitch(nativeData);
            var userNormalized = NormalizeToRelativePitch(userData);
            
            // Perform DTW alignment
            var dtwResult = PerformDTW(nativeNormalized, userNormalized, settings);
            
            Debug.Log($"[DTWPitchAnalyzer] {dtwResult}");
            
            // Convert to 0-100 score
            return Mathf.Clamp01(dtwResult.overallScore) * 100f;
        }
        
        /// <summary>
        /// Extract valid pitch values from PitchDataPoint list
        /// </summary>
        private static List<float> ExtractValidPitches(List<PitchDataPoint> pitchData)
        {
            return pitchData
                .Where(p => p.HasPitch && p.confidence >= 0.3f)
                .Select(p => p.frequency)
                .ToList();
        }
        
        /// <summary>
        /// Convert Hz to semitones relative to reference frequency
        /// </summary>
        private static List<float> ConvertToSemitones(List<float> frequencies, float referenceFreq)
        {
            return frequencies
                .Select(freq => 12f * Mathf.Log(freq / referenceFreq, 2f))
                .ToList();
        }
        
        /// <summary>
        /// Normalize to relative pitch changes (removes speaker-specific absolute pitch)
        /// </summary>
        private static List<float> NormalizeToRelativePitch(List<float> pitches)
        {
            if (pitches.Count <= 1) return pitches;
            
            // Calculate mean and center around 0
            float mean = pitches.Average();
            var centered = pitches.Select(p => p - mean).ToList();
            
            // Optional: Scale by standard deviation for consistent range
            float stdDev = CalculateStandardDeviation(pitches);
            if (stdDev > 0.001f) // Avoid division by zero
            {
                return centered.Select(p => p / stdDev).ToList();
            }
            
            return centered;
        }
        
        /// <summary>
        /// Core DTW algorithm implementation
        /// </summary>
        private static DTWResult PerformDTW(List<float> sequence1, List<float> sequence2, DTWSettings settings)
        {
            int len1 = sequence1.Count;
            int len2 = sequence2.Count;
            
            // DTW distance matrix
            float[,] dtwMatrix = new float[len1 + 1, len2 + 1];
            
            // Initialize with infinity (large values)
            for (int i = 0; i <= len1; i++)
                for (int j = 0; j <= len2; j++)
                    dtwMatrix[i, j] = float.MaxValue;
            
            dtwMatrix[0, 0] = 0f;
            
            // Fill DTW matrix
            for (int i = 1; i <= len1; i++)
            {
                for (int j = 1; j <= len2; j++)
                {
                    float cost = CalculateLocalDistance(sequence1[i - 1], sequence2[j - 1], settings);
                    
                    // Three possible paths: insertion, deletion, match
                    float match = dtwMatrix[i - 1, j - 1] + cost;
                    float insertion = dtwMatrix[i, j - 1] + cost + settings.stepPenalty;
                    float deletion = dtwMatrix[i - 1, j] + cost + settings.stepPenalty;
                    
                    dtwMatrix[i, j] = Mathf.Min(match, Mathf.Min(insertion, deletion));
                }
            }
            
            // Calculate final alignment score
            float finalDistance = dtwMatrix[len1, len2];
            float maxPossibleDistance = Mathf.Max(len1, len2) * settings.pitchWarpingTolerance;
            float alignmentScore = 1f - Mathf.Clamp01(finalDistance / maxPossibleDistance);
            
            // Extract alignment path for additional analysis
            var alignmentPath = ExtractAlignmentPath(dtwMatrix, len1, len2);
            
            // Calculate additional prosodic features
            float pitchSimilarity = CalculateAlignedPitchSimilarity(sequence1, sequence2, alignmentPath);
            float contourSimilarity = CalculateContourSimilarity(sequence1, sequence2, alignmentPath);
            float rangeSimilarity = CalculateRangeSimilarity(sequence1, sequence2);
            
            // Weighted combination
            float overallScore = 
                alignmentScore * (1f - settings.pitchContourWeight - settings.pitchRangeWeight - settings.pitchDirectionWeight) +
                pitchSimilarity * settings.pitchContourWeight +
                contourSimilarity * settings.pitchDirectionWeight +
                rangeSimilarity * settings.pitchRangeWeight;
            
            return new DTWResult
            {
                alignmentScore = alignmentScore,
                pitchSimilarity = pitchSimilarity,
                contourSimilarity = contourSimilarity,
                rangeSimilarity = rangeSimilarity,
                overallScore = overallScore,
                alignmentLength = alignmentPath.Count,
                alignmentPath = alignmentPath
            };
        }
        
        /// <summary>
        /// Calculate local distance between two pitch points
        /// </summary>
        private static float CalculateLocalDistance(float pitch1, float pitch2, DTWSettings settings)
        {
            float difference = Mathf.Abs(pitch1 - pitch2);
            
            // Normalize by warping tolerance
            return difference / settings.pitchWarpingTolerance;
        }
        
        /// <summary>
        /// Extract optimal alignment path from DTW matrix
        /// </summary>
        private static List<(int, int)> ExtractAlignmentPath(float[,] dtwMatrix, int len1, int len2)
        {
            var path = new List<(int, int)>();
            int i = len1, j = len2;
            
            while (i > 0 && j > 0)
            {
                path.Add((i - 1, j - 1));
                
                // Find minimum predecessor
                float match = dtwMatrix[i - 1, j - 1];
                float deletion = i > 1 ? dtwMatrix[i - 1, j] : float.MaxValue;
                float insertion = j > 1 ? dtwMatrix[i, j - 1] : float.MaxValue;
                
                if (match <= deletion && match <= insertion)
                {
                    i--; j--;
                }
                else if (deletion <= insertion)
                {
                    i--;
                }
                else
                {
                    j--;
                }
            }
            
            path.Reverse();
            return path;
        }
        
        /// <summary>
        /// Calculate pitch similarity based on aligned sequences
        /// </summary>
        private static float CalculateAlignedPitchSimilarity(List<float> seq1, List<float> seq2, List<(int, int)> alignment)
        {
            if (alignment.Count == 0) return 0f;
            
            float totalDifference = 0f;
            foreach (var (i1, i2) in alignment)
            {
                if (i1 < seq1.Count && i2 < seq2.Count)
                {
                    totalDifference += Mathf.Abs(seq1[i1] - seq2[i2]);
                }
            }
            
            float averageDifference = totalDifference / alignment.Count;
            
            // Convert to similarity (0-1 range)
            return Mathf.Exp(-averageDifference * 0.5f);
        }
        
        /// <summary>
        /// Calculate pitch contour (direction) similarity
        /// </summary>
        private static float CalculateContourSimilarity(List<float> seq1, List<float> seq2, List<(int, int)> alignment)
        {
            if (alignment.Count < 2) return 0f;
            
            int agreementCount = 0;
            int totalComparisons = 0;
            
            for (int k = 1; k < alignment.Count; k++)
            {
                var (i1_prev, i2_prev) = alignment[k - 1];
                var (i1_curr, i2_curr) = alignment[k];
                
                if (i1_prev < seq1.Count && i1_curr < seq1.Count && 
                    i2_prev < seq2.Count && i2_curr < seq2.Count)
                {
                    float direction1 = seq1[i1_curr] - seq1[i1_prev];
                    float direction2 = seq2[i2_curr] - seq2[i2_prev];
                    
                    // Same direction?
                    if (Mathf.Sign(direction1) == Mathf.Sign(direction2))
                        agreementCount++;
                    
                    totalComparisons++;
                }
            }
            
            return totalComparisons > 0 ? (float)agreementCount / totalComparisons : 0f;
        }
        
        /// <summary>
        /// Calculate pitch range usage similarity
        /// </summary>
        private static float CalculateRangeSimilarity(List<float> seq1, List<float> seq2)
        {
            if (seq1.Count == 0 || seq2.Count == 0) return 0f;
            
            float range1 = seq1.Max() - seq1.Min();
            float range2 = seq2.Max() - seq2.Min();
            
            if (range1 == 0f && range2 == 0f) return 1f; // Both flat
            if (range1 == 0f || range2 == 0f) return 0f; // One flat, one not
            
            float ratio = Mathf.Min(range1, range2) / Mathf.Max(range1, range2);
            return ratio;
        }
        
        /// <summary>
        /// Helper: Calculate standard deviation
        /// </summary>
        private static float CalculateStandardDeviation(List<float> values)
        {
            if (values.Count <= 1) return 0f;
            
            float mean = values.Average();
            float sumSquaredDifferences = values.Sum(v => (v - mean) * (v - mean));
            return Mathf.Sqrt(sumSquaredDifferences / values.Count);
        }
        
        #region Public Utility Methods
        
        /// <summary>
        /// Test DTW with sample data
        /// </summary>
        public static void TestDTW()
        {
            var testNative = new List<float> { 100f, 110f, 120f, 115f, 105f, 100f };
            var testUser = new List<float> { 200f, 220f, 240f, 230f, 210f, 200f };
            
            var normalizedNative = NormalizeToRelativePitch(testNative);
            var normalizedUser = NormalizeToRelativePitch(testUser);
            
            var result = PerformDTW(normalizedNative, normalizedUser, DTWSettings.Default);
            
            Debug.Log($"[DTWPitchAnalyzer] Test result: {result}");
        }
        
        /// <summary>
        /// Get detailed analysis of pitch comparison
        /// </summary>
        public static DTWResult GetDetailedDTWResult(List<PitchDataPoint> native, List<PitchDataPoint> user, DTWSettings settings = default)
        {
            if (settings.Equals(default(DTWSettings)))
                settings = DTWSettings.Default;
            
            var nativePitches = ExtractValidPitches(native);
            var userPitches = ExtractValidPitches(user);
            
            if (nativePitches.Count < 2 || userPitches.Count < 2)
            {
                return new DTWResult { overallScore = 0f };
            }
            
            var nativeData = settings.useSemitoneConversion 
                ? ConvertToSemitones(nativePitches, settings.referenceFrequency)
                : nativePitches;
            
            var userData = settings.useSemitoneConversion 
                ? ConvertToSemitones(userPitches, settings.referenceFrequency)
                : userPitches;
            
            var nativeNormalized = NormalizeToRelativePitch(nativeData);
            var userNormalized = NormalizeToRelativePitch(userData);
            
            return PerformDTW(nativeNormalized, userNormalized, settings);
        }
        
        #endregion
    }
}