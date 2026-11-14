using UnityEngine;

namespace WSFGameJam2026.Audio
{
    /// <summary>
    /// Builds sound effects in code, without audio files (port of the LD57 ProceduralSfxBank synthesis).
    /// Layers: body (detuned oscillators with a pitch sweep), noise transient, sub, bell, then a small reverb,
    /// tanh saturation and normalization.
    /// </summary>
    public class SfxSynthesizer
    {
        public const int SampleRate = 44100;

        private const float TwoPi = 2f * Mathf.PI;

        private static readonly float[] ArpLevelUp = { 523f, 659f, 784f, 1047f };
        private static readonly float[] ArpWin = { 523f, 659f, 784f, 1047f, 1319f, 1568f };
        private static readonly float[] Chord = { 523f, 659f, 784f };
        private static readonly float[] ReverbDelaysMs = { 23f, 37f, 53f, 71f };

        private readonly float reverbMix;
        private readonly float drive;
        private readonly System.Random random;

        public SfxSynthesizer(float reverbMix, float drive, int seed = 12345)
        {
            this.reverbMix = reverbMix;
            this.drive = drive;
            random = new System.Random(seed);
        }

        /// <summary>Synthesizes the recipe. Returns null for <see cref="SfxRecipe.None"/>.</summary>
        public AudioClip Build(SfxRecipe recipe)
        {
            float[] buffer;
            switch (recipe)
            {
                case SfxRecipe.Dash:
                    buffer = CreateBuffer(0.42f);
                    AddNoise(buffer, 0f, 0.3f, 400f, 2500f, 600f, 1f, 0.06f, 0.14f, true);
                    AddSub(buffer, 0f, 0.2f, 50f, 0.25f, 0.1f);
                    return Finish("dash", buffer, false, false);

                case SfxRecipe.Attack:
                    buffer = CreateBuffer(0.28f);
                    AddNoise(buffer, 0f, 0.12f, 5000f, 2500f, 1200f, 0.6f, 0.004f, 0.05f);
                    AddBody(buffer, 0f, 0.15f, 500f, 150f, 0.9f, 0.004f, 0.07f, 0.4f);
                    return Finish("attack", buffer, false, false);

                case SfxRecipe.Hit:
                    buffer = CreateBuffer(0.32f);
                    AddNoise(buffer, 0f, 0.06f, 6000f, 2500f, 800f, 0.8f, 0.002f, 0.015f);
                    AddSub(buffer, 0f, 0.18f, 60f, 1f, 0.08f);
                    AddBody(buffer, 0f, 0.18f, 220f, 90f, 0.8f, 0.003f, 0.06f, 0.2f);
                    return Finish("hit", buffer, true, false);

                case SfxRecipe.PlayerHurt:
                    buffer = CreateBuffer(0.5f);
                    AddNoise(buffer, 0f, 0.08f, 5000f, 2000f, 800f, 0.6f, 0.002f, 0.02f);
                    AddSub(buffer, 0f, 0.3f, 55f, 1f, 0.12f);
                    AddBody(buffer, 0f, 0.3f, 110f, 55f, 1f, 0.003f, 0.12f, 0.7f);
                    return Finish("hurt", buffer, true, false, 1.3f);

                case SfxRecipe.Windup:
                    buffer = CreateBuffer(0.62f);
                    AddBody(buffer, 0f, 0.5f, 180f, 520f, 0.8f, 0.05f, 1.2f, 0.4f, 6f, 0.03f);
                    AddNoise(buffer, 0f, 0.5f, 800f, 1800f, 3000f, 0.35f, 0.45f, 5f);
                    return Finish("windup", buffer, false, false);

                case SfxRecipe.Cast:
                    // A spell: an airy upward whoosh and a short bell sparkle (no sword metal).
                    buffer = CreateBuffer(0.42f);
                    AddNoise(buffer, 0f, 0.26f, 700f, 2600f, 4200f, 0.45f, 0.03f, 0.12f);
                    AddBell(buffer, 0.04f, 0.3f, 880f, 0.5f, 0.1f);
                    AddBell(buffer, 0.08f, 0.3f, 1320f, 0.35f, 0.09f);
                    return Finish("cast", buffer, false, false);

                case SfxRecipe.Shot:
                    buffer = CreateBuffer(0.24f);
                    AddNoise(buffer, 0f, 0.04f, 7000f, 3000f, 1500f, 0.9f, 0.002f, 0.012f);
                    AddBody(buffer, 0f, 0.12f, 1200f, 300f, 0.8f, 0.003f, 0.04f, 0.3f);
                    return Finish("shot", buffer, false, false);

                case SfxRecipe.GateOpen:
                    buffer = CreateBuffer(1.9f);
                    AddSub(buffer, 0f, 1f, 45f, 1f, 0.4f, 0.9f);
                    AddNoise(buffer, 0f, 1f, 300f, 220f, 120f, 0.9f, 0.03f, 0.4f);
                    AddNoise(buffer, 0f, 0.05f, 5000f, 2500f, 1000f, 0.5f, 0.002f, 0.012f);
                    AddBody(buffer, 0.02f, 0.25f, 1100f, 1100f, 0.25f, 0.003f, 0.08f, 0f);
                    AddBody(buffer, 0.02f, 0.25f, 1650f, 1650f, 0.2f, 0.003f, 0.07f, 0f);
                    return Finish("gate", buffer, true, true);

                case SfxRecipe.Heart:
                    buffer = CreateBuffer(0.75f);
                    AddBell(buffer, 0f, 0.5f, 660f, 0.8f, 0.18f);
                    AddBell(buffer, 0.1f, 0.5f, 990f, 0.7f, 0.18f);
                    return Finish("heart", buffer, false, false);

                case SfxRecipe.LevelUp:
                    buffer = CreateBuffer(1.7f);
                    foreach (float frequency in Chord)
                        AddBody(buffer, 0f, 0.8f, frequency, frequency, 0.35f, 0.15f, 0.5f, 0.5f);

                    for (int i = 0; i < ArpLevelUp.Length; i++)
                        AddBody(buffer, 0.15f + i * 0.09f, 0.5f, ArpLevelUp[i], ArpLevelUp[i], 0.45f, 0.004f, 0.18f, 0.3f);

                    return Finish("levelup", buffer, false, true);

                case SfxRecipe.CardPick:
                    buffer = CreateBuffer(0.2f);
                    AddNoise(buffer, 0f, 0.02f, 4000f, 2500f, 1500f, 0.4f, 0.002f, 0.01f);
                    AddBody(buffer, 0f, 0.08f, 700f, 500f, 0.9f, 0.003f, 0.03f, 0.1f);
                    return Finish("card", buffer, false, false);

                case SfxRecipe.WaveCleared:
                    buffer = CreateBuffer(0.95f);
                    AddBody(buffer, 0f, 0.3f, 784f, 784f, 0.7f, 0.004f, 0.12f, 0.4f);
                    AddBody(buffer, 0.14f, 0.45f, 523f, 523f, 0.8f, 0.004f, 0.2f, 0.4f);
                    return Finish("wave", buffer, false, true);

                case SfxRecipe.Death:
                    buffer = CreateBuffer(2f);
                    AddBody(buffer, 0f, 1f, 200f, 40f, 1f, 0.005f, 0.6f, 0.5f);
                    AddNoise(buffer, 0f, 0.8f, 3000f, 800f, 200f, 0.5f, 0.004f, 0.3f);
                    AddSub(buffer, 0f, 1f, 40f, 0.9f, 0.5f, 0.9f);
                    return Finish("death", buffer, true, true);

                case SfxRecipe.Win:
                    buffer = CreateBuffer(2.4f);
                    for (int i = 0; i < ArpWin.Length; i++)
                        AddBody(buffer, i * 0.1f, 0.6f, ArpWin[i], ArpWin[i], 0.4f, 0.004f, 0.2f, 0.3f);

                    foreach (float frequency in Chord)
                        AddBody(buffer, 0.6f, 0.9f, frequency, frequency, 0.4f, 0.05f, 0.5f, 0.5f);

                    return Finish("win", buffer, false, true);

                case SfxRecipe.BossTelegraph:
                    buffer = CreateBuffer(1.7f);
                    AddSub(buffer, 0f, 0.9f, 55f, 1f, 3f, 1f, 5f);
                    AddNoise(buffer, 0f, 0.9f, 300f, 300f, 300f, 0.8f, 0.05f, 3f, false, 5f);
                    return Finish("boss", buffer, true, true);

                case SfxRecipe.EnemyDie:
                    buffer = CreateBuffer(0.35f);
                    AddNoise(buffer, 0f, 0.15f, 3000f, 1200f, 400f, 0.9f, 0.003f, 0.05f);
                    AddBody(buffer, 0f, 0.15f, 350f, 90f, 0.7f, 0.003f, 0.05f, 0.3f);
                    AddSub(buffer, 0f, 0.15f, 60f, 0.7f, 0.08f);
                    return Finish("enemydie", buffer, true, false);
            }

            return null;
        }

