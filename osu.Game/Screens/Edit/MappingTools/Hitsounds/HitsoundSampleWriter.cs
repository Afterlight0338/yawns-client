// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ManagedBass;

namespace osu.Game.Screens.Edit.MappingTools.Hitsounds
{
    /// <summary>
    /// YAWNS: makes the sample files of <see cref="HitsoundExporter"/>: copies, quieter copies and mixes, written as 16-bit PCM WAV.
    /// Mixes go through a soft limiter, as Mapping Tools does for PCM.
    /// </summary>
    public static class HitsoundSampleWriter
    {
        /// <summary>
        /// The bytes of a generated file.
        /// </summary>
        /// <param name="file">The file to make.</param>
        /// <param name="load">Reads a source's bytes (a set file or a default sample). Null when it is missing, which is treated as silence.</param>
        public static byte[] Render(HitsoundExporter.GeneratedFile file, Func<HitsoundExporter.SampleSource, byte[]?> load)
        {
            var audible = file.Layers.Where(l => l.Source.Kind != HitsoundExporter.SourceKind.Blank).ToList();

            // A single sample as it is: copy the bytes (same extension, see HitsoundExporter.Build).
            if (audible.Count == 1 && file.Layers.Count == 1 && audible[0].Amplitude == 1 && load(audible[0].Source) is byte[] raw)
                return raw;

            var decoded = new List<(float[] Samples, int Rate, int Channels, double Amplitude)>();

            foreach (var layer in audible)
            {
                if (load(layer.Source) is byte[] bytes && Decode(bytes) is { } audio)
                    decoded.Add((audio.Samples, audio.Rate, audio.Channels, layer.Amplitude));
            }

            if (decoded.Count == 0)
                return Wav(Array.Empty<short>(), 44100, 1);

            int outRate = decoded.Max(d => d.Rate);
            int outChannels = Math.Min(2, decoded.Max(d => d.Channels));

            var converted = decoded.Select(d => convert(d.Samples, d.Rate, d.Channels, outRate, outChannels)).ToList();
            int length = converted.Max(c => c.Length);
            float[] mix = new float[length];

            for (int i = 0; i < converted.Count; i++)
            {
                float amplitude = (float)decoded[i].Amplitude;
                float[] c = converted[i];

                for (int s = 0; s < c.Length; s++)
                    mix[s] += c[s] * amplitude;
            }

            short[] pcm = new short[length];
            for (int s = 0; s < length; s++)
                pcm[s] = (short)Math.Round(softLimit(mix[s]) * short.MaxValue);

            return Wav(pcm, outRate, outChannels);
        }

        /// <summary>
        /// Decodes any audio BASS reads (wav, ogg, mp3) to interleaved floats. Null when it cannot be read.
        /// </summary>
        public static (float[] Samples, int Rate, int Channels)? Decode(byte[] bytes)
        {
            if (bytes.Length <= 44)
                return null;

            int stream = Bass.CreateStream(bytes, 0, bytes.Length, BassFlags.Decode | BassFlags.Float | BassFlags.Prescan);

            if (stream == 0)
                return null;

            try
            {
                var info = Bass.ChannelGetInfo(stream);
                var samples = new List<float>();
                float[] buffer = new float[16384];

                while (true)
                {
                    int got = Bass.ChannelGetData(stream, buffer, buffer.Length * sizeof(float) | (int)DataFlags.Float);

                    if (got <= 0)
                        break;

                    samples.AddRange(buffer.Take(got / sizeof(float)));
                }

                return (samples.ToArray(), info.Frequency, Math.Max(1, info.Channels));
            }
            finally
            {
                Bass.StreamFree(stream);
            }
        }

        // ponytail: linear resampling, fine for hitsounds; use a proper resampler if mixes of very different rates sound dull.
        private static float[] convert(float[] samples, int rate, int channels, int outRate, int outChannels)
        {
            int frames = samples.Length / channels;
            int outFrames = rate == outRate ? frames : (int)Math.Ceiling(frames * (double)outRate / rate);
            float[] output = new float[outFrames * outChannels];

            for (int f = 0; f < outFrames; f++)
            {
                double position = rate == outRate ? f : f * (double)rate / outRate;
                int f0 = Math.Min(frames - 1, (int)position);
                int f1 = Math.Min(frames - 1, f0 + 1);
                float t = (float)(position - f0);

                for (int c = 0; c < outChannels; c++)
                {
                    int source = Math.Min(c, channels - 1);
                    float a = samples[f0 * channels + source];
                    float b = samples[f1 * channels + source];
                    output[f * outChannels + c] = a + (b - a) * t;
                }
            }

            return output;
        }

        /// <summary>
        /// Leaves quiet audio alone and bends peaks smoothly under full scale, so mixes do not clip.
        /// </summary>
        private static float softLimit(float x)
        {
            const float knee = 0.9f;

            float magnitude = Math.Abs(x);
            if (magnitude <= knee)
                return x;

            float over = (magnitude - knee) / (1 - knee);
            return Math.Sign(x) * (knee + (1 - knee) * MathF.Tanh(over));
        }

        /// <summary>
        /// A 16-bit PCM WAV file. With no samples it is the 44-byte silent file mappers use for blank hitsounds.
        /// </summary>
        public static byte[] Wav(short[] pcm, int rate, int channels)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.ASCII);

            int dataBytes = pcm.Length * sizeof(short);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataBytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(rate);
            writer.Write(rate * channels * sizeof(short));
            writer.Write((short)(channels * sizeof(short)));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);

            foreach (short s in pcm)
                writer.Write(s);

            writer.Flush();
            return stream.ToArray();
        }
    }
}
