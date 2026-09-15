using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Content.Audio;
using Content.Schema;
using Xunit;

namespace Content.Tests
{
    public sealed class AudioTests : IDisposable
    {
        readonly string _pack;

        public AudioTests()
        {
            _pack = Path.Combine(Path.GetTempPath(), "foley-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_pack);
        }

        public void Dispose()
        {
            try { Directory.Delete(_pack, recursive: true); } catch { }
        }


        enum Shape
        {
            Transient,

            Tone,

            Square,
        }

        static byte[] Wav(float amplitude = 0.5f, float seconds = 0.2f, int rate = 44100,
                          int depth = 16, int channels = 1, Shape shape = Shape.Transient)
        {
            int frames = (int)(rate * seconds);
            int frame = channels * depth / 8;

            var data = new List<byte>(frames * frame);

            for (int at = 0; at < frames; at++)
            {
                double phase = 2.0 * Math.PI * 220.0 * at / rate;
                double seconds_in = (double)at / rate;

                double value = shape switch
                {
                    // cosine not sine so the first sample is the peak
                    Shape.Transient => Math.Cos(phase) * Math.Exp(-30.0 * seconds_in),
                    Shape.Square => Math.Sign(Math.Sin(phase)),
                    _ => Math.Sin(phase),
                };

                value *= amplitude;

                for (int channel = 0; channel < channels; channel++)
                {
                    if (depth == 16)
                    {
                        short sample = (short)(value * 32767);

                        data.Add((byte)(sample & 0xFF));
                        data.Add((byte)((sample >> 8) & 0xFF));
                    }
                    else
                    {
                        data.Add((byte)(value * 127 + 128));
                    }
                }
            }

            return Riff(data.ToArray(), rate, depth, channels, format: 1);
        }

        static byte[] Riff(byte[] data, int rate, int depth, int channels, int format,
                           int? dataLength = null)
        {
            using var stream = new MemoryStream();
            using var write = new BinaryWriter(stream);

            int block = channels * depth / 8;

            write.Write(Encoding.ASCII.GetBytes("RIFF"));
            write.Write((uint)(36 + data.Length));
            write.Write(Encoding.ASCII.GetBytes("WAVE"));

            write.Write(Encoding.ASCII.GetBytes("fmt "));
            write.Write((uint)16);
            write.Write((ushort)format);
            write.Write((ushort)channels);
            write.Write((uint)rate);
            write.Write((uint)(rate * block));
            write.Write((ushort)block);
            write.Write((ushort)depth);

            write.Write(Encoding.ASCII.GetBytes("data"));
            write.Write((uint)(dataLength ?? data.Length));
            write.Write(data);

            write.Flush();

            return stream.ToArray();
        }

        static Read<WaveFacts> Parse(byte[] wav) => WaveReader.Parse(wav, "placed.wav");

        static WaveFacts Good(byte[] wav)
        {
            Read<WaveFacts> read = Parse(wav);

            Assert.True(read.Ok, string.Join("; ", read.Problems.Select(p => p.ToString())));

            return read.Value;
        }

        static void Refused(byte[] wav, string saying)
        {
            Read<WaveFacts> read = Parse(wav);

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.Contains(saying));
        }


        [Fact]
        public void AWavIsReadOutOfItsFormatChunk()
        {
            WaveFacts facts = Good(Wav(seconds: 0.25f, rate: 48000, channels: 2));

            Assert.Equal(48000, facts.Rate);
            Assert.Equal(2, facts.Channels);
            Assert.True(facts.IsStereo);
            Assert.Equal(16, facts.Depth);
            Assert.Equal(0.25f, facts.Seconds, 2);
        }

        [Fact]
        public void TheSampleDataComesBackSoNothingHasToOpenTheFileTwice()
        {
            WaveFacts facts = Good(Wav(seconds: 0.1f, rate: 8000));

            Assert.Equal(facts.Frames * 2, facts.Pcm.Length);
        }

        [Fact]
        public void AnEightBitWavIsReadAsUnsignedWhichIsTheFormatsOneSurprise()
        {
            WaveFacts facts = Good(Wav(amplitude: 0.5f, depth: 8));

            // 8-bit wav silence is 128, so half-scale meters near -6 db
            Assert.InRange(facts.Peak, -7f, -5f);
        }


        [Fact]
        public void APeakIsMeasuredInDbfsAgainstFullScale()
        {
            Assert.InRange(Good(Wav(amplitude: 0.5f)).Peak, -6.5f, -5.5f);
            Assert.InRange(Good(Wav(amplitude: 0.1f)).Peak, -20.5f, -19.5f);
        }

        // a sine's rms is its peak over root two, about 3 db down
        [Fact]
        public void TheAverageIsTheBodyOfTheSoundAndNotItsLoudestMoment()
        {
            WaveFacts facts = Good(Wav(amplitude: 0.1f, shape: Shape.Tone));

            Assert.InRange(facts.Rms - facts.Peak, -3.5f, -2.5f);
        }