        private float RandomSigned()
        {
            return (float)(random.NextDouble() * 2.0 - 1.0);
        }

        private static float[] CreateBuffer(float seconds)
        {
            return new float[Mathf.CeilToInt(seconds * SampleRate)];
        }

        private static float AttackEnvelope(float time, float attack)
        {
            return Mathf.Min(1f, time / Mathf.Max(0.001f, attack));
        }

        /// <summary>Envelope with a linear attack, exponential decay and a 6 ms fade at the end of the layer.</summary>
        private static float Envelope(float time, float duration, float attack, float decay)
        {
            return AttackEnvelope(time, attack) * Mathf.Exp(-time / decay) * Mathf.Min(1f, (duration - time) / 0.006f);
        }

        private static int SampleCount(float[] buffer, int start, float duration)
        {
            return Mathf.Min(buffer.Length - start, Mathf.CeilToInt(duration * SampleRate));
        }

        /// <summary>Body: two detuned oscillators, sine/triangle mix, exponential pitch sweep, optional vibrato.</summary>
        private static void AddBody(float[] buffer, float start, float duration, float startFrequency, float endFrequency, float amplitude,
            float attack, float decay, float triangle = 0.3f, float vibratoHz = 0f, float vibratoDepth = 0f)
        {
            int offset = Mathf.RoundToInt(start * SampleRate);
            int count = SampleCount(buffer, offset, duration);
            float phaseA = 0f;
            float phaseB = 0f;
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)SampleRate;
                float frequency = startFrequency * Mathf.Pow(endFrequency / startFrequency, time / duration);
                if (vibratoHz > 0f)
                    frequency *= 1f + vibratoDepth * Mathf.Sin(TwoPi * vibratoHz * time);

