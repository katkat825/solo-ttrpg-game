using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Content.Schema;

namespace Content.Models
{
    // A STRANGER'S MODEL FILE, READ WITHOUT TRUSTING IT (MINIS_AND_ART.md A2).
    //
    // This is the sharp edge of the whole phase and the reason A0 and A1 were built first: a mini
    // is "the one kind of content that is a BINARY FILE THE ENGINE PARSES AT RUNTIME, and
    // untrusted binary parsing is a security surface in a way a JSON statblock is not".
    //
    // WHAT THIS IS NOT: a loader. Nothing here decodes geometry, decompresses a texture or builds
    // a node. It reads the glTF's own tables - which are JSON - counts what the file CLAIMS to
    // contain, and decides whether the game is willing to hand the file to an importer at all. The
    // importing is Godot's `GltfDocument`, and it happens only after every answer below is yes.
    // That order is the entire design: refusing a hostile file must not require parsing it first.
    //
    // THE FOUR THINGS IT REFUSES, all of them by name and none of them silently clamped:
    //
    //   OVER A CAP        vertices, triangles, nodes, animations, images, texture size, file size
    //                     (`ModelCaps`, and every one of them read out of a header)
    //   OUT OF THE PACK   any `uri` that is absolute, rooted, on a drive, on a scheme, or climbs
    //                     with `..`. A mini loads from inside its own pack folder and nowhere else
    //   NOT UNDERSTOOD    an `extensionsRequired` this build has never heard of. A file that says
    //                     "you cannot read me correctly without X" is telling the truth, and
    //                     rendering it anyway is how a pack looks broken for reasons nobody can find
    //   NOT A MODEL       a bad magic number, a truncated chunk, a JSON chunk that is not JSON
    //
    // AND WHAT IT DOES *NOT* NEED TO REFUSE, because the format does not have it: a script. glTF
    // is geometry, materials, animation and metadata; there is no expression language and no hook
    // by which a model could run. "Content is pure data only, never code" is not a rule this
    // reader enforces so much as a reason this FORMAT and no other was chosen (`MODDING.md` 5).
    //
    // IT NEVER THROWS. Every failure is a `ContentProblem`, because a model that will not parse
    // has to fail to ONE mini's placeholder box, named, with the rest of its pack still loading.
    public static class ModelReader
    {
        // glTF's own magic numbers, little-endian, out of the specification
        const uint Magic = 0x46546C67;   // "glTF"

        const uint JsonChunk = 0x4E4F534A;

        const uint BinChunk = 0x004E4942;

        // THE EXTENSIONS THIS BUILD IS WILLING TO BE TOLD IT NEEDS, and the list is short on
        // purpose. An `extensionsRequired` entry is the file saying it cannot be read correctly
        // without something; agreeing to that for an extension nobody here has implemented would
        // produce a model that renders WRONG rather than one that is refused, which is the harder
        // of the two to diagnose from a bug report. Optional `extensions` are ignored, which is
        // what the specification says to do with them
        static readonly string[] Understood =
        {
            "KHR_materials_unlit",
            "KHR_texture_transform",
            "KHR_mesh_quantization",
            "KHR_materials_emissive_strength",
        };

