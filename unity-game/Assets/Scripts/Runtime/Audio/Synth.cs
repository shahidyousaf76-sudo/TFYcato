using System;
using UnityEngine;

namespace TFY.CatAdventure.Audio
{
    /// <summary>
    /// Small offline synthesiser: original, royalty-free placeholder sounds generated at load time (sound effects, friendly
    /// meows, world music loops and ambience). Replace any id with a real clip at Resources/Audio/{id}.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 22050;

        private static AudioClip Clip(string name, float[] data) { var c = AudioClip.Create(name, data.Length, 1, Rate, false); c.SetData(data, 0); return c; }
        /// <summary>Attack ramp, exponential decay, and a short fade at the very end (no clicks).</summary>
        private static float Env(float t, float attack, float decay, float len) =>
            (t < attack ? t / attack : Mathf.Exp(-(t - attack) * decay)) * Mathf.Clamp01((len - t) / 0.02f);
        private static float Saw(float ph) => 2f * (ph - Mathf.Floor(ph + 0.5f));
        private static float Tri(float ph) => 2f * Mathf.Abs(Saw(ph)) - 1f;

        /// <summary>Pitch sweep tone: jump (up), land (down), coin (bright blip), etc.</summary>
        public static AudioClip Tone(string name, float f0, float f1, float len, string wave = "sine", float vol = 0.5f, float decay = 4f, float vibrato = 0)
        {
            int n = (int)(len * Rate); var d = new float[n]; float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, k = t / len;
                float f = Mathf.Lerp(f0, f1, k) * (1 + vibrato * Mathf.Sin(t * 40f));
                ph += f / Rate; ph -= Mathf.Floor(ph);
                float s = wave == "square" ? (ph < 0.5f ? 1 : -1) * 0.6f : wave == "tri" ? Tri(ph) : wave == "saw" ? Saw(ph) * 0.7f : Mathf.Sin(ph * Mathf.PI * 2);
                d[i] = s * vol * Env(t, 0.005f, decay, len);
            }
            return Clip(name, d);
        }

        public static AudioClip Chord(string name, float[] freqs, float noteLen, float vol = 0.35f, bool arpeggio = true)
        {
            float len = arpeggio ? noteLen * freqs.Length + 0.5f : noteLen; int n = (int)(len * Rate); var d = new float[n];
            for (int j = 0; j < freqs.Length; j++)
            {
                int start = arpeggio ? (int)(j * noteLen * Rate) : 0; float ph = 0, ph2 = 0;
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)Rate; ph += freqs[j] / Rate; ph2 += freqs[j] * 2.001f / Rate;
                    d[i] += (Mathf.Sin(ph * Mathf.PI * 2) + 0.3f * Mathf.Sin(ph2 * Mathf.PI * 2)) * vol * Mathf.Exp(-t * 2.2f) * Mathf.Min(1, t * 200);
                }
            }
            return Clip(name, d);
        }

        public static AudioClip Noise(string name, float len, float lowpass, float vol, float attack = 0.005f, float decay = 6f, bool loop = false, float lfo = 0)
        {
            int n = (int)(len * Rate); var d = new float[n]; var r = new System.Random(name.GetHashCode()); float y = 0;
            float a = Mathf.Clamp01(lowpass / Rate * 6.28f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate; y += a * (((float)r.NextDouble() * 2 - 1) - y);
                float env = loop ? 1f : Env(t, attack, decay, len);
                float mod = lfo > 0 ? 0.6f + 0.4f * Mathf.Sin(t * lfo * Mathf.PI * 2) : 1;
                d[i] = y * vol * env * mod;
            }
            if (loop) Crossfade(d, Rate / 4);
            return Clip(name, d);
        }

        /// <summary>A friendly cartoon meow: gliding pitch through "m-i-a-u" vowel formants.</summary>
        public static AudioClip Meow(string name, float basePitch, float len, float excitement)
        {
            int n = (int)(len * Rate); var d = new float[n]; float ph = 0;
            var bp1 = new Biquad(); var bp2 = new Biquad();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, k = t / len;
                float pitch = basePitch * (1 + 0.55f * Mathf.Sin(k * Mathf.PI) * (0.6f + excitement * 0.6f) - 0.2f * k) * (1 + 0.015f * Mathf.Sin(t * 38f));
                ph += pitch / Rate; ph -= Mathf.Floor(ph);
                float src = Saw(ph);
                float f1 = k < 0.3f ? Mathf.Lerp(320, 700, k / 0.3f) : Mathf.Lerp(700, 380, (k - 0.3f) / 0.7f);
                float f2 = k < 0.3f ? Mathf.Lerp(2200, 1200, k / 0.3f) : Mathf.Lerp(1200, 800, (k - 0.3f) / 0.7f);
                bp1.SetBandpass(f1, 5, Rate); bp2.SetBandpass(f2, 7, Rate);
                float env = Mathf.Min(1, t / 0.04f) * Mathf.Min(1, (len - t) / 0.12f);
                d[i] = (bp1.Process(src) * 0.9f + bp2.Process(src) * 0.5f) * env * 0.9f;
            }
            return Clip(name, d);
        }

        /// <summary>Seeded music loop: bass, chords, a simple cheerful melody and soft drums. Style per world.</summary>
        public static AudioClip Music(string name, int seed, float bpm, int[] scale, float root, string lead, float energy, int bars = 8)
        {
            float beat = 60f / bpm; int beatsTotal = bars * 4; float len = beatsTotal * beat; int n = (int)(len * Rate);
            var d = new float[n]; var r = new System.Random(seed);
            int[] prog = { 0, 5, 3, 4 };
            float Note(int degree, int octave) { int s = scale.Length; int o = Mathf.FloorToInt(degree / (float)s); int idx = ((degree % s) + s) % s; return root * Mathf.Pow(2, (scale[idx] + 12 * (o + octave)) / 12f); }
            void Add(float start, float dur, float f, string wave, float vol)
            {
                int s0 = (int)(start * Rate), s1 = Mathf.Min(n, (int)((start + dur) * Rate)); float ph = 0;
                for (int i = s0; i < s1; i++) { float t = (i - s0) / (float)Rate; ph += f / Rate; float x = wave == "tri" ? Tri(ph) : wave == "square" ? (ph % 1 < 0.5f ? 0.5f : -0.5f) : Mathf.Sin(ph * Mathf.PI * 2); d[i % n] += x * vol * Mathf.Min(1, t * 80) * Mathf.Exp(-t * (wave == "sine" ? 1.5f : 3f)); }
            }
            int degree = 2;
            for (int b = 0; b < beatsTotal; b++)
            {
                int chord = prog[(b / 4) % prog.Length]; float t = b * beat;
                Add(t, beat * 0.9f, Note(chord, -2), "sine", 0.22f);                               // bass
                if (b % 2 == 0) foreach (var k in new[] { 0, 2, 4 }) Add(t, beat * 1.8f, Note(chord + k, 0), "sine", 0.05f); // chord pad
                if (r.NextDouble() < 0.75) { degree = Mathf.Clamp(degree + r.Next(-2, 3), -2, 9); Add(t, beat * (r.NextDouble() < 0.3 ? 1f : 0.5f), Note(degree + chord % 2, 1), lead, 0.09f); }
                if (r.NextDouble() < 0.4) Add(t + beat * 0.5f, beat * 0.45f, Note(degree + 2, 1), lead, 0.06f);
                // drums
                int k0 = (int)(t * Rate);
                if (b % 2 == 0) for (int i = 0; i < Rate * 0.12f && k0 + i < n; i++) { float tt = i / (float)Rate; d[k0 + i] += Mathf.Sin(2 * Mathf.PI * (60 + 80 * Mathf.Exp(-tt * 30)) * tt) * 0.25f * energy * Mathf.Exp(-tt * 18); }
                var hr = new System.Random(b);
                for (int h = 0; h < 2; h++) { int s0 = (int)((t + h * beat * 0.5f) * Rate); for (int i = 0; i < Rate * 0.04f && s0 + i < n; i++) d[s0 + i] += ((float)hr.NextDouble() * 2 - 1) * 0.04f * energy * Mathf.Exp(-i / (Rate * 0.01f)); }
            }
            float max = 0.001f; foreach (var v in d) max = Mathf.Max(max, Mathf.Abs(v));
            for (int i = 0; i < n; i++) d[i] = d[i] / max * 0.7f;
            Crossfade(d, Rate / 20);
            return Clip(name, d);
        }

        /// <summary>Bird chirps over a soft breeze (loopable).</summary>
        public static AudioClip Birds(string name, float len)
        {
            int n = (int)(len * Rate); var d = new float[n]; var r = new System.Random(7);
            for (int c = 0; c < 10; c++)
            {
                int s0 = r.Next(0, n - Rate / 2); float f = 2600 + r.Next(0, 1400); float ph = 0; int chirps = 2 + r.Next(3);
                for (int k = 0; k < chirps; k++) for (int i = 0; i < Rate * 0.07f; i++)
                {
                    int idx = s0 + k * (int)(Rate * 0.1f) + i; if (idx >= n) break; float t = i / (float)Rate;
                    ph += (f + 1200 * t / 0.07f) / Rate; d[idx] += Mathf.Sin(ph * Mathf.PI * 2) * 0.12f * Mathf.Sin(Mathf.PI * t / 0.07f);
                }
            }
            Crossfade(d, Rate / 4);
            return Clip(name, d);
        }

        private static void Crossfade(float[] d, int len)
        {
            len = Mathf.Min(len, d.Length / 4);
            // Blend the tail into the head so the loop point is seamless.
            for (int i = 0; i < len; i++) { float k = i / (float)len; d[i] = d[i] * k + d[d.Length - len + i] * (1 - k); }
        }

        private class Biquad
        {
            float a0, a1, a2, b1, b2, x1, x2, y1, y2;
            public void SetBandpass(float f, float q, float rate)
            {
                float w = 2 * Mathf.PI * f / rate, alpha = Mathf.Sin(w) / (2 * q), cos = Mathf.Cos(w), norm = 1 / (1 + alpha);
                a0 = alpha * norm; a1 = 0; a2 = -alpha * norm; b1 = -2 * cos * norm; b2 = (1 - alpha) * norm;
            }
            public float Process(float x) { float y = a0 * x + a1 * x1 + a2 * x2 - b1 * y1 - b2 * y2; x2 = x1; x1 = x; y2 = y1; y1 = y; return y; }
        }
    }
}
