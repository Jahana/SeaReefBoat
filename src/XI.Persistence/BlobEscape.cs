using System;
using System.Text;

namespace XI.Persistence
{
    // Mirror of db::BlobWrapper::toString() (src/common/database/blob.cpp) — the blob-write
    // escape. Verified byte-for-byte against the compiled oracle (csharp/oracle/blob_golden.txt).
    //
    // NOT the same as SqlEscape.Escape (db::escapeString)! Three divergences matter:
    //   - NUL is EMITTED as \0 (SqlEscape breaks on NUL instead).
    //   - '%' doubles to '%%' (SqlEscape uses backslash-percent).
    //   - '|' and ';' are left UNCHANGED (SqlEscape backslash-escapes them).
    // No strlen break: all bytes (including >=0x80) are processed.
    public static class BlobEscape
    {
        public static string ToString(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(data.Length * 2);
            foreach (var b in data)
            {
                switch (b)
                {
                    case 0x00: sb.Append("\\0"); break;   // NUL  -> \0
                    case 0x08: sb.Append("\\b"); break;   // BS   -> \b
                    case 0x09: sb.Append("\\t"); break;   // TAB  -> \t
                    case 0x0A: sb.Append("\\n"); break;   // LF   -> \n
                    case 0x0D: sb.Append("\\r"); break;   // CR   -> \r
                    case 0x1A: sb.Append("\\Z"); break;   // SUB  -> \Z
                    case 0x22: sb.Append("\\\""); break;  // "    -> \"
                    case 0x25: sb.Append("%%"); break;    // %    -> %% (double, no backslash)
                    case 0x27: sb.Append("\\'"); break;   // '    -> \'
                    case 0x5C: sb.Append("\\\\"); break;  // \    -> \\
                    default: sb.Append((char)b); break;   // everything else verbatim (| ; high bytes)
                }
            }

            return sb.ToString();
        }
    }

    // Mirror of extractFromBlob's memcpy rule (src/common/database/result_set.h):
    // on a NULL blob the whole path is SKIPPED (login-server null blobs must not clobber
    // map-initialized data); otherwise the destination is zero-initialized and the first
    // min(sizeof(T), blobLen) bytes are copied in. Returns the fully-sized destination bytes.
    public static class BlobExtract
    {
        // destSize == sizeof(T). isNull == rset->isNull(blobKey).
        public static byte[] Extract(int destSize, byte[] blob, bool isNull)
        {
            var dest = new byte[destSize];

            if (isNull)
            {
                // C++ leaves `destination` untouched on a null blob. For a fresh struct that is
                // zero-initialized already, so we return zeros here; callers reusing a populated
                // destination must skip the write (see the note in result_set.h).
                return dest;
            }

            var blobLen = blob?.Length ?? 0;
            var n = Math.Min(destSize, blobLen);
            Array.Copy(blob ?? Array.Empty<byte>(), dest, n);
            return dest;
        }
    }
}
