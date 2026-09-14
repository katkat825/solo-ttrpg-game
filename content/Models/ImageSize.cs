namespace Content.Models
{
    // HOW BIG A TEXTURE IS, FROM ITS FIRST FEW BYTES (MINIS_AND_ART.md A2).
    //
    // The texture-resolution cap needs a width and a height, and the cheapest honest way to get
    // one is to read the header - which is also the only way that is SAFE: decoding a stranger's
    // PNG to find out how big it is means running a decoder over hostile bytes, which is the thing
    // the cap exists to avoid doing. Twenty bytes of header answer the question.
    //
    // TWO FORMATS, BECAUSE glTF ALLOWS TWO. The specification's own words: an image is `image/png`
    // or `image/jpeg`. Anything else in a `mimeType` is refused by `ModelReader` before this is
    // asked, so this need only know the two.
    //
    // IT REFUSES RATHER THAN GUESSES. A file whose header does not parse comes back false, and the
    // caller reports "this is not a PNG or a JPEG" - which is a better sentence than a plausible
    // number read out of the wrong offsets.
    public static class ImageSize
    {
        public static bool TryRead(byte[] bytes, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (bytes == null) return false;

            return Png(bytes, ref width, ref height) || Jpeg(bytes, ref width, ref height);
        }

        // WHETHER THE HEADER AT LEAST CLAIMS TO BE ONE OF THE TWO FORMATS glTF ALLOWS, even when
        // its dimensions sit further in than the few bytes read here reach - a JPEG whose Start Of
        // Frame is behind a large embedded colour profile or thumbnail. It lets a caller tell "not
        // a picture this game reads" from "a picture whose size is past this header", and word the
        // refusal accordingly rather than calling a valid JPEG "not a JPEG". PNG's IHDR is always
        // at byte 16, so a PNG that TryRead cannot size is genuinely malformed; only JPEG has this
        public static bool Recognised(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4) return false;

            // JPEG Start Of Image
            if (bytes[0] == 0xFF && bytes[1] == 0xD8) return true;

            // PNG's 8-byte signature
            return bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 'P' && bytes[2] == 'N' &&
                   bytes[3] == 'G' && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A &&
                   bytes[7] == 0x0A;
        }

        // PNG: an 8-byte signature, then the IHDR chunk, whose first two fields are the dimensions
        // as big-endian 32-bit integers. IHDR is required to be the first chunk, so the offsets
        // are fixed and there is nothing to walk
        static bool Png(byte[] bytes, ref int width, ref int height)
        {
            if (bytes.Length < 24) return false;

            if (bytes[0] != 0x89 || bytes[1] != 'P' || bytes[2] != 'N' || bytes[3] != 'G' ||
                bytes[4] != 0x0D || bytes[5] != 0x0A || bytes[6] != 0x1A || bytes[7] != 0x0A)
                return false;

            if (bytes[12] != 'I' || bytes[13] != 'H' || bytes[14] != 'D' || bytes[15] != 'R')
                return false;

            width = Big(bytes, 16);
            height = Big(bytes, 20);

            return width > 0 && height > 0;
        }

        // JPEG: a marker stream. Walk it until a Start Of Frame, whose payload begins with the
        // precision byte and then the height and the width, big-endian and in that order -
        // which is the wrong way round from every other format and the reason this is worth a
        // comment rather than a glance
        static bool Jpeg(byte[] bytes, ref int width, ref int height)
        {
            if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8) return false;

            int at = 2;

            while (at + 9 < bytes.Length)
            {
                if (bytes[at] != 0xFF) return false;

                int marker = bytes[at + 1];

                // padding: any number of 0xFF bytes may precede a marker
                if (marker == 0xFF) { at++; continue; }

                // markers with no payload at all
                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    at += 2;
                    continue;
                }

                int length = (bytes[at + 2] << 8) | bytes[at + 3];

                if (length < 2) return false;

                // SOF0..SOF15, except the four that are not frame headers (DHT, JPG, DAC, and the
                // restart markers already taken above)
                if (marker >= 0xC0 && marker <= 0xCF &&
                    marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                {
                    if (at + 9 >= bytes.Length) return false;

                    height = (bytes[at + 5] << 8) | bytes[at + 6];
                    width = (bytes[at + 7] << 8) | bytes[at + 8];

                    return width > 0 && height > 0;
                }

                at += 2 + length;
            }

            return false;
        }

        static int Big(byte[] bytes, int at) =>
            (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
    }
}
