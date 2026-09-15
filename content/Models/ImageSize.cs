namespace Content.Models
{
    public static class ImageSize
    {
        public static bool TryRead(byte[] bytes, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (bytes == null) return false;

            return Png(bytes, ref width, ref height) || Jpeg(bytes, ref width, ref height);
        }

        // tells "not a picture we read" from "a JPEG whose size sits past this header"; only JPEG needs it, PNG's IHDR is always at byte 16
        public static bool Recognised(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4) return false;

            // JPEG Start Of Image
            if (bytes[0] == 0xFF && bytes[1] == 0xD8) return true;

            return bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 'P' && bytes[2] == 'N' &&
                   bytes[3] == 'G' && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A &&
                   bytes[7] == 0x0A;
        }

        // 8-byte signature, then IHDR whose first two fields are the big-endian dimensions; IHDR is always first, so offsets are fixed
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

        // walk markers to a Start Of Frame; its payload is precision, then height, then width (height first, unlike everything else)
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

                // SOF0..SOF15, minus the four that aren't frame headers (DHT, JPG, DAC)
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
