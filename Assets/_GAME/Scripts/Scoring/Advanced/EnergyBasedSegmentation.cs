using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// COPILOT CONTEXT: Energy-based phrase segmentation for Japanese rhythm analysis
// Detects speech segments and pauses using energy and pitch patterns
// Avoids speech recognition while providing meaningful rhythm comparison

namespace JapanesePitchTrainer.Scoring.Advanced
{
    public static class EnergyBasedSegmentation
    {
        [System.Serializable]
        public struct SegmentationSettings
        {
            [Header("Energy Analysis")]
            [Range(0.001f, 0.1f)] public float silenceEnergyThreshold;
            [Range(0.01f, 1.0f)] public float minimumSegmentDuration;
            [Range(0.01f, 0.5f)] public float minimumPauseDuration;
            
            [Header("Phrase Detection")]
            [Range(0.1f, 2.0f)] public float energyDropThreshold;
            [Range(0.1f, 2.0f)] public float maxPhraseDuration;
            [Range(2, 20)] public int smoothingWindowSize;
            
            [Header("Pitch Integration")]
            public bool usePitchForSegmentation;
            [Range(10f, 100f)] public float pitchResetThreshold; // Hz change
            
            public static SegmentationSettings Default => new SegmentationSettings
            {
                silenceEnergyThreshold = 0.01f,
                minimumSegmentDuration = 0.1f,
                minimumPauseDuration = 0.05f,
                energyDropThreshold = 0.5f,
                maxPhraseDuration = 3.0f,
                smoothingWindowSize = 5,
                usePitchForSegmentation = true,
                pitchResetThreshold = 50f
            };
        }
        
        [System.Serializable]
        public struct PhraseSegment
        {
            public float start;
            public float duration;
            public float averageEnergy;
            public float averagePitch;
            public float pitchVariation;
            public SegmentType type;
            
            public float End => start + duration;
            
            public override string ToString()
            {
                return $"{type}: {start:F2}s-{End:F2}s ({duration:F2}s) " +
                       $"Energy: {averageEnergy:F3}, Pitch: {averagePitch:F1}Hz±{pitchVariation:F1}";
            }
        }
        
        public enum SegmentType
        {
            Speech,
            Pause,
            Silence
        }
        
        public struct RhythmAnalysis
        {
            public List<PhraseSegment> segments;
            public float totalSpeechDuration;
            public float totalPauseDuration;
            public float speechToSilenceRatio;
            public float averageSegmentDuration;
            public float rhythmRegularity;
            
            public override string ToString()
            {
                return $"Rhythm: {segments.Count} segments, " +
                       $"Speech: {totalSpeechDuration:F1}s, Pauses: {totalPauseDuration:F1}s, " +
                       $"Ratio: {speechToSilenceRatio:F2}, Regularity: {rhythmRegularity:F2}";
            }
        }
        
        /// <summary>
        /// Main method: Segment audio into speech and pause phrases
        /// </summary>
        public static RhythmAnalysis AnalyzeRhythm(List<PitchDataPoint> pitchData, SegmentationSettings settings = default)
        {
            if (settings.Equals(default(SegmentationSettings)))
                settings = SegmentationSettings.Default;
            
            if (pitchData == null || pitchData.Count == 0)
            {
                Debug.LogWarning("[EnergyBasedSegmentation] No pitch data provided");
                return new RhythmAnalysis { segments = new List<PhraseSegment>() };
            }
            
            // Step 1: Smooth energy data
            var smoothedData = SmoothEnergyData(pitchData, settings.smoothingWindowSize);
            
            // Step 2: Detect speech/silence boundaries
            var basicSegments = DetectBasicSegments(smoothedData, settings);
            
            // Step 3: Refine segments using pitch information
            var refinedSegments = settings.usePitchForSegmentation 
                ? RefinePhraseSegments(basicSegments, smoothedData, settings)
                : basicSegments;
            
            // Step 4: Calculate rhythm metrics
            var analysis = CalculateRhythmMetrics(refinedSegments);
            
            Debug.Log($"[EnergyBasedSegmentation] {analysis}");
            
            return analysis;
        }
        
