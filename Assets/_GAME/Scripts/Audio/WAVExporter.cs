using System;
using System.IO;
using UnityEngine;

// COPILOT CONTEXT: WAV file export utility for user recordings
// Implements WAV format as specified in Notes.md (16-bit, Mono, 44.1kHz)
// Cross-platform compatible with Unity's persistent data path

public static class WAVExporter
{
    private const int HEADER_SIZE = 44;
    
    public static bool SaveWAV(float[] audioData, string filePath, int sampleRate = 44100, int channels = 1)
    {
        if (audioData == null || audioData.Length == 0)
        {
            Debug.LogError("[WAVExporter] No audio data provided!");
            return false;
        }
        
        try
        {
            // Ensure directory exists
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Convert float samples to 16-bit PCM
            short[] intData = ConvertToInt16(audioData);
            
            // Create WAV file
            using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(fileStream))
            {
                WriteWAVHeader(writer, intData.Length, sampleRate, channels);
                WriteAudioData(writer, intData);
            }
            
            Debug.Log($"[WAVExporter] Successfully saved WAV: {filePath} " +
                     $"({audioData.Length} samples, {audioData.Length / (float)sampleRate:F1}s)");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[WAVExporter] Failed to save WAV file: {e.Message}");
            return false;
        }
    }
    
    private static short[] ConvertToInt16(float[] floatData)
    {
        short[] intData = new short[floatData.Length];
        
        for (int i = 0; i < floatData.Length; i++)
        {
            // Clamp to [-1, 1] range and convert to 16-bit
            float clampedSample = Mathf.Clamp(floatData[i], -1f, 1f);
            intData[i] = (short)(clampedSample * short.MaxValue);
        }
        
        return intData;
    }
    
    private static void WriteWAVHeader(BinaryWriter writer, int audioDataLength, int sampleRate, int channels)
    {
        int byteRate = sampleRate * channels * 2; // 2 bytes per sample (16-bit)
        int blockAlign = channels * 2;
        int dataSize = audioDataLength * 2; // 2 bytes per sample
        int fileSize = HEADER_SIZE + dataSize - 8;
        
        // RIFF header
        writer.Write("RIFF".ToCharArray());
        writer.Write(fileSize);
        writer.Write("WAVE".ToCharArray());
        
        // Format chunk
        writer.Write("fmt ".ToCharArray());
        writer.Write(16); // PCM format chunk size
        writer.Write((ushort)1); // Audio format (1 = PCM)
        writer.Write((ushort)channels); // Number of channels
        writer.Write(sampleRate); // Sample rate
        writer.Write(byteRate); // Byte rate
        writer.Write((ushort)blockAlign); // Block align
        writer.Write((ushort)16); // Bits per sample
        
        // Data chunk
        writer.Write("data".ToCharArray());
        writer.Write(dataSize);
    }
    
    private static void WriteAudioData(BinaryWriter writer, short[] audioData)
    {
        for (int i = 0; i < audioData.Length; i++)
        {
            writer.Write(audioData[i]);
        }
    }
    
    // Utility method to get file info
    public static AudioFileInfo GetWAVInfo(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }
        
        try
        {
            FileInfo fileInfo = new FileInfo(filePath);
            
            using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fileStream))
            {
                // Skip to format chunk
                fileStream.Seek(20, SeekOrigin.Begin);
                
                ushort audioFormat = reader.ReadUInt16();
                ushort channels = reader.ReadUInt16();
                uint sampleRate = reader.ReadUInt32();
                uint byteRate = reader.ReadUInt32();
                ushort blockAlign = reader.ReadUInt16();
                ushort bitsPerSample = reader.ReadUInt16();
                
                // Skip to data size
                fileStream.Seek(40, SeekOrigin.Begin);
                uint dataSize = reader.ReadUInt32();
                
                float durationSeconds = dataSize / (float)byteRate;
                
                return new AudioFileInfo
                {
                    FilePath = filePath,
                    FileSize = fileInfo.Length,
                    SampleRate = (int)sampleRate,
                    Channels = channels,
                    BitsPerSample = bitsPerSample,
                    Duration = durationSeconds,
                    DataSize = dataSize
                };
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[WAVExporter] Failed to read WAV info: {e.Message}");
            return null;
        }
    }
}

[System.Serializable]
public class AudioFileInfo
{
    public string FilePath;
    public long FileSize;
    public int SampleRate;
    public int Channels;
    public int BitsPerSample;
    public float Duration;
    public uint DataSize;
    
    public override string ToString()
    {
        return $"WAV: {SampleRate}Hz, {Channels}ch, {BitsPerSample}bit, {Duration:F1}s, {FileSize / 1024f:F1}KB";
    }
}