        // `Inspect` RATHER THAN `Read`, and the word is the contract: every other reader in
        // `content/` turns a file into the thing it describes, and this one deliberately does not.
        // It looks at a model and reports what is in it. The thing itself is built by Godot,
        // afterwards, only if this said yes
        //
        // <paramref name="folder"/> is the pack the model has to stay inside; <paramref name="file"/>
        // is its path relative to that folder, and is what an author gets told about
        public static Read<ModelFacts> Inspect(string folder, string file)
        {
            var problems = new List<ContentProblem>();

            string extension = Path.GetExtension(file ?? "").ToLowerInvariant();

            if (Array.IndexOf(ModelCaps.Extensions, extension) < 0)
                return Bad(file, "",
                           $"'{extension}' is not a model this game reads - a mini is " +
                           $"{Vocabulary.Offer(ModelCaps.Extensions)}, which is one open format " +
                           "every 3D tool can export");

            string path = Path.Combine(folder ?? "", (file ?? "").Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(path))
                return Bad(file, "", "there is no such file in this pack");

            long bytes;

            try
            {
                bytes = new FileInfo(path).Length;
            }
            catch (Exception could)
            {
                return Bad(file, "", "could not be read - " + could.Message);
            }

            // THE FIRST CAP, AND IT IS CHECKED BEFORE A SINGLE BYTE IS OPENED. Everything below
            // reads the file into memory, so the size ceiling has to come from the filesystem
            // rather than from the read
            if (bytes > ModelCaps.Bytes)
                return Bad(file, "",
                           $"this file is {bytes / 1024 / 1024} MB and a mini may be " +
                           $"{ModelCaps.Bytes / 1024 / 1024} MB - a figure 20 mm tall on a table " +
                           "does not need more, and the cap is what keeps one pack from being a " +
                           "download nobody finishes");

            byte[] raw;

            try
            {
                raw = File.ReadAllBytes(path);
            }
            catch (Exception could)
            {
                return Bad(file, "", "could not be read - " + could.Message);
            }

            string json;
            byte[] binary = Array.Empty<byte>();

            if (extension == ".glb")
            {
                if (!Container(raw, file, out json, out binary, out string wrong))
                    return Bad(file, "", wrong);
            }
            else
            {
                try
                {
                    json = Encoding.UTF8.GetString(raw);
                }
                catch (Exception could)
                {
                    return Bad(file, "", "is not text where a .gltf has to be - " + could.Message);
                }
            }

            return Tables(json, binary, folder, file, bytes, problems);
        }

        // ---- the GLB container: a 12-byte header and a run of chunks ----

        // EVERY LENGTH IN HERE IS A STRANGER'S. A chunk that claims to be four gigabytes long, or
        // to start past the end of the file, is the oldest trick there is, so each one is checked
        // against what is actually present before it is used to slice anything
        static bool Container(byte[] raw, string file, out string json, out byte[] binary,
                              out string wrong)
        {
            json = "";
            binary = Array.Empty<byte>();
            wrong = "";

            if (raw.Length < 12)
            {
                wrong = "is too short to be a .glb - it has no header";
                return false;
            }

            if (Word(raw, 0) != Magic)
            {
                wrong = "does not begin with glTF's magic number, so it is not a .glb whatever it " +
                        "is called - re-export it, or rename it to what it really is";
                return false;
            }

            uint version = Word(raw, 4);

            if (version != 2)
            {
                wrong = $"is glTF version {version} and this game reads version 2 - every current " +
                        "exporter writes 2";
                return false;
            }

            // the header's own length field, held against the file that actually arrived. A
            // mismatch is a truncated download far more often than it is anything else, and
            // saying so is more use than "unexpected end of file"
            uint declared = Word(raw, 8);

            if (declared > raw.Length)
            {
                wrong = $"says it is {declared} bytes and only {raw.Length} arrived - the file is " +
                        "truncated, which usually means an interrupted copy";
                return false;
            }

            int at = 12;
            bool sawJson = false;

            while (at + 8 <= raw.Length)
            {
                uint length = Word(raw, at);
                uint kind = Word(raw, at + 4);

                at += 8;

                if (length > raw.Length - at)
                {
                    wrong = $"has a chunk claiming to be {length} bytes with only {raw.Length - at} " +
                            "left in the file";
                    return false;
                }

                if (kind == JsonChunk && !sawJson)
                {
                    try
                    {
                        json = Encoding.UTF8.GetString(raw, at, (int)length);
                    }
                    catch (Exception could)
                    {
                        wrong = "has a JSON chunk that is not text - " + could.Message;
                        return false;
                    }

                    sawJson = true;
                }
                else if (kind == BinChunk && binary.Length == 0)
                {
                    binary = new byte[length];
                    Array.Copy(raw, at, binary, 0, (int)length);
                }

                // anything else is a chunk type this build does not know, and the specification
                // says to skip those rather than refuse them

                at += (int)length;

                // chunks are four-byte aligned, and a writer that padded is not an error
                while (at % 4 != 0 && at < raw.Length) at++;
            }

            if (!sawJson)
            {
                wrong = "has no JSON chunk in it, so there is nothing in it to read";
                return false;
            }

            return true;
        }