        /// <summary>
        /// Smooth energy data to reduce noise
        /// </summary>
        private static List<PitchDataPoint> SmoothEnergyData(List<PitchDataPoint> data, int windowSize)
        {
            var smoothed = new List<PitchDataPoint>();
            
            for (int i = 0; i < data.Count; i++)
            {
                int start = Mathf.Max(0, i - windowSize / 2);
                int end = Mathf.Min(data.Count - 1, i + windowSize / 2);
                
                float avgEnergy = 0f;
                float avgPitch = 0f;
                float avgConfidence = 0f;
                int pitchCount = 0;
                
                for (int j = start; j <= end; j++)
                {
                    avgEnergy += data[j].audioLevel;
                    avgConfidence += data[j].confidence;
                    
                    if (data[j].HasPitch)
                    {
                        avgPitch += data[j].frequency;
                        pitchCount++;
                    }
                }
                
                int windowLength = end - start + 1;
                avgEnergy /= windowLength;
                avgConfidence /= windowLength;
                avgPitch = pitchCount > 0 ? avgPitch / pitchCount : 0f;
                
                smoothed.Add(new PitchDataPoint(
                    data[i].timestamp,
                    avgPitch,
                    avgConfidence,
                    avgEnergy
                ));
            }
            
            return smoothed;
        }
        
        /// <summary>
        /// Detect basic speech/silence segments based on energy
        /// </summary>
        private static List<PhraseSegment> DetectBasicSegments(List<PitchDataPoint> data, SegmentationSettings settings)
        {
            var segments = new List<PhraseSegment>();
            
            bool inSpeech = false;
            float segmentStart = 0f;
            float segmentEnergy = 0f;
            float segmentPitch = 0f;
            int segmentPitchCount = 0;
            List<float> segmentPitches = new List<float>();
            
            for (int i = 0; i < data.Count; i++)
            {
                var point = data[i];
                bool hasEnoughEnergy = point.audioLevel >= settings.silenceEnergyThreshold;
                
                if (!inSpeech && hasEnoughEnergy)
                {
                    // Start of speech segment
                    inSpeech = true;
                    segmentStart = point.timestamp;
                    segmentEnergy = 0f;
                    segmentPitch = 0f;
                    segmentPitchCount = 0;
                    segmentPitches.Clear();
                }
                else if (inSpeech && !hasEnoughEnergy)
                {
                    // End of speech segment
                    float duration = point.timestamp - segmentStart;
                    
                    if (duration >= settings.minimumSegmentDuration)
                    {
                        float avgEnergy = segmentEnergy / Mathf.Max(1, i - GetIndexFromTimestamp(data, segmentStart));
                        float avgPitch = segmentPitchCount > 0 ? segmentPitch / segmentPitchCount : 0f;
                        float pitchVariation = CalculatePitchVariation(segmentPitches);
                        
                        segments.Add(new PhraseSegment
                        {
                            start = segmentStart,
                            duration = duration,
                            averageEnergy = avgEnergy,
                            averagePitch = avgPitch,
                            pitchVariation = pitchVariation,
                            type = SegmentType.Speech
                        });
                    }
                    
                    inSpeech = false;
                }
                
                if (inSpeech)
                {
                    segmentEnergy += point.audioLevel;
                    if (point.HasPitch)
                    {
                        segmentPitch += point.frequency;
                        segmentPitchCount++;
                        segmentPitches.Add(point.frequency);
                    }
                }
            }
            
            // Handle case where recording ends during speech
            if (inSpeech && data.Count > 0)
            {
                float duration = data.Last().timestamp - segmentStart;
                if (duration >= settings.minimumSegmentDuration)
                {
                    float avgEnergy = segmentEnergy / Mathf.Max(1, data.Count - GetIndexFromTimestamp(data, segmentStart));
                    float avgPitch = segmentPitchCount > 0 ? segmentPitch / segmentPitchCount : 0f;
                    float pitchVariation = CalculatePitchVariation(segmentPitches);
                    
                    segments.Add(new PhraseSegment
                    {
                        start = segmentStart,
                        duration = duration,
                        averageEnergy = avgEnergy,
                        averagePitch = avgPitch,
                        pitchVariation = pitchVariation,
                        type = SegmentType.Speech
                    });
                }
            }
            
            return segments;
        }
        
        /// <summary>
        /// Refine segments using pitch reset detection
        /// </summary>
        private static List<PhraseSegment> RefinePhraseSegments(List<PhraseSegment> basicSegments, 
                                                               List<PitchDataPoint> data, 
                                                               SegmentationSettings settings)
        {
            var refinedSegments = new List<PhraseSegment>();
            
            foreach (var segment in basicSegments)
            {
                if (segment.type != SegmentType.Speech || segment.duration <= settings.maxPhraseDuration)
                {
                    refinedSegments.Add(segment);
                    continue;
                }
                
                // Split long segments at pitch resets
                var subSegments = SplitAtPitchResets(segment, data, settings);
                refinedSegments.AddRange(subSegments);
            }
            
            return refinedSegments;
        }
        
