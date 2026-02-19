using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    /// <summary>
    /// Générateur procédural de boucles musicales orientées jazz-fusion / acid jazz.
    /// Mélodie: chaîne de Markov avec accent sur notes de couleur (7e, 9e, 11e).
    /// Rythme: groove syncopé (kick/snare ghost notes/charley shuffle).
    /// Harmonie: progression modale enrichie selon le niveau d'ambiance (0..1).
    /// Texture: electric piano + clavinet + basse ronde + bruit analogique subtil.
    /// </summary>
    public sealed class ProceduralMusicGenerator
    {
        private readonly Random random;

        public ProceduralMusicGenerator(int seed)
        {
            random = new Random(seed);
        }

        public SoundEffect GenerateCombatLoop(float ambienceLevel)
        {
            ambienceLevel = MathHelper.Clamp(ambienceLevel, 0f, 1f);

            const int sampleRate = 44100;
            const float durationSeconds = 12f;
            int sampleCount = (int)(sampleRate * durationSeconds);
            short[] samples = new short[sampleCount];

            float bpm = MathHelper.Lerp(96f, 114f, ambienceLevel);
            float beatsPerSecond = bpm / 60f;
            float secondsPerBeat = 1f / beatsPerSecond;

            int[] scale = BuildAdaptiveScale(ambienceLevel);
            int[] progression = BuildAdaptiveProgression(ambienceLevel);
            int[] melodyDegrees = BuildMarkovMelodyDegrees(ambienceLevel, bars: 12);
            float[] melodyRhythm = BuildMelodyRhythm(ambienceLevel);
            float[] pulseRhythm = BuildPulseRhythm(ambienceLevel);

            float[] perlinSeeds = { (float)random.NextDouble() * 9f, (float)random.NextDouble() * 9f + 10f };

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float beatPos = t / secondsPerBeat;
                int beatIndex = (int)beatPos;
                float beatPhase = beatPos - beatIndex;

                int barIndex = beatIndex / 4;
                int chordIndex = progression[barIndex % progression.Length];

                float rhythmNoise = FractalPerlin1D(t * MathHelper.Lerp(0.9f, 1.8f, ambienceLevel), perlinSeeds[0]);
                float hatGate = MathHelper.Clamp((rhythmNoise + 1f) * 0.5f, 0f, 1f);

                float kick = 0f;
                float snare = 0f;
                float hat = 0f;

                if (beatPhase < 0.1f)
                {
                    float env = MathF.Exp(-30f * beatPhase);
                    float freq = 52f + (1f - beatPhase / 0.1f) * 38f;
                    kick = MathF.Sin(2f * MathF.PI * freq * beatPhase * secondsPerBeat) * env;
                }

                if (beatPhase > 0.74f && beatPhase < 0.8f)
                {
                    float local = beatPhase - 0.74f;
                    float env = MathF.Exp(-45f * local);
                    kick += MathF.Sin(2f * MathF.PI * 64f * local * secondsPerBeat) * env * 0.32f;
                }

                float snareStart = 0.5f;
                if (beatPhase > snareStart && beatPhase < snareStart + 0.08f)
                {
                    float local = beatPhase - snareStart;
                    float env = MathF.Exp(-24f * local);
                    snare = (((float)random.NextDouble() * 2f) - 1f) * env * 0.72f;
                }

                if (beatPhase > 0.32f && beatPhase < 0.36f)
                {
                    float local = beatPhase - 0.32f;
                    float env = MathF.Exp(-52f * local);
                    snare += ((((float)random.NextDouble() * 2f) - 1f) * env) * 0.2f;
                }

                float hatSubdivision = (t / (secondsPerBeat * 0.33333334f)) % 1f;
                if (hatSubdivision < 0.09f)
                {
                    float swing = ((beatIndex & 1) == 0) ? 1f : 0.82f;
                    float env = MathF.Exp(-58f * hatSubdivision);
                    hat = ((((float)random.NextDouble() * 2f) - 1f) * env) * (0.25f + 0.7f * hatGate);
                    hat *= swing;
                }

                float bassFreq = FrequencyFromDegree(scale, chordIndex, octaveOffset: -2);
                float bassEnvelope = MathF.Pow(MathF.Max(0f, 1f - beatPhase), 1.8f);
                float bass = (
                    MathF.Sin(2f * MathF.PI * bassFreq * t) +
                    0.22f * MathF.Sin(2f * MathF.PI * bassFreq * 2f * t) +
                    0.12f * MathF.Sin(2f * MathF.PI * bassFreq * 3f * t)
                ) * bassEnvelope;

                float subBassFreq = FrequencyFromDegree(scale, chordIndex, octaveOffset: -3);
                float subBass = MathF.Sin(2f * MathF.PI * subBassFreq * t) * (0.38f + ambienceLevel * 0.18f);

                int melodyIndex = FindMelodyIndex(t, secondsPerBeat, melodyRhythm);
                int melodyDegree = melodyDegrees[melodyIndex % melodyDegrees.Length];
                float melodyFreq = FrequencyFromDegree(scale, melodyDegree + chordIndex, octaveOffset: 0);
                float melodyLocal = MelodyLocalPhase(t, secondsPerBeat, melodyRhythm);
                float melodyEnv = MathF.Exp(-5.2f * melodyLocal) * MathHelper.Lerp(0.5f, 1f, ambienceLevel);
                float vibrato = MathF.Sin(2f * MathF.PI * (5.5f + ambienceLevel * 2f) * t) * (0.0025f + 0.0035f * ambienceLevel);
                float electricPiano = (
                    MathF.Sin(2f * MathF.PI * melodyFreq * (t + vibrato)) +
                    0.35f * MathF.Sin(2f * MathF.PI * melodyFreq * 2f * (t + vibrato))
                ) * melodyEnv;

                float clav = MathF.Sign(MathF.Sin(2f * MathF.PI * melodyFreq * 0.5f * t))
                    * MathF.Sin(2f * MathF.PI * melodyFreq * t) * melodyEnv * (0.22f + ambienceLevel * 0.12f);

                float melody = electricPiano + clav;

                int pulseIndex = FindMelodyIndex(t, secondsPerBeat, pulseRhythm);
                int pulseDegree = chordIndex + (pulseIndex % 2 == 0 ? 0 : 4);
                float pulseFreq = FrequencyFromDegree(scale, pulseDegree, octaveOffset: 0);
                float pulseLocal = MelodyLocalPhase(t, secondsPerBeat, pulseRhythm);
                float pulseGate = pulseLocal < 0.18f ? 1f - (pulseLocal / 0.18f) : 0f;
                float pulse = (
                    MathF.Sin(2f * MathF.PI * pulseFreq * t) +
                    0.5f * MathF.Sin(2f * MathF.PI * pulseFreq * 2.01f * t)
                ) * pulseGate * (0.24f + ambienceLevel * 0.25f);

                float padNoise = FractalPerlin1D(t * 0.22f, perlinSeeds[1]);
                float padDetune = 1f + padNoise * 0.01f;
                float chordRoot = FrequencyFromDegree(scale, chordIndex, octaveOffset: -1);
                float chordThird = FrequencyFromDegree(scale, chordIndex + 2, octaveOffset: -1);
                float chordFifth = FrequencyFromDegree(scale, chordIndex + 4, octaveOffset: -1);
                float pad = (
                    MathF.Sin(2f * MathF.PI * chordRoot * t * padDetune) +
                    MathF.Sin(2f * MathF.PI * chordThird * t * (2f - padDetune)) +
                    MathF.Sin(2f * MathF.PI * chordFifth * t)
                ) / 3f;
                pad *= 0.42f + 0.28f * (1f - ambienceLevel);

                float padNinth = FrequencyFromDegree(scale, chordIndex + 8, octaveOffset: -1);
                pad += MathF.Sin(2f * MathF.PI * padNinth * t * (1.003f - padNoise * 0.003f)) * 0.14f;

                float sidechain = 1f - MathF.Exp(-8.5f * MathF.Max(0f, 0.16f - beatPhase));
                float ambienceTexture = (FractalPerlin1D(t * 7.2f, perlinSeeds[1] + 4.2f) * 0.06f) * (0.2f + ambienceLevel * 0.8f);

                float mix =
                    kick * 0.38f +
                    snare * 0.24f +
                    hat * 0.2f +
                    bass * 0.3f +
                    subBass * 0.1f +
                    melody * 0.36f +
                    pulse * 0.18f +
                    pad * 0.24f +
                    ambienceTexture;

                mix *= sidechain;

                mix = MathHelper.Clamp(mix, -1f, 1f);
                samples[i] = (short)(mix * short.MaxValue * 0.84f);
            }

            return new SoundEffect(ConvertPcm16ToBytes(samples), sampleRate, AudioChannels.Mono);
        }

        private static int[] BuildAdaptiveScale(float ambienceLevel)
        {
            if (ambienceLevel > 0.66f)
                return new[] { 0, 2, 3, 5, 7, 9, 10 }; // dorien tendu (acid-jazz nocturne)

            if (ambienceLevel > 0.33f)
                return new[] { 0, 2, 4, 5, 7, 9, 10 }; // mixolydien dominant souple

            return new[] { 0, 2, 4, 5, 7, 9, 11 }; // majeur 7/9 lumineux
        }

        private static int[] BuildAdaptiveProgression(float ambienceLevel)
        {
            if (ambienceLevel > 0.66f)
                return new[] { 0, 4, 1, 5, 0, 6, 3, 5 };

            if (ambienceLevel > 0.33f)
                return new[] { 0, 4, 5, 3, 0, 2, 6, 4 };

            return new[] { 0, 5, 3, 4, 0, 2, 5, 4 };
        }

        private int[] BuildMarkovMelodyDegrees(float ambienceLevel, int bars)
        {
            int notes = bars * 4;
            int[] sequence = new int[notes];
            int currentState = random.Next(0, 7);

            float[,] transitions = BuildTransitionMatrix(ambienceLevel);

            for (int i = 0; i < notes; i++)
            {
                sequence[i] = currentState;
                currentState = NextStateFromMatrix(transitions, currentState);
            }

            return sequence;
        }

        private static float[,] BuildTransitionMatrix(float ambienceLevel)
        {
            float tension = ambienceLevel;
            return new float[,]
            {
                { 0.12f, 0.24f, 0.08f, 0.26f, 0.12f, 0.10f, 0.08f },
                { 0.18f, 0.06f, 0.20f, 0.10f, 0.20f, 0.14f, 0.12f },
                { 0.10f, 0.16f, 0.05f, 0.19f, 0.15f, 0.18f + 0.08f * tension, 0.17f - 0.08f * tension },
                { 0.22f, 0.10f, 0.14f, 0.04f, 0.21f, 0.11f, 0.18f },
                { 0.20f, 0.08f, 0.16f, 0.18f, 0.05f, 0.16f, 0.17f },
                { 0.12f, 0.16f, 0.20f, 0.08f, 0.24f, 0.03f, 0.17f },
                { 0.26f, 0.12f, 0.14f, 0.16f, 0.18f, 0.10f, 0.04f }
            };
        }

        private int NextStateFromMatrix(float[,] matrix, int current)
        {
            float roll = (float)random.NextDouble();
            float cumulative = 0f;
            for (int i = 0; i < 7; i++)
            {
                cumulative += matrix[current, i];
                if (roll <= cumulative)
                    return i;
            }

            return 0;
        }

        private static float[] BuildMelodyRhythm(float ambienceLevel)
        {
            return ambienceLevel > 0.66f
                ? new[] { 0.5f, 0.25f, 0.25f, 0.75f, 0.5f, 0.75f }
                : ambienceLevel > 0.33f
                    ? new[] { 0.75f, 0.25f, 0.5f, 0.5f, 1f }
                    : new[] { 1f, 0.5f, 0.25f, 0.75f };
        }

        private static float[] BuildPulseRhythm(float ambienceLevel)
        {
            return ambienceLevel > 0.66f
                ? new[] { 0.25f, 0.25f, 0.5f, 0.25f, 0.25f, 0.5f }
                : ambienceLevel > 0.33f
                    ? new[] { 0.5f, 0.25f, 0.25f, 0.75f, 0.25f }
                    : new[] { 0.75f, 0.25f, 0.5f, 0.5f };
        }

        private static int FindMelodyIndex(float t, float secondsPerBeat, IReadOnlyList<float> rhythm)
        {
            float cycleBeats = 0f;
            for (int i = 0; i < rhythm.Count; i++)
                cycleBeats += rhythm[i];

            float beats = t / secondsPerBeat;
            float inCycle = beats % cycleBeats;

            float cursor = 0f;
            for (int i = 0; i < rhythm.Count; i++)
            {
                float next = cursor + rhythm[i];
                if (inCycle < next)
                    return i;
                cursor = next;
            }

            return 0;
        }

        private static float MelodyLocalPhase(float t, float secondsPerBeat, IReadOnlyList<float> rhythm)
        {
            float cycleBeats = 0f;
            for (int i = 0; i < rhythm.Count; i++)
                cycleBeats += rhythm[i];

            float beats = t / secondsPerBeat;
            float inCycle = beats % cycleBeats;

            float cursor = 0f;
            for (int i = 0; i < rhythm.Count; i++)
            {
                float next = cursor + rhythm[i];
                if (inCycle < next)
                    return (inCycle - cursor) / MathF.Max(0.0001f, rhythm[i]);

                cursor = next;
            }

            return 0f;
        }

        private static float FrequencyFromDegree(int[] scale, int degree, int octaveOffset)
        {
            int scaleIndex = ((degree % scale.Length) + scale.Length) % scale.Length;
            int octave = (int)MathF.Floor(degree / (float)scale.Length) + octaveOffset;
            int semitonesFromA3 = scale[scaleIndex] + octave * 12;
            return 220f * MathF.Pow(2f, semitonesFromA3 / 12f);
        }

        private static byte[] ConvertPcm16ToBytes(IReadOnlyList<short> samples)
        {
            byte[] bytes = new byte[samples.Count * sizeof(short)];
            for (int i = 0; i < samples.Count; i++)
            {
                short sample = samples[i];
                int offset = i * sizeof(short);
                bytes[offset] = (byte)(sample & 0xFF);
                bytes[offset + 1] = (byte)((sample >> 8) & 0xFF);
            }

            return bytes;
        }

        private static float FractalPerlin1D(float x, float seed)
        {
            float total = 0f;
            float frequency = 1f;
            float amplitude = 1f;
            float max = 0f;

            for (int i = 0; i < 4; i++)
            {
                total += Perlin1D((x + seed) * frequency) * amplitude;
                max += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            return max <= 0f ? 0f : total / max;
        }

        private static float Perlin1D(float x)
        {
            int x0 = (int)MathF.Floor(x);
            int x1 = x0 + 1;
            float t = x - x0;
            float fade = t * t * t * (t * (t * 6f - 15f) + 10f);

            float g0 = Gradient(Hash(x0), t);
            float g1 = Gradient(Hash(x1), t - 1f);

            return MathHelper.Lerp(g0, g1, fade);
        }

        private static int Hash(int x)
        {
            unchecked
            {
                int h = x;
                h = (h << 13) ^ h;
                return (h * (h * h * 15731 + 789221) + 1376312589) & 0x7fffffff;
            }
        }

        private static float Gradient(int hash, float x)
        {
            return ((hash & 1) == 0 ? x : -x);
        }
    }
}