        static uint Word(byte[] raw, int at) =>
            (uint)(raw[at] | (raw[at + 1] << 8) | (raw[at + 2] << 16) | (raw[at + 3] << 24));

        // ---- the tables, which is where every cap is actually checked ----

        static Read<ModelFacts> Tables(string json, byte[] binary, string folder, string file,
                                       long bytes, List<ContentProblem> problems)
        {
            JsonDocument document;

            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException bad)
            {
                return Bad(file, "", "the glTF inside this file is not valid JSON - " + bad.Message,
                           (int)(bad.LineNumber ?? 0) + 1);
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return Bad(file, "", "a glTF is a JSON object and this is not");

                Required(root, file, problems);
                Uris(root, file, problems);

                int[] counts = Accessors(root);

                Meshes(root, counts, out int vertices, out int triangles, out int meshes);

                int nodes = Length(root, "nodes");
                IReadOnlyList<string> clips = Clips(root, file, problems);

                Over(vertices, ModelCaps.Vertices, "vertices", file, problems);
                Over(triangles, ModelCaps.Triangles, "triangles", file, problems);
                Over(nodes, ModelCaps.Nodes, "nodes", file, problems);

                IReadOnlyList<TextureFact> textures =
                    Textures(root, binary, folder, file, problems);

                if (problems.Count > 0) return Read<ModelFacts>.Bad(problems);

                return Read<ModelFacts>.Good(new ModelFacts(
                    file, bytes, vertices, triangles, nodes, meshes, clips, textures));
            }
        }