        /// <summary>
        /// Split segment at major pitch resets (phrase boundaries)
        /// </summary>
        private static List<PhraseSegment> SplitAtPitchResets(PhraseSegment segment, 
                                                             List<PitchDataPoint> data, 
                                                             SegmentationSettings settings)
        {
            var subSegments = new List<PhraseSegment>();
            
            int startIndex = GetIndexFromTimestamp(data, segment.start);
            int endIndex = GetIndexFromTimestamp(data, segment.End);
            
            var resetPoints = new List<float> { segment.start };
            
            // Find pitch reset points
            for (int i = startIndex + 1; i < endIndex; i++)
            {
                if (data[i].HasPitch && data[i - 1].HasPitch)
                {
                    float pitchChange = Mathf.Abs(data[i].frequency - data[i - 1].frequency);
                    if (pitchChange >= settings.pitchResetThreshold)
                    {
                        resetPoints.Add(data[i].timestamp);
                    }
                }
            }
            
            resetPoints.Add(segment.End);
            
            // Create sub-segments
            for (int i = 0; i < resetPoints.Count - 1; i++)
            {
                float subStart = resetPoints[i];
                float subEnd = resetPoints[i + 1];
                float subDuration = subEnd - subStart;
                
                if (subDuration >= settings.minimumSegmentDuration)
                {
                    var subSegment = CalculateSegmentProperties(subStart, subDuration, data);
                    subSegments.Add(subSegment);
                }
            }
            
            return subSegments.Count > 0 ? subSegments : new List<PhraseSegment> { segment };
        }
        
        /// <summary>
        /// Calculate segment properties from data
        /// </summary>
        private static PhraseSegment CalculateSegmentProperties(float start, float duration, List<PitchDataPoint> data)
        {
            int startIndex = GetIndexFromTimestamp(data, start);
            int endIndex = GetIndexFromTimestamp(data, start + duration);
            
            float totalEnergy = 0f;
            float totalPitch = 0f;
            int pitchCount = 0;
            var pitches = new List<float>();
            
            for (int i = startIndex; i <= endIndex && i < data.Count; i++)
            {
                totalEnergy += data[i].audioLevel;
                if (data[i].HasPitch)
                {
                    totalPitch += data[i].frequency;
                    pitchCount++;
                    pitches.Add(data[i].frequency);
                }
            }
            
            int sampleCount = endIndex - startIndex + 1;
            float avgEnergy = sampleCount > 0 ? totalEnergy / sampleCount : 0f;
            float avgPitch = pitchCount > 0 ? totalPitch / pitchCount : 0f;
            float pitchVariation = CalculatePitchVariation(pitches);
            
            return new PhraseSegment
            {
                start = start,
                duration = duration,
                averageEnergy = avgEnergy,
                averagePitch = avgPitch,
                pitchVariation = pitchVariation,
                type = SegmentType.Speech
            };
        }
        
        /// <summary>
        /// Calculate rhythm metrics from segments
        /// </summary>
        private static RhythmAnalysis CalculateRhythmMetrics(List<PhraseSegment> segments)
        {
            var speechSegments = segments.Where(s => s.type == SegmentType.Speech).ToList();
            var pauseSegments = segments.Where(s => s.type == SegmentType.Pause).ToList();
            
            float totalSpeechDuration = speechSegments.Sum(s => s.duration);
            float totalPauseDuration = pauseSegments.Sum(s => s.duration);
            
            float speechToSilenceRatio = totalPauseDuration > 0 ? totalSpeechDuration / totalPauseDuration : 
                                        totalSpeechDuration > 0 ? float.MaxValue : 0f;
            
            float averageSegmentDuration = speechSegments.Count > 0 ? 
                speechSegments.Average(s => s.duration) : 0f;
            
            // Calculate rhythm regularity (consistency of segment durations)
            float rhythmRegularity = CalculateRhythmRegularity(speechSegments);
            
            return new RhythmAnalysis
            {
                segments = segments,
                totalSpeechDuration = totalSpeechDuration,
                totalPauseDuration = totalPauseDuration,
                speechToSilenceRatio = speechToSilenceRatio,
                averageSegmentDuration = averageSegmentDuration,
                rhythmRegularity = rhythmRegularity
            };
        }
        
        /// <summary>
        /// Calculate how regular/consistent the rhythm is
        /// </summary>
        private static float CalculateRhythmRegularity(List<PhraseSegment> speechSegments)
        {
            if (speechSegments.Count < 2) return 1f;
            
            var durations = speechSegments.Select(s => s.duration).ToList();
            float mean = durations.Average();
            float variance = durations.Sum(d => (d - mean) * (d - mean)) / durations.Count;
            float stdDev = Mathf.Sqrt(variance);
            
            // Coefficient of variation (lower = more regular)
            float coefficient = mean > 0 ? stdDev / mean : 0f;
            
            // Convert to regularity score (higher = more regular)
            return Mathf.Clamp01(1f - coefficient);
        }
        
