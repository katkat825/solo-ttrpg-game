using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Content.Schema;

namespace Content.Audio
{
    public static class WaveReader
    {
        public static Read<WaveFacts> Inspect(string folder, string file)
        {
            string path = Path.Combine(folder ?? "", (file ?? "").Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(path))
                return Bad(file, "there is no such file in this pack");

            long bytes;

            try
            {
                bytes = new FileInfo(path).Length;
            }
            catch (Exception could)
            {
                return Bad(file, "could not be read - " + could.Message);
            }

            // checked off the filesystem before the file is read into memory
            if (bytes > FoleyCaps.Bytes)
                return Bad(file,
                           $"this sample is {bytes / 1024} KB and one may be " +
                           $"{FoleyCaps.Bytes / 1024} KB - foley is a tenth of a second of " +
                           "something being set down, not a recording");

            byte[] raw;

            try
            {
                raw = File.ReadAllBytes(path);
            }
            catch (Exception could)
            {
                return Bad(file, "could not be read - " + could.Message);
            }

            return Parse(raw, file);
        }

        public static Read<WaveFacts> Parse(byte[] raw, string file)
        {
            if (raw == null || raw.Length < 12)
                return Bad(file, "is too short to be a wav - it has no header");

            if (Tag(raw, 0) != "RIFF" || Tag(raw, 8) != "WAVE")
                return Bad(file,
                           "is not a wav whatever it is called - it does not start with a RIFF " +
                           "header. Re-export it as a wav, which is the one format a pack's audio " +
                           "may be in");

            int channels = 0, rate = 0, depth = 0, format = 0;
            int data = -1, length = 0;

            int at = 12;

            while (at + 8 <= raw.Length)
            {
                string kind = Tag(raw, at);
                long size = Word(raw, at + 4);

                at += 8;

                if (size < 0 || size > raw.Length - at)
                {
                    // a truncated data chunk is clamped, not rejected; a cut-short copy is the common case
                    if (kind != "data")
                        return Bad(file, $"has a '{kind}' chunk claiming {size} bytes with only " +
                                         $"{raw.Length - at} left in the file");

                    size = raw.Length - at;
                }

                if (kind == "fmt ")
                {
                    if (size < 16) return Bad(file, "has a format chunk too short to read");

                    format = Short(raw, at);
                    channels = Short(raw, at + 2);
                    rate = (int)Word(raw, at + 4);
                    depth = Short(raw, at + 14);
                }
                else if (kind == "data" && data < 0)
                {
                    data = at;
                    length = (int)size;
                }

                at += (int)size;

                // riff chunks are word-aligned; an odd-length one is followed by a pad byte
                if (size % 2 != 0) at++;
            }

            if (channels == 0 || rate == 0)
                return Bad(file, "has no format chunk, so nothing knows what is in it");

            if (data < 0)
                return Bad(file, "has no data chunk, so it is a header with no sound in it");

            // 1 is PCM; anything else would mean decoding a stranger's bytes
            if (format != 1)
                return Bad(file,
                           $"is a wav but not an uncompressed one (format {format}) - export it " +
                           "as PCM, which is what \"16-bit wav\" means in every audio tool");

            if (Array.IndexOf(FoleyCaps.Depths, depth) < 0)
                return Bad(file,
                           $"is {depth}-bit and a sample may be " +
                           $"{Vocabulary.Offer(Words(FoleyCaps.Depths))} - that is what the game " +
                           "can play without converting it, and a conversion nobody asked for is " +
                           "a difference between what you heard and what a player hears");

            if (channels < 1 || channels > 2)
                return Bad(file, $"has {channels} channels and a sample is mono or stereo");

            int frame = channels * depth / 8;
            int frames = frame > 0 ? length / frame : 0;

            if (frames == 0)
                return Bad(file, "has no samples in it");

            var facts = Meter(raw, data, length, depth, channels, rate, frames, file);

            var problems = new List<ContentProblem>();

            if (facts.Seconds > FoleyCaps.Seconds)
                problems.Add(Problem(
                    file, $"is {facts.Seconds:0.0} seconds and a foley sample may be " +
                          $"{FoleyCaps.Seconds:0} - the pool picks one per event, so a long " +
                          "sample is a sound that is still playing when the next thing happens"));

            if (facts.Peak > FoleyCaps.Peak)
                problems.Add(Problem(
                    file, $"peaks at {facts.Peak:0.0} dBFS and a sample may reach " +
                          $"{FoleyCaps.Peak:0.0} - it is clipping, or mastered to be the loudest " +
                          "thing in somebody else's game"));

            if (facts.Rms > FoleyCaps.Rms)
                problems.Add(Problem(
                    file, $"averages {facts.Rms:0.0} dBFS and a sample may average " +
                          $"{FoleyCaps.Rms:0.0} - this is a brickwalled sample, and it will be " +
                          "the loudest thing in the room every time the piece is set down. The " +
                          "shipped pools sit near -18"));

            if (problems.Count > 0) return Read<WaveFacts>.Bad(problems);

            return Read<WaveFacts>.Good(facts);
        }


        static WaveFacts Meter(byte[] raw, int data, int length, int depth, int channels, int rate,
                               int frames, string file)
        {
            double loudest = 0;
            double sum = 0;
            int counted = 0;

            if (depth == 16)
            {
                for (int at = data; at + 1 < data + length; at += 2)
                {
                    double sample = (short)(raw[at] | (raw[at + 1] << 8)) / 32768.0;

                    loudest = Math.Max(loudest, Math.Abs(sample));
                    sum += sample * sample;
                    counted++;
                }
            }
            else
            {
                // 8-bit wav is unsigned, with 128 as silence
                for (int at = data; at < data + length; at++)
                {
                    double sample = (raw[at] - 128) / 128.0;

                    loudest = Math.Max(loudest, Math.Abs(sample));
                    sum += sample * sample;
                    counted++;
                }
            }

            var pcm = new byte[length];

            Array.Copy(raw, data, pcm, 0, length);

            return new WaveFacts(file, channels, rate, depth, frames,
                                 Decibels(loudest),
                                 Decibels(counted > 0 ? Math.Sqrt(sum / counted) : 0),
                                 pcm);
        }

        static float Decibels(double amplitude) =>
            amplitude <= 0 ? WaveFacts.Silent
                           : (float)Math.Max(WaveFacts.Silent, 20.0 * Math.Log10(amplitude));


        static string Tag(byte[] raw, int at) =>
            at + 4 <= raw.Length ? Encoding.ASCII.GetString(raw, at, 4) : "";

        static long Word(byte[] raw, int at) =>
            at + 4 <= raw.Length
                ? (long)(uint)(raw[at] | (raw[at + 1] << 8) | (raw[at + 2] << 16) | (raw[at + 3] << 24))
                : -1;

        static int Short(byte[] raw, int at) =>
            at + 2 <= raw.Length ? raw[at] | (raw[at + 1] << 8) : 0;

        static string[] Words(int[] depths)
        {
            var words = new string[depths.Length];

            for (int i = 0; i < depths.Length; i++) words[i] = depths[i] + "-bit";

            return words;
        }

        static ContentProblem Problem(string file, string what) =>
            new ContentProblem(file, "", what);

        static Read<WaveFacts> Bad(string file, string what) =>
            Read<WaveFacts>.Bad(Problem(file, what));
    }
}
