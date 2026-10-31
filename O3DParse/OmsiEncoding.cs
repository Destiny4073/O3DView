using System;
using System.Text;

namespace O3DParse;

/// <summary>
/// A single byte code page, implemented from a 128-entry table for the high half (the low
/// half is always ASCII). This avoids a dependency on System.Text.Encoding.CodePages.
/// Bytes with no mapping decode to U+FFFD; characters with no mapping encode to '?'.
/// </summary>
public class OmsiSbcsEncoding : Encoding
{
    private readonly char[] high; // 0x80..0xFF
    private readonly string webName;

    public OmsiSbcsEncoding(string webName, char[] high)
    {
        if (high.Length != 128)
            throw new ArgumentException("The high table must have exactly 128 entries!", nameof(high));
        this.high = high;
        this.webName = webName;
    }

    public override string WebName => webName;

    public override int GetCharCount(byte[] bytes, int index, int count) => count;

    public override int GetChars(byte[] bytes, int index, int count, char[] chars, int charIndex)
    {
        for (int i = 0; i < count; i++)
        {
            byte b = bytes[index + i];
            chars[charIndex + i] = b < 0x80 ? (char)b : high[b - 0x80];
        }
        return count;
    }

    public override int GetByteCount(char[] chars, int index, int count) => count;

    public override int GetBytes(char[] chars, int index, int count, byte[] bytes, int byteIndex)
    {
        for (int i = 0; i < count; i++)
        {
            char c = chars[index + i];
            if (c < 0x80)
            {
                bytes[byteIndex + i] = (byte)c;
            }
            else
            {
                int idx = Array.IndexOf(high, c);
                bytes[byteIndex + i] = idx >= 0 ? (byte)(0x80 + idx) : (byte)'?';
            }
        }
        return count;
    }

    public override int GetMaxCharCount(int byteCount) => byteCount;

    public override int GetMaxByteCount(int charCount) => charCount;
}

/// <summary>
/// The text encodings OMSI content files are written in, and detection of which one a file
/// uses. OMSI is a Delphi program that reads its text files in the system's ANSI code page,
/// so a mod is written in the code page of its author's machine. Ported from openOMSI's
/// omsi-cfg (codepage.rs), which reverse engineered the behaviour.
/// </summary>
public static class OmsiEncoding
{
    // NOTE: The code page tables must be declared before the encodings that use them, static
    // field initializers run in declaration order.
    #region Code page tables (0x80..0xFF; bytes with no mapping are U+FFFD)

    private static readonly char[] Win1252High =
    [
        '\u20AC', '\uFFFD', '\u201A', '\u0192', '\u201E', '\u2026', '\u2020', '\u2021',
        '\u02C6', '\u2030', '\u0160', '\u2039', '\u0152', '\uFFFD', '\u017D', '\uFFFD',
        '\uFFFD', '\u2018', '\u2019', '\u201C', '\u201D', '\u2022', '\u2013', '\u2014',
        '\u02DC', '\u2122', '\u0161', '\u203A', '\u0153', '\uFFFD', '\u017E', '\u0178',
        '\u00A0', '\u00A1', '\u00A2', '\u00A3', '\u00A4', '\u00A5', '\u00A6', '\u00A7',
        '\u00A8', '\u00A9', '\u00AA', '\u00AB', '\u00AC', '\u00AD', '\u00AE', '\u00AF',
        '\u00B0', '\u00B1', '\u00B2', '\u00B3', '\u00B4', '\u00B5', '\u00B6', '\u00B7',
        '\u00B8', '\u00B9', '\u00BA', '\u00BB', '\u00BC', '\u00BD', '\u00BE', '\u00BF',
        '\u00C0', '\u00C1', '\u00C2', '\u00C3', '\u00C4', '\u00C5', '\u00C6', '\u00C7',
        '\u00C8', '\u00C9', '\u00CA', '\u00CB', '\u00CC', '\u00CD', '\u00CE', '\u00CF',
        '\u00D0', '\u00D1', '\u00D2', '\u00D3', '\u00D4', '\u00D5', '\u00D6', '\u00D7',
        '\u00D8', '\u00D9', '\u00DA', '\u00DB', '\u00DC', '\u00DD', '\u00DE', '\u00DF',
        '\u00E0', '\u00E1', '\u00E2', '\u00E3', '\u00E4', '\u00E5', '\u00E6', '\u00E7',
        '\u00E8', '\u00E9', '\u00EA', '\u00EB', '\u00EC', '\u00ED', '\u00EE', '\u00EF',
        '\u00F0', '\u00F1', '\u00F2', '\u00F3', '\u00F4', '\u00F5', '\u00F6', '\u00F7',
        '\u00F8', '\u00F9', '\u00FA', '\u00FB', '\u00FC', '\u00FD', '\u00FE', '\u00FF',
    ];

