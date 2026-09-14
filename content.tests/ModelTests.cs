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
    // A STRANGER'S MODEL FILE, READ WITHOUT TRUSTING IT (MINIS_AND_ART.md A2).
    //
    // This is the milestone the whole phase was ordered around - "the sharp edge last" - and it is
    // the one place in the project where a test is standing in for an attacker. Every case below
    // is either a file a well-meaning author will produce (a 4K atlas, a sculpt nobody decimated,
    // a `.gltf` whose textures sit beside it) or one nobody should: a chunk that claims to be
    // longer than the file, a uri that climbs out of the pack.
    //
    // THE FILES ARE BUILT IN THE TEST rather than shipped as fixtures, deliberately. A binary
    // fixture is a blob nobody can read in a diff, and every interesting case here is a handful of
    // bytes in a header - a truncated chunk is one integer. Building them means the test says what
    // it is testing.
    //
    // NOTHING HERE MAY THROW. `ModelReader` is the isolation boundary for art: a hostile file has
    // to come back as a `ContentProblem`, because the alternative is one bad subscribed folder
    // taking a fight down with it.
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
            try { Directory.Delete(_pack, recursive: true); } catch { /* a temp folder */ }
        }

        // ---- building a .glb ----

        const uint Magic = 0x46546C67;
        const uint JsonChunk = 0x4E4F534A;
        const uint BinChunk = 0x004E4942;

        // a glTF whose tables say it holds one triangle-soup mesh, which is the smallest honest
        // model there is
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

        // ---- what a good model reads back as ----

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

        // the clip names are what turns "no such clip" into a sentence an author can act on (A1)
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

        // "a model with no rig at all is a static piece that still slides and gets struck"
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

        // ---- the container, where every length is a stranger's claim ----

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

        // THE COMMONEST BROKEN FILE THERE IS, and the message says so: an interrupted copy
        [Fact]
        public void AFileThatSaysItIsLongerThanItIsIsCalledTruncated()
        {
            Refused(Inspect(Glb(OneMesh(), declared: 1_000_000)), "truncated");
        }

        // the oldest trick there is - a chunk header claiming more bytes than are present
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

        // ---- the caps ----

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

        // ---- out of the pack ----

        // "a mini loads from inside its own pack folder and nowhere else"
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

        // EMBEDDED IS FINE AND IS THE ONE THING THAT IS NOT A PATH - it is bytes inside a file the
        // size cap already bounds
        [Fact]
        public void AnEmbeddedDataUriIsNotAPathAndIsAllowed()
        {
            string png = Convert.ToBase64String(Png(64, 64));

            Assert.True(Inspect(Glb(OneMesh(extra: $@",
                ""images"": [ {{ ""uri"": ""data:image/png;base64,{png}"" }} ]"))).Ok);
        }

        // A FILE THAT SAYS IT CANNOT BE READ WITHOUT SOMETHING IS BELIEVED, because drawing it
        // anyway shows the model wrong rather than not at all
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

        // optional extensions are ignored, which is what the specification says a reader should do
        [Fact]
        public void AnOptionalExtensionIsIgnoredRatherThanRefused()
        {
            Assert.True(Inspect(Glb(OneMesh(extra: @",
                ""extensionsUsed"": [ ""EXT_something_nobody_has"" ]"))).Ok);
        }

        // ---- textures, measured rather than decoded ----

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

        // a texture beside the model, which is what an un-packed .gltf export looks like
        [Fact]
        public void ATextureInAFileBesideTheModelIsMeasuredToo()
        {
            Put("textures/skin.png", Png(4096, 2048));

            Refused(Inspect(Glb(OneMesh(extra: @",
                ""images"": [ { ""uri"": ""textures/skin.png"" } ]"))), "4096x2048");
        }

        // ---- and the header sniffing that makes all of that cheap ----

        [Fact]
        public void APngsSizeIsReadOutOfItsIhdr()
        {
            Assert.True(ImageSize.TryRead(Png(1024, 512), out int width, out int height));
            Assert.Equal(1024, width);
            Assert.Equal(512, height);
        }

        // a JPEG writes height before width, which is the wrong way round from everything else
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

        // ---- the smallest picture headers that are still true ----

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

        // A BUFFER VIEW IS A SLICE AND THE SLICE HAS TO BE THERE. `ModelReader` measures nothing
        // out of a view that runs past the end of the binary chunk, which is the same refusal-to-
        // slice-a-stranger's-claim the container check makes - so a header fixture is padded out
        // to the length its view declares
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