        /// <summary>
        /// Helper: Calculate pitch variation within a segment
        /// </summary>
        private static float CalculatePitchVariation(List<float> pitches)
        {
            if (pitches.Count < 2) return 0f;
            
            float mean = pitches.Average();
            float variance = pitches.Sum(p => (p - mean) * (p - mean)) / pitches.Count;
            return Mathf.Sqrt(variance);
        }
        
        /// <summary>
        /// Helper: Get array index from timestamp
        /// </summary>
        private static int GetIndexFromTimestamp(List<PitchDataPoint> data, float timestamp)
        {
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].timestamp >= timestamp)
                    return i;
            }
            return data.Count - 1;
        }
        
        #region Public Utility Methods
        
        /// <summary>
        /// Compare rhythm between two recordings
        /// </summary>
        public static float CompareRhythm(RhythmAnalysis native, RhythmAnalysis user)
        {
            if (native.segments.Count == 0 || user.segments.Count == 0)
                return 0f;
            
            // Compare relative timing patterns
            float segmentRatioSimilarity = CompareSegmentRatios(native, user);
            float pausePatternSimilarity = ComparePausePatterns(native, user);
            float regularitySimilarity = CompareRegularity(native, user);
            
            // Weighted combination
            float overallSimilarity = 
                segmentRatioSimilarity * 0.5f +
                pausePatternSimilarity * 0.3f +
                regularitySimilarity * 0.2f;
            
            Debug.Log($"[EnergyBasedSegmentation] Rhythm comparison: " +
                     $"Segments={segmentRatioSimilarity:F3}, " +
                     $"Pauses={pausePatternSimilarity:F3}, " +
                     $"Regularity={regularitySimilarity:F3}, " +
                     $"Overall={overallSimilarity:F3}");
            
            return overallSimilarity;
        }
        
        /// <summary>
        /// Compare relative segment duration patterns
        /// </summary>
        private static float CompareSegmentRatios(RhythmAnalysis native, RhythmAnalysis user)
        {
            var nativeSegments = native.segments.Where(s => s.type == SegmentType.Speech).ToList();
            var userSegments = user.segments.Where(s => s.type == SegmentType.Speech).ToList();
            
            if (nativeSegments.Count == 0 || userSegments.Count == 0) return 0f;
            
            // Calculate relative duration ratios
            var nativeRatios = CalculateRelativeDurations(nativeSegments);
            var userRatios = CalculateRelativeDurations(userSegments);
            
            // Compare patterns using DTW-like approach
            return CompareDurationPatterns(nativeRatios, userRatios);
        }
        
        /// <summary>
        /// Calculate relative durations (normalized to sum = 1)
        /// </summary>
        private static List<float> CalculateRelativeDurations(List<PhraseSegment> segments)
        {
            float totalDuration = segments.Sum(s => s.duration);
            if (totalDuration == 0f) return new List<float>();
            
            return segments.Select(s => s.duration / totalDuration).ToList();
        }
        
        /// <summary>
        /// Compare duration patterns with flexible matching
        /// </summary>
        private static float CompareDurationPatterns(List<float> pattern1, List<float> pattern2)
        {
            if (pattern1.Count == 0 || pattern2.Count == 0) return 0f;
            
            // Simple correlation for now (could be enhanced with DTW)
            int minLength = Mathf.Min(pattern1.Count, pattern2.Count);
            float correlation = 0f;
            
            for (int i = 0; i < minLength; i++)
            {
                float diff = Mathf.Abs(pattern1[i] - pattern2[i]);
                correlation += 1f - diff; // Inverse of difference
            }
            
            return Mathf.Clamp01(correlation / minLength);
        }
        
        /// <summary>
        /// Compare pause patterns
        /// </summary>
        private static float ComparePausePatterns(RhythmAnalysis native, RhythmAnalysis user)
        {
            // Simple comparison of pause ratios
            float nativeRatio = native.totalSpeechDuration > 0 ? 
                native.totalPauseDuration / native.totalSpeechDuration : 0f;
            float userRatio = user.totalSpeechDuration > 0 ? 
                user.totalPauseDuration / user.totalSpeechDuration : 0f;
            
            float maxRatio = Mathf.Max(nativeRatio, userRatio);
            if (maxRatio == 0f) return 1f; // Both have no pauses
            
            float similarity = 1f - Mathf.Abs(nativeRatio - userRatio) / maxRatio;
            return Mathf.Clamp01(similarity);
        }
        
        /// <summary>
        /// Compare rhythm regularity
        /// </summary>
        private static float CompareRegularity(RhythmAnalysis native, RhythmAnalysis user)
        {
            float difference = Mathf.Abs(native.rhythmRegularity - user.rhythmRegularity);
            return 1f - difference; // Difference is already 0-1 range
        }
        
        #endregion
    }
}