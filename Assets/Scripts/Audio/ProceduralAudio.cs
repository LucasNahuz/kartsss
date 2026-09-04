using System.Collections.Generic;
using UnityEngine;
using VortexKarts.Data;

namespace VortexKarts.Audio
{
    /// <summary>
    /// Synthesises every sound in the game at startup: engine loops, skids, boosts, impacts, UI blips and
    /// short music loops per theme. No external audio assets are needed and everything is original.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 44100;
        private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        private static AudioClip Make(string name, float[] data)
        {
            Normalize(data, 0.9f);
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            cache[name] = clip;
            return clip;
        }

        public static AudioClip Get(string name)
        {
            AudioClip c;
            if (cache.TryGetValue(name, out c) && c != null) return c;
            switch (name)
            {
                case "engine": return Make(name, EngineLoop());
                case "drift": return Make(name, DriftLoop());
                case "boost": return Make(name, Whoosh(0.8f, 300f, 1600f));
                case "megaboost": return Make(name, Whoosh(1.4f, 200f, 2200f));
                case "impact": return Make(name, Impact(0.28f, 90f));
                case "impact_soft": return Make(name, Impact(0.18f, 140f));
                case "explosion": return Make(name, Explosion());
                case "pickup": return Make(name, Blip(new[] { 660f, 990f }, 0.12f));
                case "use": return Make(name, Sweep(900f, 280f, 0.3f));
                case "shield_on": return Make(name, Blip(new[] { 440f, 660f, 880f }, 0.1f));
                case "shield_block": return Make(name, Blip(new[] { 1200f, 600f }, 0.09f));
                case "emp": return Make(name, EmpZap());
                case "lap": return Make(name, Blip(new[] { 523f, 659f, 784f, 1047f }, 0.09f));
                case "countdown": return Make(name, Tone(440f, 0.25f));
                case "go": return Make(name, Tone(880f, 0.6f));
                case "ui_click": return Make(name, Blip(new[] { 1000f, 1400f }, 0.04f));
                case "ui_move": return Make(name, Tone(700f, 0.05f));
                case "respawn": return Make(name, Sweep(200f, 800f, 0.5f));
                case "land": return Make(name, Impact(0.14f, 70f));
                case "drift_release": return Make(name, Sweep(400f, 1200f, 0.25f));
                case "wrongway": return Make(name, Tone(220f, 0.3f));
                case "finish": return Make(name, Fanfare());
                case "music_menu": return Make(name, Music(0, 112f, 16f));
                case "music_neon": return Make(name, Music(1, 128f, 24f));
                case "music_canyon": return Make(name, Music(2, 120f, 24f));
                case "music_sky": return Make(name, Music(3, 136f, 24f));
            }
            return null;
        }

        public static AudioClip MusicFor(TrackTheme theme)
        {
            switch (theme)
            {
                case TrackTheme.NeonMetro: return Get("music_neon");
                case TrackTheme.SolarCanyon: return Get("music_canyon");
                default: return Get("music_sky");
            }
        }

        // ------------------------------------------------------------------ Generators

        private static void Normalize(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            if (max < 1e-5f) return;
            float g = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= g;
        }