    private static readonly char[] Win1250High =
    [
        '\u20AC', '\uFFFD', '\u201A', '\uFFFD', '\u201E', '\u2026', '\u2020', '\u2021',
        '\uFFFD', '\u2030', '\u0160', '\u2039', '\u015A', '\u0164', '\u017D', '\u0179',
        '\uFFFD', '\u2018', '\u2019', '\u201C', '\u201D', '\u2022', '\u2013', '\u2014',
        '\uFFFD', '\u2122', '\u0161', '\u203A', '\u015B', '\u0165', '\u017E', '\u017A',
        '\u00A0', '\u02C7', '\u02D8', '\u00A3', '\u00A4', '\u0104', '\u00A6', '\u00A7',
        '\u00A8', '\u00A9', '\u015E', '\u00AB', '\u00AC', '\u00AD', '\u00AE', '\u017B',
        '\u00B0', '\u00B1', '\u02D9', '\u0142', '\u00B4', '\u00B5', '\u00B6', '\u00B7',
        '\u00B8', '\u0105', '\u015F', '\u00BB', '\u013D', '\u02DD', '\u013E', '\u017C',
        '\u0154', '\u00C1', '\u00C2', '\u0102', '\u00C4', '\u0139', '\u0106', '\u00C7',
        '\u010C', '\u00C9', '\u0118', '\u00CB', '\u011A', '\u00CD', '\u00CE', '\u010E',
        '\u0110', '\u0143', '\u0147', '\u00D3', '\u00D4', '\u0150', '\u00D6', '\u00D7',
        '\u0158', '\u016E', '\u00DA', '\u0170', '\u00DC', '\u00DD', '\u0162', '\u00DF',
        '\u0155', '\u00E1', '\u00E2', '\u0103', '\u00E4', '\u013A', '\u0107', '\u00E7',
        '\u010D', '\u00E9', '\u0119', '\u00EB', '\u011B', '\u00ED', '\u00EE', '\u010F',
        '\u0111', '\u0144', '\u0148', '\u00F3', '\u00F4', '\u0151', '\u00F6', '\u00F7',
        '\u0159', '\u016F', '\u00FA', '\u0171', '\u00FC', '\u00FD', '\u0163', '\u02D9',
    ];

    private static readonly char[] Win1251High =
    [
        '\u0402', '\u0403', '\u201A', '\u0453', '\u201E', '\u2026', '\u2020', '\u2021',
        '\u20AC', '\u2030', '\u0409', '\u2039', '\u040A', '\u040C', '\u040B', '\u040F',
        '\u0452', '\u2018', '\u2019', '\u201C', '\u201D', '\u2022', '\u2013', '\u2014',
        '\uFFFD', '\u2122', '\u0459', '\u203A', '\u045A', '\u045C', '\u045B', '\u045F',
        '\u00A0', '\u040E', '\u045E', '\u0408', '\u00A4', '\u0490', '\u00A6', '\u00A7',
        '\u0401', '\u00A9', '\u0404', '\u00AB', '\u00AC', '\u00AD', '\u00AE', '\u0407',
        '\u00B0', '\u00B1', '\u0406', '\u0456', '\u0491', '\u00B5', '\u00B6', '\u00B7',
        '\u0451', '\u2116', '\u0454', '\u00BB', '\u0458', '\u0405', '\u0455', '\u0457',
        '\u0410', '\u0411', '\u0412', '\u0413', '\u0414', '\u0415', '\u0416', '\u0417',
        '\u0418', '\u0419', '\u041A', '\u041B', '\u041C', '\u041D', '\u041E', '\u041F',
        '\u0420', '\u0421', '\u0422', '\u0423', '\u0424', '\u0425', '\u0426', '\u0427',
        '\u0428', '\u0429', '\u042A', '\u042B', '\u042C', '\u042D', '\u042E', '\u042F',
        '\u0430', '\u0431', '\u0432', '\u0433', '\u0434', '\u0435', '\u0436', '\u0437',
        '\u0438', '\u0439', '\u043A', '\u043B', '\u043C', '\u043D', '\u043E', '\u043F',
        '\u0440', '\u0441', '\u0442', '\u0443', '\u0444', '\u0445', '\u0446', '\u0447',
        '\u0448', '\u0449', '\u044A', '\u044B', '\u044C', '\u044D', '\u044E', '\u044F',
    ];

