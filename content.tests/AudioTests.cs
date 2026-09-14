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
    // A SUPPLIED SAMPLE, PARSED AND METERED (MINIS_AND_ART.md A3).
    //
    // `THE_TABLE.md` section 7 ranks audio as half the game, and the cap that matters most here is
    // the one a pack author cannot check for themselves: loudness. A supplied set mixes into the
    // same pool as the dice, at gains `MiniVoice` chose against samples maximised to about -18 dB
    // RMS, so a brickwalled sample is not "a bit loud" - it is the loudest thing in the room every
    // time a piece is set down, and the player's only recourse is to turn the whole game down.
    //
    // THE SAMPLES ARE SYNTHESISED rather than shipped, for the same reason `ModelTests` builds its
    // own glTFs: a wav fixture is a blob nobody can read in a diff, and a decaying tone at a known
    // amplitude is a test that says what it is testing. A full-scale square wave IS a brickwalled
    // sample - that is not an approximation of one - and a struck-and-decaying one IS foley.
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
            try { Directory.Delete(_pack, recursive: true); } catch { /* a temp folder */ }
        }

        // ---- building a wav ----

        // WHAT A SAMPLE IS SHAPED LIKE, which matters more than its peak. Foley is a transient -
        // a thing struck, and then air - and a sample's AVERAGE is what tells the three apart:
        // the same peak reads as a set-down, a held tone, or a wall of noise depending only on
        // what is underneath it. `FoleyCaps` caps the average for exactly that reason, so the
        // fixtures here have to be honest about shape or the caps are being tested against
        // something no pack would ever ship
        enum Shape
        {
            // struck and decaying - what every sample in `samples/minis/` and `impacts/wood/` is
            Transient,

            // held at one level, which is an instrument rather than foley
            Tone,

            // full-scale and square: a maximised sample, with no dynamics left under the ceiling
            Square,
        }

        // <paramref name="amplitude"/> is 0..1 of full scale, and is the sample's PEAK
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
                    // cosine rather than sine so the very first sample IS the peak, which makes
                    // "amplitude" mean what the parameter says it means
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

        // ---- what a good sample reads back as ----

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

        // THE PCM COMES BACK WITH IT, because `Game.Audio.PackVoice` builds an `AudioStreamWav`
        // out of exactly these fields - one parse, in the place where it is testable
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

            // silence is 128 in an 8-bit wav, so a half-scale sample has to meter near -6 dB
            // rather than near full scale, which is what reading it as signed would produce
            Assert.InRange(facts.Peak, -7f, -5f);
        }

        // ---- the meter ----

        [Fact]
        public void APeakIsMeasuredInDbfsAgainstFullScale()
        {
            Assert.InRange(Good(Wav(amplitude: 0.5f)).Peak, -6.5f, -5.5f);
            Assert.InRange(Good(Wav(amplitude: 0.1f)).Peak, -20.5f, -19.5f);
        }

        // a sine's RMS is its peak over root two - about 3 dB down - which is the arithmetic this
        // pins, because a meter that is wrong is worse than no meter
        // A HELD TONE'S AVERAGE IS ITS PEAK OVER ROOT TWO - about 3 dB down - which is the
        // arithmetic this pins, because a meter that is wrong is worse than no meter. Quiet enough
        // to pass the caps, because what is being tested is the meter and not the ceiling
        [Fact]
        public void TheAverageIsTheBodyOfTheSoundAndNotItsLoudestMoment()
        {
            WaveFacts facts = Good(Wav(amplitude: 0.1f, shape: Shape.Tone));

            Assert.InRange(facts.Rms - facts.Peak, -3.5f, -2.5f);
        }

        // AND A TRANSIENT'S IS FAR BELOW IT, which is the whole reason both numbers are measured:
        // a set-down and a wall of noise can peak at exactly the same place
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

        // ---- and the caps ----

        [Fact]
        public void AClippingSampleIsRefusedWithTheNumberItMeasured()
        {
            Refused(Wav(amplitude: 1f), "dBFS");
        }

        // A BRICKWALLED SAMPLE IS EXACTLY WHAT A MAXIMISER PRODUCES - a wall at the ceiling with
        // no dynamics under it - so the average is what catches it, not the peak
        [Fact]
        public void AWallOfNoiseIsCaughtByItsAverageAndToldWhereTheShippedPoolsSit()
        {
            Read<WaveFacts> read = Parse(Wav(amplitude: 0.9f, shape: Shape.Square));

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.Contains("-18"));
        }

        // THE ASYMMETRY IS ON PURPOSE: quiet is a choice an author may have meant, and loud is an
        // imposition on everybody else's mix
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

        // a truncated wav is the commonest broken audio file there is, and the reader reads what
        // arrived rather than what the header promised
        [Fact]
        public void ADataChunkLongerThanTheFileIsReadAsFarAsItGoes()
        {
            byte[] full = Wav(seconds: 0.1f);
            byte[] lying = Riff(full.Skip(44).ToArray(), 44100, 16, 1, 1, dataLength: 900_000);

            Assert.True(Parse(lying).Ok);
        }

        // ---- the folder, which is the list ----

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

        // ONE BAD SAMPLE IS A SET MINUS ONE SAMPLE, not a silent mini - the same isolation the
        // rest of the phase uses, one level finer
        [Fact]
        public void OneClippingSampleDoesNotTakeTheRestOfTheSetWithIt()
        {
            FoleySet set = FoleySet.Read(Folder("bones", Wav(), Wav(amplitude: 1f), Wav()),
                                         "audio/bones");

            Assert.Equal(2, set.Count);
            Assert.Single(set.Problems);
        }

        // the author sees the folder they wrote, not this machine's absolute path
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

        // an empty folder and a missing one are different mistakes and get different sentences
        [Fact]
        public void AnEmptyFolderIsAMiniSetDownInSilenceAndIsSaidSo()
        {
            FoleySet set = FoleySet.Read(Folder("bones"), "audio/bones");

            Assert.Contains(set.Problems, p => p.What.Contains("silence"));
        }
    }
}