        private static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));
        private static float Square(float phase) => Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f;
        private static float Tri(float phase) => 1f - 4f * Mathf.Abs(Mathf.Repeat(phase, 1f) - 0.5f);

        private static float[] EngineLoop()
        {
            // One second loop of a 4-stroke-ish rumble at 60 Hz base (pitch shifted at runtime).
            int n = SampleRate;
            var d = new float[n];
            System.Random r = new System.Random(7);
            float baseF = 60f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float p = t * baseF;
                float v = Saw(p) * 0.5f + Saw(p * 2f) * 0.25f + Square(p * 0.5f) * 0.18f + Tri(p * 3f) * 0.1f;
                v += ((float)r.NextDouble() * 2f - 1f) * 0.08f;
                d[i] = v;
            }
            Fade(d, 64);
            return d;
        }

        private static float[] DriftLoop()
        {
            int n = SampleRate;
            var d = new float[n];
            System.Random r = new System.Random(11);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)r.NextDouble() * 2f - 1f;
                lp += (noise - lp) * 0.25f;
                float screech = Mathf.Sin(t * 2f * Mathf.PI * (1400f + Mathf.Sin(t * 30f) * 200f)) * 0.35f;
                d[i] = lp * 0.7f + screech * 0.5f;
            }
            Fade(d, 128);
            return d;
        }

        private static float[] Whoosh(float seconds, float f0, float f1)
        {
            int n = (int)(SampleRate * seconds);
            var d = new float[n];
            System.Random r = new System.Random(3);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Sin(t * Mathf.PI);
                float noise = (float)r.NextDouble() * 2f - 1f;
                float cutoff = Mathf.Lerp(0.05f, 0.6f, t);
                lp += (noise - lp) * cutoff;
                float f = Mathf.Lerp(f0, f1, t * t);
                float tone = Mathf.Sin(2f * Mathf.PI * f * (i / (float)SampleRate)) * 0.3f;
                d[i] = (lp * 0.9f + tone) * env;
            }
            return d;
        }

        private static float[] Impact(float seconds, float thudHz)
        {
            int n = (int)(SampleRate * seconds);
            var d = new float[n];
            System.Random r = new System.Random(5);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 18f);
                float noise = (float)r.NextDouble() * 2f - 1f;
                float thud = Mathf.Sin(2f * Mathf.PI * thudHz * t * (1f - t * 0.6f));
                d[i] = (noise * 0.6f + thud * 0.8f) * env;
            }
            return d;
        }

        private static float[] Explosion()
        {
            int n = (int)(SampleRate * 0.7f);
            var d = new float[n];
            System.Random r = new System.Random(9);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 6f);
                float noise = (float)r.NextDouble() * 2f - 1f;
                lp += (noise - lp) * 0.12f;
                float boom = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 4f);
                d[i] = (lp * 0.8f + boom * 0.9f) * env;
            }
            return d;
        }

        private static float[] EmpZap()
        {
            int n = (int)(SampleRate * 0.6f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 5f);
                float f = 1800f * Mathf.Exp(-t * 3f) + 120f;
                float v = Square(t * f) * 0.5f + Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.4f;
                d[i] = v * env;
            }
            return d;
        }

        private static float[] Blip(float[] freqs, float each)
        {
            int per = (int)(SampleRate * each);
            var d = new float[per * freqs.Length];
            for (int k = 0; k < freqs.Length; k++)
            {
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Sin(i / (float)per * Mathf.PI);
                    d[k * per + i] = (Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * 0.7f + Tri(t * freqs[k] * 2f) * 0.2f) * env;
                }
            }
            return d;
        }

        private static float[] Tone(float f, float seconds)
        {
            int n = (int)(SampleRate * seconds);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Min(1f, i / 200f) * Mathf.Min(1f, (n - i) / 800f);
                d[i] = (Mathf.Sin(2f * Mathf.PI * f * t) * 0.7f + Square(t * f) * 0.15f) * env;
            }
            return d;
        }

        private static float[] Sweep(float f0, float f1, float seconds)
        {
            int n = (int)(SampleRate * seconds);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(f0, f1, t);
                phase += f / SampleRate;
                float env = Mathf.Sin(t * Mathf.PI);
                d[i] = (Mathf.Sin(2f * Mathf.PI * phase) * 0.6f + Saw(phase) * 0.2f) * env;
            }
            return d;
        }

        private static float[] Fanfare()
        {
            float[] notes = { 523f, 659f, 784f, 1047f, 784f, 1047f, 1319f };
            float[] lens = { 0.15f, 0.15f, 0.15f, 0.3f, 0.15f, 0.15f, 0.6f };
            int total = 0;
            for (int i = 0; i < lens.Length; i++) total += (int)(SampleRate * lens[i]);
            var d = new float[total];
            int idx = 0;
            for (int k = 0; k < notes.Length; k++)
            {
                int per = (int)(SampleRate * lens[k]);
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Min(1f, i / 300f) * Mathf.Min(1f, (per - i) / 1500f);
                    d[idx++] = (Square(t * notes[k]) * 0.3f + Mathf.Sin(2f * Mathf.PI * notes[k] * t) * 0.5f + Tri(t * notes[k] * 0.5f) * 0.3f) * env;
                }
            }
            return d;
        }

        private static void Fade(float[] d, int samples)
        {
            for (int i = 0; i < samples && i < d.Length; i++)
            {
                float g = i / (float)samples;
                d[i] *= g;
                d[d.Length - 1 - i] *= g;
            }
        }

        /// <summary>
        /// Simple chiptune loop: bass on beats, arpeggio on eighths, hats on off-beats. Each theme uses a
        /// different scale and pattern so the tracks feel distinct.
        /// </summary>
        private static float[] Music(int theme, float bpm, float seconds)
        {
            int n = (int)(SampleRate * seconds);
            var d = new float[n];
            float beat = 60f / bpm;
            float eighth = beat * 0.5f;
            // Scales in semitones from the root.
            int[][] scales =
            {
                new[] { 0, 3, 5, 7, 10, 12, 15 },        // menu: minor pentatonic-ish, relaxed
                new[] { 0, 2, 3, 7, 8, 12, 14 },         // neon: dark synth
                new[] { 0, 2, 4, 7, 9, 12, 14 },         // canyon: major pentatonic, sunny
                new[] { 0, 2, 5, 7, 11, 12, 14 }         // sky: airy
            };
            float[] roots = { 110f, 98f, 130.8f, 123.5f };
            int[] scale = scales[Mathf.Clamp(theme, 0, 3)];
            float root = roots[Mathf.Clamp(theme, 0, 3)];
            System.Random r = new System.Random(100 + theme);
            int bars = Mathf.Max(1, (int)(seconds / (beat * 4f)));
            // Chord progression as scale degrees.
            int[] prog = { 0, 0, 3, 4, 0, 5, 3, 4 };

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                int eighthIndex = (int)(t / eighth);
                int beatIndex = (int)(t / beat);
                int bar = beatIndex / 4;
                float tInEighth = (t - eighthIndex * eighth) / eighth;
                float tInBeat = (t - beatIndex * beat) / beat;

                int chordDegree = prog[bar % prog.Length];
                float bassF = root * Mathf.Pow(2f, scale[chordDegree % scale.Length] / 12f) * 0.5f;
                float bassEnv = Mathf.Exp(-tInBeat * 5f);
                float bass = (Saw(t * bassF) * 0.5f + Mathf.Sin(2f * Mathf.PI * bassF * t) * 0.5f) * bassEnv * 0.45f;

                // Arpeggio: cycle chord tones, occasionally jump an octave.
                int arpStep = eighthIndex % 4;
                int degree = (chordDegree + arpStep * 2) % scale.Length;
                float arpF = root * 2f * Mathf.Pow(2f, scale[degree] / 12f) * (arpStep == 3 && theme != 0 ? 2f : 1f);
                float arpEnv = Mathf.Exp(-tInEighth * (theme == 0 ? 3f : 6f));
                float lead = (Square(t * arpF) * 0.25f + Tri(t * arpF) * 0.35f) * arpEnv * 0.35f;

                // Hat on off-beats (not in menu).
                float hat = 0f;
                if (theme != 0 && eighthIndex % 2 == 1)
                {
                    float noise = (float)r.NextDouble() * 2f - 1f;
                    hat = noise * Mathf.Exp(-tInEighth * 40f) * 0.12f;
                }
                // Kick on beats 1 and 3.
                float kick = 0f;
                if (theme != 0 && beatIndex % 2 == 0)
                {
                    kick = Mathf.Sin(2f * Mathf.PI * (60f + 40f * Mathf.Exp(-tInBeat * 30f)) * tInBeat * beat) * Mathf.Exp(-tInBeat * 14f) * 0.5f;
                }
                d[i] = bass + lead + hat + kick;
            }
            Fade(d, 2000);
            return d;
        }
    }
}