    #endregion

    /// <summary>Windows-1252, the code page of the stock (German) content.</summary>
    public static readonly OmsiSbcsEncoding Windows1252 = new("windows-1252", Win1252High);
    /// <summary>Windows-1250, Central European (Polish, Czech, Slovak, Hungarian...).</summary>
    public static readonly OmsiSbcsEncoding Windows1250 = new("windows-1250", Win1250High);
    /// <summary>Windows-1251, Cyrillic (Russian, Ukrainian, Serbian...).</summary>
    public static readonly OmsiSbcsEncoding Windows1251 = new("windows-1251", Win1251High);

    /// <summary>
    /// Detect which encoding these bytes (without a byte-order mark) were most likely written in:
    /// pure ASCII is read as Windows-1252 (ASCII is ASCII everywhere), valid UTF-8 as UTF-8
    /// (newer mods), then heuristics for Cyrillic, Central European and Czech/Slovak text,
    /// defaulting to Windows-1252.
    /// </summary>
    public static Encoding DetectTextEncoding(ReadOnlySpan<byte> bytes)
    {
        bool ascii = true;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] >= 0x80)
            {
                ascii = false;
                break;
            }
        }
        if (ascii)
            return Windows1252;
        if (IsValidUtf8(bytes))
            return Encoding.UTF8;

        // Russian text is words of Cyrillic letters, i.e. runs of three and more bytes that
        // are letters in Windows-1251; a Western text never has three accented letters in a
        // row (German has at most two, "Größe"), so half of the high letters sitting in such
        // runs means 1251.
        int letters = 0, inRuns = 0, run = 0;
        for (int i = 0; i <= bytes.Length; i++)
        {
            if (i < bytes.Length && CyrillicLetter(bytes[i]))
            {
                letters++;
                run++;
            }
            else
            {
                if (run >= 3)
                    inRuns += run;
                run = 0;
            }
        }
        if (inRuns >= 3 && inRuns * 2 >= letters)
            return Windows1251;

        // A Polish text has 1250 letters that are signs in 1252 next to plain letters
        // ("Głowny", "Łazarz"); "m³/s" in the stock constfiles is neither
        if (CountFlanked(bytes, CentralEuropeanLower, CentralEuropeanUpper) >= 2)
            return Windows1250;

        // A Czech or Slovak one has 1250 letters inside words that would be rare accented
        // letters in 1252 ("Třebenická"), more of them than a Danish text has of its æ and å
        int czech = CountFlanked(bytes, CzechLower, CzechUpper);
        if (czech >= 2 && czech > CountMatches(bytes, Nordic))
            return Windows1250;

        // A Serbian/Croatian/Bosnian/Slovene text has one of its letters inside a word
        // ("BEČEJ", "Ćuprija"), beside a Š or Ž in the same file, and no letter only a
        // Western text has ("Næstved", "Première")
        if (CountSouthSlavic(bytes) >= 1
            && CountMatches(bytes, static b => b is 0x8A or 0x8E or 0x9A or 0x9E) >= 1
            && CountMatches(bytes, WesternOnly) == 0)
            return Windows1250;

        return Windows1252;
    }

    private static bool CyrillicLetter(byte b) => b >= 0xC0 || b == 0xA8 || b == 0xB8;

    private static bool CentralEuropeanLower(byte b) => b is 0xB3 or 0xB9 or 0x9C or 0x9F; // ł ą ś ź

    private static bool CentralEuropeanUpper(byte b) => b is 0xA3 or 0xA5 or 0x8C or 0x8F or 0xAF; // Ł Ą Ś Ź Ż

    private static bool CzechLower(byte b) => b is 0xEC or 0xF8 or 0xF9 or 0xF2 or 0x9D or 0xBE or 0xBF; // ě ř ů ň ť ľ ż

    private static bool CzechUpper(byte b) => b is 0xCC or 0xD8 or 0xD9 or 0xD2 or 0x8D or 0xBC; // Ě Ř Ů Ň Ť Ľ

    private static bool Nordic(byte b) => b is 0xE6 or 0xE5 or 0xC6 or 0xC5; // æ å Æ Å

    private static bool SouthSlavic(byte b) => b is 0xC8 or 0xE8 or 0xC6 or 0xE6 or 0xD0 or 0xF0; // Č č ć æ Đ đ

    private static bool WesternOnly(byte b) => b is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC4 or 0xC5 or 0xC7
        or 0xC9 or 0xCA or 0xCB or 0xCC or 0xCD or 0xCE or 0xCF
        or 0xD1 or 0xD2 or 0xD3 or 0xD4 or 0xD5 or 0xD6
        or 0xD8 or 0xD9 or 0xDA or 0xDB or 0xDC or 0xDD
        or 0xE0 or 0xE1 or 0xE2 or 0xE3 or 0xE4 or 0xE5 or 0xE7
        or 0xE9 or 0xEA or 0xEB or 0xEC or 0xED or 0xEE or 0xEF
        or 0xF1 or 0xF2 or 0xF3 or 0xF4 or 0xF5 or 0xF6
        or 0xF8 or 0xF9 or 0xFA or 0xFB or 0xFC or 0xFD or 0xFF;

    private static bool IsLetterByte(byte b) => char.IsAsciiLetter((char)b) || b >= 0xC0;

    /// <summary>Counts letters of the given kinds flanked by letters ("Głowny": a lower-case
    /// one between two letters; "Łazarz": a capital before one).</summary>
    private static int CountFlanked(ReadOnlySpan<byte> bytes, Func<byte, bool> lower, Func<byte, bool> upper)
    {
        int count = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            bool before = i > 0 && IsLetterByte(bytes[i - 1]);
            bool after = i + 1 < bytes.Length && IsLetterByte(bytes[i + 1]);
            if ((lower(b) && before && after) || (upper(b) && after))
                count++;
        }
        return count;
    }

    /// <summary>Counts South Slavic letters inside a word ("Sremčica", "Ćuprija").</summary>
    private static int CountSouthSlavic(ReadOnlySpan<byte> bytes)
    {
        int count = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            bool after = i + 1 < bytes.Length && IsLetterByte(bytes[i + 1]);
            if (SouthSlavic(b) && after && (b < 0xE0 || (i > 0 && IsLetterByte(bytes[i - 1]))))
                count++;
        }
        return count;
    }

    private static int CountMatches(ReadOnlySpan<byte> bytes, Func<byte, bool> match)
    {
        int count = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (match(bytes[i]))
                count++;
        }
        return count;
    }

    /// <summary>Whether the bytes are valid UTF-8. A multi-byte character cut off by the end
    /// of the buffer (partial reads) counts as valid if everything before it is.</summary>
    private static bool IsValidUtf8(ReadOnlySpan<byte> b)
    {
        int i = 0;
        while (i < b.Length)
        {
            byte c = b[i];
            if (c < 0x80)
            {
                i++;
                continue;
            }

            int len;
            if ((c & 0xE0) == 0xC0) len = 2;
            else if ((c & 0xF0) == 0xE0) len = 3;
            else if ((c & 0xF8) == 0xF0) len = 4;
            else return false;

            if (i + len > b.Length)
            {
                // Possibly a character cut off by the buffer end: everything after the lead
                // byte must be a continuation byte
                for (int j = i + 1; j < b.Length; j++)
                {
                    if ((b[j] & 0xC0) != 0x80)
                        return false;
                }
                return true;
            }

            if ((b[i + 1] & 0xC0) != 0x80)
                return false;
            if (len >= 3 && (b[i + 2] & 0xC0) != 0x80)
                return false;
            if (len >= 4 && (b[i + 3] & 0xC0) != 0x80)
                return false;

            uint cp = len switch
            {
                2 => (uint)((c & 0x1Fu) << 6) | (uint)(b[i + 1] & 0x3Fu),
                3 => (uint)((c & 0x0Fu) << 12) | (uint)((b[i + 1] & 0x3Fu) << 6) | (uint)(b[i + 2] & 0x3Fu),
                _ => (uint)((c & 0x07u) << 18) | (uint)((b[i + 1] & 0x3Fu) << 12) | (uint)((b[i + 2] & 0x3Fu) << 6) | (uint)(b[i + 3] & 0x3Fu),
            };
            if (len == 2 && cp < 0x80)
                return false; // overlong
            if (len == 3 && (cp < 0x800 || (cp >= 0xD800 && cp <= 0xDFFF)))
                return false; // overlong or surrogate
            if (len == 4 && (cp < 0x10000 || cp > 0x10FFFF))
                return false; // overlong or out of range

            i += len;
        }
        return true;
    }

}
