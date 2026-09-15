using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Content.Schema;

namespace Content.Models
{
    public static class ModelReader
    {
        // glTF's magic numbers, little-endian
        const uint Magic = 0x46546C67; // "glTF"

        const uint JsonChunk = 0x4E4F534A;

        const uint BinChunk = 0x004E4942;

        // extensions we can honor if a file requires one; agreeing to an unimplemented one would render wrong, not refuse
        static readonly string[] Understood =
        {
            "KHR_materials_unlit",
            "KHR_texture_transform",
            "KHR_mesh_quantization",
            "KHR_materials_emissive_strength",
        };

        // Inspect, not Read: it reports what's in the file; Godot builds the model afterward, only if this said yes
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

            // checked off the filesystem before the file is read into memory
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


        // every length here is a stranger's; each is checked against what's actually present before slicing
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

            // a declared length past the file is usually a truncated download
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

                // unknown chunk types are skipped, per spec, not refused

                at += (int)length;

                // chunks are four-byte aligned; a writer that padded is not an error
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

        // every uri, refused unless it stays inside the pack; checked as written, not resolved
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

                    // a data: uri is bytes inside the already-capped file, not a path
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

        // a uri is percent-escaped; a .. written as %2e%2e is still a ..
        static string Unescaped(string uri)
        {
            try
            {
                return Uri.UnescapeDataString(uri);
            }
            catch (Exception)
            {
                return uri;
            }
        }


        // each accessor's count, read once because meshes refer to them by index repeatedly
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

        // counts as the file declares them: vertices from POSITION, triangles from indices/3 (or vertices/3 when there are none)
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

        // an unnamed animation is legal glTF but useless to a manifest, so it's reported as its index
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

                // an image it can't measure isn't refused; the cap bites what it can see
                if (head == null) continue;

                if (!ImageSize.TryRead(head, out int width, out int height))
                {
                    // a recognised format whose size sits past the header is a different problem from "not a picture"
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

        // only a header's worth, never the whole image; measuring a texture must not mean loading one
        const int Header = 1024;

        static byte[] Head(JsonElement image, int[] views, byte[] binary, string folder)
        {
            if (image.ValueKind != JsonValueKind.Object) return null;

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

            // a data: uri is base64 in the already-capped file; its first kilobyte is enough and cheap
            if (said.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return Embedded(said);

            // the only file this reader opens besides the model; the path is already clean, joined to the pack folder
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
                return null;
            }
        }

        static byte[] Embedded(string uri)
        {
            int comma = uri.IndexOf(',');

            if (comma < 0 || !uri.Substring(0, comma).EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
                return null;

            // a header's worth, rounded to a base64 quantum, not the whole picture
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

        // offset/length pairs; a view into any buffer but 0 gets a negative offset, since only buffer 0 is the GLB's own chunk
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
