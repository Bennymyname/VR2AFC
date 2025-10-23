using UnityEngine;

public static class SoundSynth
{
    public static AudioClip MakeSine(float freq = 880f, float dur = 0.18f, float vol = 0.2f, int sampleRate = 48000)
    {
        int samples = Mathf.CeilToInt(dur * sampleRate);
        var data = new float[samples];
        float inc = 2f * Mathf.PI * freq / sampleRate;
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            data[i] = Mathf.Sin(phase) * vol;
            phase += inc;
        }
        var clip = AudioClip.Create("ding", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static AudioClip MakeNoise(float dur = 0.20f, float vol = 0.18f, int sampleRate = 48000)
    {
        int samples = Mathf.CeilToInt(dur * sampleRate);
        var data = new float[samples];
        var rng = new System.Random(12345);
        for (int i = 0; i < samples; i++)
        {
            // white noise in [-1,1]
            float n = (float)(rng.NextDouble() * 2.0 - 1.0);
            data[i] = n * vol;
        }
        var clip = AudioClip.Create("noise", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
