using UnityEngine;

// COPILOT CONTEXT: Circular buffer for continuous audio recording
// Maintains fixed-size buffer that overwrites oldest data when full
// Optimized for real-time audio capture with minimal memory allocation

public class CircularAudioBuffer
{
    private float[] buffer;
    private int writeIndex;
    private int totalSamplesWritten;
    private readonly int bufferSize;
    
    public CircularAudioBuffer(int size)
    {
        bufferSize = size;
        buffer = new float[bufferSize];
        writeIndex = 0;
        totalSamplesWritten = 0;
    }
    
    public void AddSamples(float[] samples)
    {
        if (samples == null || samples.Length == 0) return;
        
        for (int i = 0; i < samples.Length; i++)
        {
            buffer[writeIndex] = samples[i];
            writeIndex = (writeIndex + 1) % bufferSize;
            totalSamplesWritten++;
        }
    }
    
    public void AddSample(float sample)
    {
        buffer[writeIndex] = sample;
        writeIndex = (writeIndex + 1) % bufferSize;
        totalSamplesWritten++;
    }
    
    public float[] GetLastSeconds(float seconds)
    {
        int sampleRate = 44100; // Default sample rate
        return GetLastSamples(Mathf.RoundToInt(seconds * sampleRate));
    }
    
    public float[] GetLastSamples(int sampleCount)
    {
        if (sampleCount <= 0 || sampleCount > bufferSize)
        {
            sampleCount = bufferSize;
        }
        
        // If we haven't filled the buffer yet, return what we have
        int availableSamples = Mathf.Min(totalSamplesWritten, bufferSize);
        int samplesToReturn = Mathf.Min(sampleCount, availableSamples);
        
        float[] result = new float[samplesToReturn];
        
        // Calculate start position (going backwards from current write position)
        int startIndex = writeIndex - samplesToReturn;
        if (startIndex < 0)
        {
            startIndex += bufferSize;
        }
        
        // Copy data, handling wrap-around
        for (int i = 0; i < samplesToReturn; i++)
        {
            int sourceIndex = (startIndex + i) % bufferSize;
            result[i] = buffer[sourceIndex];
        }
        
        return result;
    }
    
    public float[] GetAllData()
    {
        return GetLastSamples(bufferSize);
    }
    
    public void Clear()
    {
        for (int i = 0; i < bufferSize; i++)
        {
            buffer[i] = 0f;
        }
        writeIndex = 0;
        totalSamplesWritten = 0;
    }
    
    // Properties
    public int BufferSize => bufferSize;
    public int CurrentSize => Mathf.Min(totalSamplesWritten, bufferSize);
    public bool IsFull => totalSamplesWritten >= bufferSize;
    public bool HasData => totalSamplesWritten > 0;
    public float FillPercentage => (float)CurrentSize / bufferSize;
}