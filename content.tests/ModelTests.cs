using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Content.Models;
using Content.Schema;
using Xunit;

namespace Content.Tests
{
    public sealed class ModelTests : IDisposable
    {
        readonly string _pack;

        public ModelTests()
        {
            _pack = Path.Combine(Path.GetTempPath(), "pack-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_pack);
        }

        public void Dispose()
        {
            try { Directory.Delete(_pack, recursive: true); } catch { }
        }


        const uint Magic = 0x46546C67;
        const uint JsonChunk = 0x4E4F534A;
        const uint BinChunk = 0x004E4942;

        static string OneMesh(int vertices = 300, int triangles = 100, int nodes = 3,
                              string extra = "") => $@"{{
            ""asset"": {{ ""version"": ""2.0"" }},
            ""accessors"": [
                {{ ""count"": {vertices} }},
                {{ ""count"": {triangles * 3} }}
            ],
            ""meshes"": [ {{ ""primitives"": [
                {{ ""attributes"": {{ ""POSITION"": 0 }}, ""indices"": 1 }}
            ] }} ],
            ""nodes"": [ {string.Join(", ", Enumerable.Repeat(@"{ ""mesh"": 0 }", nodes))} ]
            {extra}
        }}";

        string Glb(string json, byte[] binary = null, string name = "skeleton.glb",
                   int? declared = null, int? jsonLength = null)
        {
            byte[] chunk = Encoding.UTF8.GetBytes(json);

            while (chunk.Length % 4 != 0) chunk = chunk.Concat(new byte[] { 0x20 }).ToArray();

            using var stream = new MemoryStream();
            using var write = new BinaryWriter(stream);

            int total = 12 + 8 + chunk.Length + (binary != null ? 8 + binary.Length : 0);

            write.Write(Magic);
            write.Write((uint)2);
            write.Write((uint)(declared ?? total));

            write.Write((uint)(jsonLength ?? chunk.Length));
            write.Write(JsonChunk);
            write.Write(chunk);

            if (binary != null)
            {
                write.Write((uint)binary.Length);
                write.Write(BinChunk);
                write.Write(binary);
            }

            write.Flush();

            return Put(name, stream.ToArray());
        }

        string Put(string name, byte[] bytes)
        {
            string path = Path.Combine(_pack, name);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);