                phaseA += TwoPi * frequency * 0.993f / SampleRate;
                phaseB += TwoPi * frequency * 1.007f / SampleRate;
                float oscillatorA = Mathf.Lerp(Mathf.Sin(phaseA), 2f / Mathf.PI * Mathf.Asin(Mathf.Sin(phaseA)), triangle);
                float oscillatorB = Mathf.Lerp(Mathf.Sin(phaseB), 2f / Mathf.PI * Mathf.Asin(Mathf.Sin(phaseB)), triangle);
                buffer[offset + i] += 0.5f * (oscillatorA + oscillatorB) * Envelope(time, duration, attack, decay) * amplitude;
            }
        }

        /// <summary>
        /// Noise transient: white noise through a low pass filter (cutoff startCutoff → midCutoff → endCutoff, exponential).
        /// Band also cuts the lows.
        /// </summary>
        private void AddNoise(float[] buffer, float start, float duration, float startCutoff, float midCutoff, float endCutoff, float amplitude,
            float attack, float decay, bool band = false, float pulseHz = 0f)
        {
            int offset = Mathf.RoundToInt(start * SampleRate);
            int count = SampleCount(buffer, offset, duration);
            float lowPass = 0f;
            float lows = 0f;
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)SampleRate;
                float progress = time / duration;
                float cutoff = progress < 0.5f
                    ? startCutoff * Mathf.Pow(midCutoff / startCutoff, progress * 2f)
                    : midCutoff * Mathf.Pow(endCutoff / midCutoff, (progress - 0.5f) * 2f);

                float coefficient = 1f - Mathf.Exp(-TwoPi * cutoff / SampleRate);
                lowPass += coefficient * (RandomSigned() - lowPass);
                float sample = lowPass;
                if (band)
                {
                    float lowsCoefficient = 1f - Mathf.Exp(-TwoPi * (cutoff * 0.25f) / SampleRate);
                    lows += lowsCoefficient * (lowPass - lows);
                    sample = lowPass - lows;
                }

                // Evens out the loudness lost in the filter.
                float normalization = Mathf.Min(1f / Mathf.Sqrt(Mathf.Max(0.02f, coefficient)), 6f);
                float envelope = Envelope(time, duration, attack, decay);
                if (pulseHz > 0f)
                    envelope *= 0.6f + 0.4f * Mathf.Cos(TwoPi * pulseHz * time);

                buffer[offset + i] += sample * normalization * 0.5f * envelope * amplitude;
            }
        }

        /// <summary>Sub: a 40-70 Hz sine with a fast decay, a slight pitch drop and an optional pulse.</summary>
        private static void AddSub(float[] buffer, float start, float duration, float frequency, float amplitude, float decay,
            float drop = 0.8f, float pulseHz = 0f)
        {
            int offset = Mathf.RoundToInt(start * SampleRate);
            int count = SampleCount(buffer, offset, duration);
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)SampleRate;
                phase += TwoPi * Mathf.Lerp(frequency, frequency * drop, time / duration) / SampleRate;
                float envelope = Envelope(time, duration, 0.004f, decay);
                if (pulseHz > 0f)
                    envelope *= 0.55f + 0.45f * Mathf.Cos(TwoPi * pulseHz * time);

                buffer[offset + i] += Mathf.Sin(phase) * envelope * amplitude;
            }
        }

        /// <summary>Bell: a sine with its 2nd harmonic at 0.4.</summary>
        private static void AddBell(float[] buffer, float start, float duration, float frequency, float amplitude, float decay)
        {
            int offset = Mathf.RoundToInt(start * SampleRate);
            int count = SampleCount(buffer, offset, duration);
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)SampleRate;
                float tone = Mathf.Sin(TwoPi * frequency * time) + 0.4f * Mathf.Sin(TwoPi * 2f * frequency * time);
                buffer[offset + i] += tone * 0.7f * Envelope(time, duration, 0.003f, decay) * amplitude;
            }
        }

        /// <summary>Reverb (4 feedback delays with a filter in the loop), tanh soft clip, normalization to 0.8, fade out.</summary>
        private AudioClip Finish(string name, float[] dry, bool impact, bool longTail, float driveScale = 1f)
        {
            int count = dry.Length;
            float feedback = longTail ? 0.4f : 0.28f;
            float stretch = longTail ? 1.6f : 1f;
            float mix = Mathf.Clamp01(reverbMix * (longTail ? 1.25f : 1f));
            float[] wet = new float[count];
            float[] line = new float[count];
            for (int d = 0; d < ReverbDelaysMs.Length; d++)
            {
                int delay = Mathf.Max(1, Mathf.RoundToInt(ReverbDelaysMs[d] * stretch * 0.001f * SampleRate));
                float filterState = 0f;
                for (int i = 0; i < count; i++)
                {
                    float fed = 0f;
                    if (i >= delay)
                    {
                        filterState += 0.35f * (line[i - delay] - filterState);
                        fed = filterState * feedback;
                    }

                    line[i] = dry[i] + fed;
                    wet[i] += line[i] * 0.3f;
                }
            }

            float saturation = (impact ? drive : drive * 0.67f) * driveScale;
            float[] output = new float[count];
            float peak = 0f;
            for (int i = 0; i < count; i++)
            {
                float sample = (float)System.Math.Tanh((dry[i] * (1f - mix * 0.5f) + wet[i] * mix) * saturation);
                output[i] = sample;
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }

            float gain = peak > 0.0001f ? 0.8f / peak : 1f;
            int fade = Mathf.Max(1, Mathf.Min(count, Mathf.RoundToInt(0.01f * SampleRate)));
            for (int i = 0; i < count; i++)
                output[i] *= gain * (i >= count - fade ? (count - i) / (float)fade : 1f);

            var clip = AudioClip.Create("sfx_" + name, count, 1, SampleRate, false);
            clip.SetData(output, 0);
            return clip;
        }
    }
}
