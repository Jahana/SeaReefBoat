using System;

namespace XI.Zone
{
    // CItem::getSignature / setSignature and the 6-bit signature packing (EncodeStringSignature / DecodeStringSignature).
    // The name lives in the exdata: a linkshell's in its own Name (LinkshellItem), a furnishing's at Exdata::Furniture's
    // Signature (0x0A), anything else's at 0x0C, where every equipment exdata kind keeps it. Item.Signature mirrors
    // getSignature.
    public static class ItemSignature
    {
        private const int SignatureAt = 0x0C, FurnitureSignatureAt = 0x0A, SignatureSize = 12;
        private const int EncodedLength = 16; // SignatureStringLength
        private const int DecodedLength = 16; // PacketNameLength
        public const uint ItemFlagInscribable = 0x20; // ItemFlag::Inscribable: signed by its synthesis, and on load

        // getSignature (CItem / CItemLinkshell / CItemFurnishing)
        public static string Get(Item item)
        {
            if (LinkshellItem.Is(item)) return LinkshellItem.Signature(item);
            return Decode(item.Extra.AsSpan(item.Furnishing ? FurnitureSignatureAt : SignatureAt, SignatureSize));
        }

        // setSignature: CItem's clears Signature[12] at 0x0C and copies the first 12 encoded bytes over it, CItemFurnishing's
        // (encodeSignature) copies them over Furniture's, CItemLinkshell's packs the linkshell name
        public static void Set(Item item, string signature)
        {
            if (LinkshellItem.Is(item))
            {
                LinkshellItem.SetSignature(item, signature);
                return;
            }
            var encoded = Encode(signature ?? "");
            int at = item.Furnishing ? FurnitureSignatureAt : SignatureAt;
            Array.Clear(item.Extra, at, SignatureSize);
            Array.Copy(encoded, 0, item.Extra, at, SignatureSize);
            item.Signature = Get(item);
        }

        // EncodeStringSignature: up to 15 chars of 6 bits, most significant first (0-9 1.., A-Z 11.., a-z 37.., anything
        // else 0); strncpy then keeps up to the first zero byte of the 16
        public static byte[] Encode(string signature)
        {
            var bits = new byte[EncodedLength];
            int chars = Math.Min(15, signature.Length);
            for (int i = 0; i < chars; i++)
            {
                char ch = signature[i];
                int code = ch >= '0' && ch <= '9' ? ch - '0' + 1 : ch >= 'A' && ch <= 'Z' ? ch - 'A' + 11 : ch >= 'a' && ch <= 'z' ? ch - 'a' + 37 : 0;
                LinkshellItem.PackBits(bits, code, 6 * i, 6);
            }
            int nul = Array.IndexOf(bits, (byte)0);
            if (nul >= 0) Array.Clear(bits, nul, bits.Length - nul);
            return bits;
        }

        // DecodeStringSignature: 16 codes, 1-62 back to 0-9 A-Z a-z and any other code kept as is, so the name ends at the
        // first zero code (63 reads as '?')
        public static string Decode(ReadOnlySpan<byte> encoded)
        {
            var decoded = new char[DecodedLength];
            for (int i = 0; i < DecodedLength; i++)
            {
                int code = LinkshellItem.UnpackBits(encoded, 6 * i, 6);
                decoded[i] = code >= 1 && code <= 10 ? (char)('0' - 1 + code)
                           : code >= 11 && code <= 36 ? (char)('A' - 11 + code)
                           : code >= 37 && code <= 62 ? (char)('a' - 37 + code)
                           : (char)code;
            }
            int nul = Array.IndexOf(decoded, '\0');
            return new string(decoded, 0, nul < 0 ? DecodedLength : nul);
        }
    }
}