            return name;
        }

        Read<ModelFacts> Inspect(string file) => ModelReader.Inspect(_pack, file);

        static string Said(Read<ModelFacts> read) =>
            string.Join("; ", read.Problems.Select(p => p.ToString()));

        static void Refused(Read<ModelFacts> read, string saying)
        {
            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.Contains(saying));
        }


        [Fact]
        public void AModelIsCountedOutOfItsOwnTablesRatherThanItsGeometry()
        {
            Read<ModelFacts> read = Inspect(Glb(OneMesh(vertices: 480, triangles: 160, nodes: 4)));

            Assert.True(read.Ok, Said(read));
            Assert.Equal(480, read.Value.Vertices);
            Assert.Equal(160, read.Value.Triangles);
            Assert.Equal(4, read.Value.Nodes);
            Assert.Equal(1, read.Value.Meshes);
        }

        [Fact]
        public void TheAnimationNamesComeBackSoAMisspeltClipCanBeOfferedTheRealOnes()
        {
            Read<ModelFacts> read = Inspect(Glb(OneMesh(extra: @",
                ""animations"": [ { ""name"": ""Idle"" }, { ""name"": ""Walk_A"" } ]")));

            Assert.True(read.Ok, Said(read));
            Assert.Equal(new[] { "Idle", "Walk_A" }, read.Value.Clips);
            Assert.True(read.Value.Has("Walk_A"));
            Assert.False(read.Value.Has("Walk"));
        }

        [Fact]
        public void AModelWithNoAnimationIsFine()
        {
            Read<ModelFacts> read = Inspect(Glb(OneMesh()));

            Assert.True(read.Ok, Said(read));
            Assert.False(read.Value.IsRigged);
        }

        [Fact]
        public void TheSameGltfAsTextIsReadTheSameWay()
        {
            string file = Put("skeleton.gltf", Encoding.UTF8.GetBytes(OneMesh()));

            Assert.True(Inspect(file).Ok);
        }


        [Fact]
        public void SomethingThatIsNotAGlbIsRefusedWhateverItIsCalled()
        {
            Refused(Inspect(Put("skeleton.glb", Encoding.UTF8.GetBytes("this is a text file"))),
                    "magic number");
        }

        [Fact]
        public void AFileTooShortToHaveAHeaderIsRefused()
        {
            Refused(Inspect(Put("skeleton.glb", new byte[] { 1, 2, 3 })), "too short");
        }

        [Fact]
        public void AFileThatSaysItIsLongerThanItIsIsCalledTruncated()
        {
            Refused(Inspect(Glb(OneMesh(), declared: 1_000_000)), "truncated");
        }

        [Fact]
        public void AChunkClaimingMoreBytesThanArePresentIsRefusedRatherThanSliced()
        {
            Refused(Inspect(Glb(OneMesh(), jsonLength: 900_000)), "with only");
        }

        [Fact]
        public void AGlbWithNoJsonInItHasNothingToRead()
        {
            using var stream = new MemoryStream();
            using var write = new BinaryWriter(stream);

            write.Write(Magic);
            write.Write((uint)2);
            write.Write((uint)12);
            write.Flush();

            Refused(Inspect(Put("empty.glb", stream.ToArray())), "no JSON chunk");
        }

        [Fact]
        public void AJsonChunkThatIsNotJsonIsRefusedWithALine()
        {
            Refused(Inspect(Glb("{ not json at all")), "not valid JSON");
        }

        [Fact]
        public void AVersionThisBuildDoesNotReadIsRefusedByNumber()
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(_pack, Glb(OneMesh())));

            bytes[4] = 3;

            Refused(Inspect(Put("v3.glb", bytes)), "version 3");
        }


        [Fact]
        public void AModelOverTheVertexCapIsRefusedByNameAndNotClampedSilently()
        {
            Refused(Inspect(Glb(OneMesh(vertices: ModelCaps.Vertices + 1, triangles: 1))),
                    "decimate it");
        }

        [Fact]
        public void AModelOverTheTriangleCapIsRefused()
        {
            Read<ModelFacts> read =
                Inspect(Glb(OneMesh(vertices: 10, triangles: ModelCaps.Triangles + 1)));

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.Where == "triangles");
        }

        [Fact]
        public void AModelOverTheNodeCapIsRefused()
        {
            Read<ModelFacts> read = Inspect(Glb(OneMesh(nodes: ModelCaps.Nodes + 1)));

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.Where == "nodes");
        }

        [Fact]
        public void AnExtensionThisFileIsNotIsRefusedBeforeAnythingElse()
        {
            Refused(ModelReader.Inspect(_pack, Put("skeleton.fbx", new byte[] { 1 })),
                    "not a model this game reads");
        }

        [Fact]
        public void AFileThatIsNotThereIsAProblemAndNotAnException()
        {
            Refused(Inspect("models/absent.glb"), "no such file");
        }


        [Theory]
        [InlineData("../../../etc/passwd")]
        [InlineData("/etc/shadow")]
        [InlineData("https://example.com/textures/skin.png")]
        [InlineData("file:///C:/windows/win.ini")]
        [InlineData("..%2f..%2fout.png")]
        public void AUriThatLeavesThePackIsRefusedWithTheStringItWrote(string uri)
        {
            Refused(Inspect(Glb(OneMesh(extra: $@",
                ""images"": [ {{ ""uri"": ""{uri}"" }} ]"))), "outside this pack");
        }

        [Fact]
        public void ABufferPointingOutOfThePackIsRefusedTheSameWay()
        {
            Refused(Inspect(Glb(OneMesh(extra: @",
                ""buffers"": [ { ""uri"": ""../../secrets.bin"" } ]"))), "outside this pack");
        }

        [Fact]
        public void AnEmbeddedDataUriIsNotAPathAndIsAllowed()
        {
            string png = Convert.ToBase64String(Png(64, 64));

            Assert.True(Inspect(Glb(OneMesh(extra: $@",
                ""images"": [ {{ ""uri"": ""data:image/png;base64,{png}"" }} ]"))).Ok);
        }

        [Fact]
        public void AnExtensionThisBuildHasNeverHeardOfIsRefusedByName()
        {
            Refused(Inspect(Glb(OneMesh(extra: @",
                ""extensionsRequired"": [ ""KHR_draco_mesh_compression"" ]"))),
                    "KHR_draco_mesh_compression");
        }

        [Fact]
        public void AnExtensionThisBuildDoesUnderstandIsAllowed()
        {
            Assert.True(Inspect(Glb(OneMesh(extra: @",
                ""extensionsRequired"": [ ""KHR_materials_unlit"" ]"))).Ok);
        }

        [Fact]
        public void AnOptionalExtensionIsIgnoredRatherThanRefused()
        {
            Assert.True(Inspect(Glb(OneMesh(extra: @",
                ""extensionsUsed"": [ ""EXT_something_nobody_has"" ]"))).Ok);
        }


        [Fact]
        public void ATextureOverTheResolutionCapIsRefusedWithItsSize()
        {
            byte[] png = Png(4096, 4096);

            Read<ModelFacts> read = Inspect(Glb(OneMesh(extra: @",
                ""bufferViews"": [ { ""buffer"": 0, ""byteOffset"": 0, ""byteLength"": 64 } ],
                ""images"": [ { ""name"": ""skin"", ""bufferView"": 0 } ]"), Padded(png)));

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.Contains("4096x4096"));
            Assert.Contains(read.Problems, p => p.What.Contains("wasted memory"));
        }

        [Fact]
        public void ATextureUnderTheCapIsMeasuredAndKept()
        {
            Read<ModelFacts> read = Inspect(Glb(OneMesh(extra: @",
                ""bufferViews"": [ { ""buffer"": 0, ""byteOffset"": 0, ""byteLength"": 64 } ],
                ""images"": [ { ""name"": ""skin"", ""bufferView"": 0 } ]"), Padded(Png(512, 512))));

            Assert.True(read.Ok, Said(read));
            Assert.Equal(512, read.Value.Textures.Single().Widest);
        }

        [Fact]
        public void SomethingInAnImageSlotThatIsNotAPictureIsSaidSo()
        {
            Read<ModelFacts> read = Inspect(Glb(OneMesh(extra: @",
                ""bufferViews"": [ { ""buffer"": 0, ""byteOffset"": 0, ""byteLength"": 64 } ],
                ""images"": [ { ""name"": ""skin"", ""bufferView"": 0 } ]"),
                Padded(Encoding.UTF8.GetBytes("not a picture, just bytes in a buffer view"))));

            Refused(read, "not a PNG or a JPEG");
        }

        [Fact]
        public void ATextureInAFileBesideTheModelIsMeasuredToo()
        {
            Put("textures/skin.png", Png(4096, 2048));

            Refused(Inspect(Glb(OneMesh(extra: @",
                ""images"": [ { ""uri"": ""textures/skin.png"" } ]"))), "4096x2048");
        }


        [Fact]
        public void APngsSizeIsReadOutOfItsIhdr()
        {
            Assert.True(ImageSize.TryRead(Png(1024, 512), out int width, out int height));
            Assert.Equal(1024, width);
            Assert.Equal(512, height);
        }

        // jpeg writes height before width, opposite to the others
        [Fact]
        public void AJpegsSizeIsReadOutOfItsFrameHeaderTheRightWayRound()
        {
            Assert.True(ImageSize.TryRead(Jpeg(800, 600), out int width, out int height));
            Assert.Equal(800, width);
            Assert.Equal(600, height);
        }

        [Fact]
        public void SomethingThatIsNeitherIsNotGuessedAt()
        {
            Assert.False(ImageSize.TryRead(Encoding.UTF8.GetBytes("GIF89a....."), out _, out _));
            Assert.False(ImageSize.TryRead(new byte[] { 0x89 }, out _, out _));
            Assert.False(ImageSize.TryRead(null, out _, out _));
        }


        static byte[] Png(int width, int height)
        {
            var bytes = new List<byte> { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A };

            bytes.AddRange(new byte[] { 0, 0, 0, 13 });
            bytes.AddRange(Encoding.ASCII.GetBytes("IHDR"));
            bytes.AddRange(Big(width));
            bytes.AddRange(Big(height));
            bytes.AddRange(new byte[] { 8, 6, 0, 0, 0 });

            return bytes.ToArray();
        }

        static byte[] Jpeg(int width, int height)
        {
            var bytes = new List<byte> { 0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x11, 0x08 };

            bytes.Add((byte)(height >> 8));
            bytes.Add((byte)(height & 0xFF));
            bytes.Add((byte)(width >> 8));
            bytes.Add((byte)(width & 0xFF));
            bytes.AddRange(new byte[] { 3, 1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1 });

            return bytes.ToArray();
        }

        static byte[] Padded(byte[] bytes, int to = 64)
        {
            Array.Resize(ref bytes, Math.Max(bytes.Length, to));

            return bytes;
        }

        static byte[] Big(int value) => new[]
        {
            (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value,
        };
    }
}