        [Fact]
        public void ATransientAveragesFarBelowItsPeakAndAWallDoesNot()
        {
            Assert.True(Good(Wav(amplitude: 0.5f)).Rms < -15f);
            Assert.Contains(Parse(Wav(amplitude: 0.5f, shape: Shape.Square)).Problems,
                            p => p.What.Contains("brickwalled"));
        }

        [Fact]
        public void SilenceIsAFloorRatherThanAnInfinity()
        {
            WaveFacts facts = Good(Wav(amplitude: 0f));

            Assert.Equal(WaveFacts.Silent, facts.Peak);
            Assert.Equal(WaveFacts.Silent, facts.Rms);
        }


        [Fact]
        public void AClippingSampleIsRefusedWithTheNumberItMeasured()
        {
            Refused(Wav(amplitude: 1f), "dBFS");
        }

        [Fact]
        public void AWallOfNoiseIsCaughtByItsAverageAndToldWhereTheShippedPoolsSit()
        {
            Read<WaveFacts> read = Parse(Wav(amplitude: 0.9f, shape: Shape.Square));

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.Contains("-18"));
        }

        [Fact]
        public void AVeryQuietSampleIsNotRefusedBecauseQuietMayBeWhatWasMeant()
        {
            Assert.True(Parse(Wav(amplitude: 0.001f)).Ok);
        }

        [Fact]
        public void ASampleLongerThanFoleyEverIsIsRefused()
        {
            Refused(Wav(seconds: FoleyCaps.Seconds + 1f, rate: 8000), "seconds");
        }

        [Fact]
        public void ADepthTheGameCannotPlayWithoutConvertingIsRefused()
        {
            byte[] wav = Riff(new byte[400], rate: 44100, depth: 24, channels: 1, format: 1);

            Refused(wav, "8-bit, 16-bit");
        }

        [Fact]
        public void ACompressedWavIsRefusedBecauseDecodingOneIsTheThingTheCapsAvoid()
        {
            byte[] wav = Riff(new byte[400], rate: 44100, depth: 16, channels: 1, format: 85);

            Refused(wav, "uncompressed");
        }

        [Fact]
        public void SomethingThatIsNotAWavIsRefusedWhateverItIsCalled()
        {
            Refused(Encoding.UTF8.GetBytes("ID3 this is an mp3 really"), "not a wav");
        }

        [Fact]
        public void AHeaderWithNoSoundInItIsSaidSo()
        {
            byte[] wav = Riff(Array.Empty<byte>(), rate: 44100, depth: 16, channels: 1, format: 1);

            Refused(wav, "no samples");
        }

        [Fact]
        public void MoreThanTwoChannelsIsNotASample()
        {
            byte[] wav = Riff(new byte[800], rate: 44100, depth: 16, channels: 6, format: 1);

            Refused(wav, "mono or stereo");
        }

        [Fact]
        public void ADataChunkLongerThanTheFileIsReadAsFarAsItGoes()
        {
            byte[] full = Wav(seconds: 0.1f);
            byte[] lying = Riff(full.Skip(44).ToArray(), 44100, 16, 1, 1, dataLength: 900_000);

            Assert.True(Parse(lying).Ok);
        }


        string Folder(string named, params byte[][] samples)
        {
            string folder = Path.Combine(_pack, named);

            Directory.CreateDirectory(folder);

            for (int at = 0; at < samples.Length; at++)
                File.WriteAllBytes(Path.Combine(folder, $"placed_{at:00}.wav"), samples[at]);

            return folder;
        }

        [Fact]
        public void EveryWavInTheFolderIsTheSetAndNothingNamesAFile()
        {
            FoleySet set = FoleySet.Read(Folder("bones", Wav(), Wav(), Wav()), "audio/bones");

            Assert.Equal(3, set.Count);
            Assert.Empty(set.Problems);
        }

        // one bad sample drops just that sample, not the whole mini
        [Fact]
        public void OneClippingSampleDoesNotTakeTheRestOfTheSetWithIt()
        {
            FoleySet set = FoleySet.Read(Folder("bones", Wav(), Wav(amplitude: 1f), Wav()),
                                         "audio/bones");

            Assert.Equal(2, set.Count);
            Assert.Single(set.Problems);
        }

        [Fact]
        public void AProblemNamesTheFolderTheAuthorWroteAndNotThisMachinesPath()
        {
            FoleySet set = FoleySet.Read(Folder("bones", Wav(amplitude: 1f)), "audio/bones");

            Assert.StartsWith("audio/bones/", set.Problems.Single().File);
        }

        [Fact]
        public void AFolderThatIsNotThereIsAProblemAndNotAnException()
        {
            FoleySet set = FoleySet.Read(Path.Combine(_pack, "absent"), "audio/absent");

            Assert.True(set.IsEmpty);
            Assert.Contains(set.Problems, p => p.What.Contains("no such folder"));
        }

        [Fact]
        public void AnEmptyFolderIsAMiniSetDownInSilenceAndIsSaidSo()
        {
            FoleySet set = FoleySet.Read(Folder("bones"), "audio/bones");

            Assert.Contains(set.Problems, p => p.What.Contains("silence"));
        }
    }
}