        // A FILE THAT SAYS IT NEEDS SOMETHING WE HAVE NOT GOT IS BELIEVED. Optional `extensions`
        // are ignored - which is exactly what the specification says a reader should do with them
        static void Required(JsonElement root, string file, List<ContentProblem> problems)
        {
            if (!root.TryGetProperty("extensionsRequired", out JsonElement list) ||
                list.ValueKind != JsonValueKind.Array)
                return;

            foreach (JsonElement entry in list.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.String) continue;

                string extension = entry.GetString() ?? "";

                if (Array.IndexOf(Understood, extension) >= 0) continue;

                problems.Add(new ContentProblem(
                    file, "extensionsRequired",
                    $"this model says it cannot be read without '{extension}', and this build " +
                    $"knows {Vocabulary.Offer(Understood)}. Re-export without it - drawing it " +
                    "anyway would show the model wrong rather than not at all"));
            }
        }

        // EVERY `uri` IN THE FILE, WHEREVER IT IS. Buffers and images are the two tables that
        // carry one, and a mini loads from inside its own pack folder and nowhere else - so an
        // absolute path, a drive letter, a `..`, or an `http://` is refused with the string it
        // wrote, rather than resolved and then found to be outside
        static void Uris(JsonElement root, string file, List<ContentProblem> problems)
        {
            foreach (string table in new[] { "buffers", "images" })
            {
                if (!root.TryGetProperty(table, out JsonElement list) ||
                    list.ValueKind != JsonValueKind.Array)
                    continue;

                int at = 0;

                foreach (JsonElement entry in list.EnumerateArray())
                {
                    string where = $"{table}[{at++}].uri";

                    if (entry.ValueKind != JsonValueKind.Object) continue;

                    if (!entry.TryGetProperty("uri", out JsonElement uri) ||
                        uri.ValueKind != JsonValueKind.String)
                        continue;

                    string said = uri.GetString() ?? "";

                    // EMBEDDED IS FINE AND IS THE ONE THING THAT IS NOT A PATH. A `data:` uri is
                    // bytes inside the file that is already being read, and it is bounded by the
                    // file-size cap like everything else in it
                    if (said.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;

                    if (Minis.MiniReader.Inside(Unescaped(said))) continue;

                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{said}' is outside this pack - a model may only name files beside it, " +
                        "with forward slashes and no '..'. Export it as a .glb and the textures " +
                        "travel inside the file"));
                }
            }
        }

        // a uri is percent-escaped, and a `..` written as `%2e%2e` is still a `..`
        static string Unescaped(string uri)
        {
            try
            {
                return Uri.UnescapeDataString(uri);
            }
            catch (Exception)
            {
                // an escape sequence that will not unescape is not a path either
                return uri;
            }
        }

        // ---- counting what it claims to hold ----

        // `accessors[i].count` for every accessor, which is how many of whatever-it-is there are.
        // Read once into an array because a mesh refers to them by index, repeatedly
        static int[] Accessors(JsonElement root)
        {
            if (!root.TryGetProperty("accessors", out JsonElement list) ||
                list.ValueKind != JsonValueKind.Array)
                return Array.Empty<int>();

            var counts = new List<int>();

            foreach (JsonElement entry in list.EnumerateArray())
                counts.Add(entry.ValueKind == JsonValueKind.Object &&
                           entry.TryGetProperty("count", out JsonElement count) &&
                           count.ValueKind == JsonValueKind.Number &&
                           count.TryGetInt32(out int many) && many > 0
                    ? many
                    : 0);

            return counts.ToArray();
        }

        // VERTICES ARE THE POSITION ACCESSOR AND TRIANGLES ARE THE INDEX ACCESSOR OVER THREE,
        // which is the count as the file itself declares it rather than as a decoder would find
        // it. A primitive with no indices is drawn straight out of its vertices, so it is those
        // over three instead
        static void Meshes(JsonElement root, int[] accessors, out int vertices, out int triangles,
                           out int meshes)
        {
            vertices = 0;
            triangles = 0;
            meshes = 0;

            if (!root.TryGetProperty("meshes", out JsonElement list) ||
                list.ValueKind != JsonValueKind.Array)
                return;

            foreach (JsonElement mesh in list.EnumerateArray())
            {
                meshes++;

                if (mesh.ValueKind != JsonValueKind.Object ||
                    !mesh.TryGetProperty("primitives", out JsonElement primitives) ||
                    primitives.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (JsonElement primitive in primitives.EnumerateArray())
                {
                    if (primitive.ValueKind != JsonValueKind.Object) continue;

                    int positions = 0;

                    if (primitive.TryGetProperty("attributes", out JsonElement attributes) &&
                        attributes.ValueKind == JsonValueKind.Object &&
                        attributes.TryGetProperty("POSITION", out JsonElement position))
                        positions = At(accessors, position);

                    vertices += positions;

                    int indices = primitive.TryGetProperty("indices", out JsonElement index)
                        ? At(accessors, index)
                        : positions;

                    triangles += indices / 3;
                }
            }
        }

        static int At(int[] accessors, JsonElement index) =>
            index.ValueKind == JsonValueKind.Number && index.TryGetInt32(out int i) &&
            i >= 0 && i < accessors.Length
                ? accessors[i]
                : 0;

        static int Length(JsonElement root, string table) =>
            root.TryGetProperty(table, out JsonElement list) && list.ValueKind == JsonValueKind.Array
                ? list.GetArrayLength()
                : 0;

        // THE NAMES A CLIP MAP CAN POINT AT. An animation with no name is legal glTF and useless
        // to a manifest, so it is reported as the index it is - which is at least findable
        static IReadOnlyList<string> Clips(JsonElement root, string file,
                                           List<ContentProblem> problems)
        {
            var clips = new List<string>();

            if (!root.TryGetProperty("animations", out JsonElement list) ||
                list.ValueKind != JsonValueKind.Array)
                return clips;

            Over(list.GetArrayLength(), ModelCaps.Animations, "animations", file, problems);

            int at = 0;

            foreach (JsonElement entry in list.EnumerateArray())
            {
                string name = entry.ValueKind == JsonValueKind.Object &&
                              entry.TryGetProperty("name", out JsonElement said) &&
                              said.ValueKind == JsonValueKind.String
                    ? said.GetString()
                    : "";

                clips.Add(string.IsNullOrEmpty(name) ? $"animation {at}" : name);
                at++;
            }

            return clips;
        }

        // ---- and how big its pictures are ----

        static IReadOnlyList<TextureFact> Textures(JsonElement root, byte[] binary, string folder,
                                                   string file, List<ContentProblem> problems)
        {
            var textures = new List<TextureFact>();

            if (!root.TryGetProperty("images", out JsonElement list) ||
                list.ValueKind != JsonValueKind.Array)
                return textures;

            Over(list.GetArrayLength(), ModelCaps.Images, "images", file, problems);

            int[] views = Views(root);
            int at = 0;

            foreach (JsonElement image in list.EnumerateArray())
            {
                string name = Name(image, at++);

                byte[] head = Head(image, views, binary, folder);

                // NOT BEING ABLE TO MEASURE ONE IS NOT A REFUSAL. An image in an external file
                // this reader chose not to open, or in a buffer it could not slice, is one it
                // cannot hold to the cap - and refusing every model whose textures are arranged
                // unusually would refuse most of the honest ones. The cap bites what it can see
                if (head == null) continue;

                if (!ImageSize.TryRead(head, out int width, out int height))
                {
                    // A PICTURE THAT IS ONE OF THE TWO FORMATS BUT WHOSE SIZE IS PAST THE HEADER
                    // this reader checks - a JPEG behind a large colour profile or thumbnail - is a
                    // different problem from a file that is not a picture at all, and saying so is
                    // the difference between "re-export it" and "you gave me the wrong file"
                    problems.Add(new ContentProblem(
                        file, $"images[{at - 1}]",
                        ImageSize.Recognised(head)
                            ? $"'{name}' is a PNG or JPEG whose dimensions sit past the header this " +
                              "reads - re-export it with less embedded metadata (a large colour " +
                              "profile or thumbnail), or as a plain PNG, so its size can be read " +
                              "and held to the cap"
                            : $"'{name}' is not a PNG or a JPEG - those are the two kinds of " +
                              "picture a glTF may carry"));
                    continue;
                }

                var fact = new TextureFact(name, width, height);

                textures.Add(fact);

                if (fact.Widest > ModelCaps.Texture)
                    problems.Add(new ContentProblem(
                        file, $"images[{at - 1}]",
                        $"'{name}' is {width}x{height} and a mini's texture may be " +
                        $"{ModelCaps.Texture} across - a figure this size on a table cannot show " +
                        "more, so the extra is wasted memory rather than detail"));
            }

            return textures;
        }

        static string Name(JsonElement image, int at)
        {
            if (image.ValueKind == JsonValueKind.Object)
            {
                if (image.TryGetProperty("name", out JsonElement said) &&
                    said.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(said.GetString()))
                    return said.GetString();

                if (image.TryGetProperty("uri", out JsonElement uri) &&
                    uri.ValueKind == JsonValueKind.String &&
                    !uri.GetString().StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    return uri.GetString();
            }

            return $"image {at}";
        }

        // ONLY AS MANY BYTES AS A HEADER NEEDS, WHICH IS THE WHOLE POINT. Thirty-two is past the
        // end of a PNG's IHDR; a JPEG's frame header can be further in, so it gets a little more.
        // Never the whole image - measuring a texture must not mean loading one
        const int Header = 1024;

        static byte[] Head(JsonElement image, int[] views, byte[] binary, string folder)
        {
            if (image.ValueKind != JsonValueKind.Object) return null;

            // embedded in the GLB's binary chunk, which is where a self-contained export puts it
            if (image.TryGetProperty("bufferView", out JsonElement view) &&
                view.ValueKind == JsonValueKind.Number && view.TryGetInt32(out int which) &&
                which >= 0 && which * 2 + 1 < views.Length)
            {
                int offset = views[which * 2];
                int length = views[which * 2 + 1];

                if (offset < 0 || length <= 0 || offset + length > binary.Length) return null;

                var head = new byte[Math.Min(Header, length)];

                Array.Copy(binary, offset, head, 0, head.Length);

                return head;
            }

            if (!image.TryGetProperty("uri", out JsonElement uri) ||
                uri.ValueKind != JsonValueKind.String)
                return null;

            string said = Unescaped(uri.GetString() ?? "");

            // a data: uri is base64 in the file that has already been read and capped; decoding
            // its first kilobyte is cheap and tells the truth about the picture inside it
            if (said.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return Embedded(said);

            // AND THE ONLY FILE THIS READER OPENS BESIDES THE MODEL. `Uris` has already refused
            // anything that could leave the pack, so by here the path is relative and clean - and
            // it is combined against the pack folder rather than against anything the file said
            if (!Minis.MiniReader.Inside(said)) return null;

            string path = Path.Combine(folder ?? "", said.Replace('/', Path.DirectorySeparatorChar));

            try
            {
                if (!File.Exists(path)) return null;

                using FileStream stream = File.OpenRead(path);

                var head = new byte[Header];
                int read = stream.Read(head, 0, head.Length);

                if (read <= 0) return null;

                Array.Resize(ref head, read);

                return head;
            }
            catch (Exception)
            {
                // unreadable is un-measurable, and the cap bites what it can see
                return null;
            }
        }

        static byte[] Embedded(string uri)
        {
            int comma = uri.IndexOf(',');

            if (comma < 0 || !uri.Substring(0, comma).EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
                return null;

            // a header's worth, rounded up to a base64 quantum, rather than the whole picture
            string head = uri.Substring(comma + 1);

            if (head.Length > Header * 2) head = head.Substring(0, Header / 3 * 4);

            try
            {
                return Convert.FromBase64String(head);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        // every bufferView's offset and length, flattened into pairs so a lookup is arithmetic.
        // Views into anything but buffer 0 are given a negative offset, because only buffer 0 is
        // the GLB's own binary chunk and this reader measures nothing out of an external one
        static int[] Views(JsonElement root)
        {
            if (!root.TryGetProperty("bufferViews", out JsonElement list) ||
                list.ValueKind != JsonValueKind.Array)
                return Array.Empty<int>();

            var pairs = new List<int>();

            foreach (JsonElement view in list.EnumerateArray())
            {
                int offset = Int(view, "byteOffset", 0);
                int length = Int(view, "byteLength", 0);
                int buffer = Int(view, "buffer", 0);

                pairs.Add(buffer == 0 ? offset : -1);
                pairs.Add(length);
            }

            return pairs.ToArray();
        }

        static int Int(JsonElement element, string field, int fallback) =>
            element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(field, out JsonElement value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)
                ? number
                : fallback;

        // ---- saying no ----

        static void Over(int counted, int cap, string what, string file,
                         List<ContentProblem> problems)
        {
            if (counted <= cap) return;

            problems.Add(new ContentProblem(
                file, what,
                $"this model has {counted:n0} {what} and a mini may have {cap:n0} - decimate it " +
                "in your modelling tool. A piece 20 mm tall under a flat-banded shader cannot " +
                "show the difference, and the cap is what a subscriber's machine is protected by"));
        }

        static Read<ModelFacts> Bad(string file, string where, string what, int line = 0) =>
            Read<ModelFacts>.Bad(new ContentProblem(file, where, what, line));
    }
}
