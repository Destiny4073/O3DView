using System;

namespace O3DParse;

/// <summary>
/// The per-vertex scrambling ("protection") of O3D files of version 4 and up: vertices are
/// swapped, negated and UV-offset by a state machine whose state also mixes in the raw
/// (still scrambled) position of the previous vertex. The key is stored in the file header
/// itself; the original additionally refuses keys outside its registration list, which is
/// deliberately not reproduced. Ported from openOMSI's omsi-o3d (reverse engineered from
/// Omsi.exe's loader <c>mc_o3dfile</c> at 0x56c74c and writer at 0x56af20).
/// </summary>
internal static class O3DScramble
{
    /// <summary>
    /// The initial state of the scramble state machine for a file.
    /// </summary>
    /// <param name="key">The encryption key from the file header.</param>
    /// <param name="version">The O3D file version.</param>
    /// <param name="alt">Whether the <see cref="O3DExtendedOptions.ALT_ENCRYPTION_SEED"/> flag is set.</param>
    public static ushort InitialState(uint key, byte version, bool alt)
    {
        long k = key;
        k += version - 4;
        if (alt)
            k += 0x17D;
        return (ushort)(k % 0xFDE8);
    }

    /// <summary>
    /// Mixing byte derived from the raw (still scrambled) position of a vertex; feeds the
    /// state of the <em>next</em> vertex.
    /// </summary>
    public static uint FracByte(O3DVec3 p)
    {
        double f(double x) => x - Math.Truncate(x);
        double prod = Math.Abs(f(p.x) * f(p.y) * f(p.z)) * 600.0;
        return (uint)((long)Math.Truncate(prod) % 256);
    }

    /// <summary>
    /// Undo the per-vertex scrambling of version 4+ files.
    /// </summary>
    public static void Unscramble(ushort s, ref O3DVec3 p, ref O3DVec3 n, ref O3DVec2 uv)
    {
        if (s < 1000) (p.x, p.y) = (p.y, p.x);
        else if (s < 3000) (p.x, p.z) = (p.z, p.x);
        else if (s > 7000) (p.y, p.z) = (p.z, p.y);

        if ((s & 3) == 0) n.x = -n.x;
        if (s % 6 == 0) n.y = -n.y;
        if (s % 7 == 0) n.z = -n.z;

        // The s > 6500 branch can never run (every such state already took s > 4500), so the
        // x/z normal swap never happens. The exe has the same dead test in both directions.
        // Kept as is so files decode exactly as OMSI decodes them.
        if (s < 600) (n.y, n.z) = (n.z, n.y);
        else if (s > 4500) (n.x, n.y) = (n.y, n.x);
        else if (s > 6500) (n.x, n.z) = (n.z, n.x);

        if (s % 5 == 0)
        {
            float m = s % 100;
            uv.x -= m * m / 10000f;
        }
        if (s % 3 == 0)
        {
            float m = s % 50;
            uv.y -= m * m / 2500f;
        }
    }

    /// <summary>
    /// Scramble a vertex for writing, the way the original's writer does it: swap the
    /// position, swap the normal, then negate; the texture coordinates are moved up.
    /// </summary>
    public static void Scramble(ushort s, ref O3DVec3 p, ref O3DVec3 n, ref O3DVec2 uv)
    {
        if (s < 1000) (p.x, p.y) = (p.y, p.x);
        else if (s < 3000) (p.x, p.z) = (p.z, p.x);
        else if (s > 7000) (p.y, p.z) = (p.z, p.y);

        if (s < 600) (n.y, n.z) = (n.z, n.y);
        else if (s > 4500) (n.x, n.y) = (n.y, n.x);

        if ((s & 3) == 0) n.x = -n.x;
        if (s % 6 == 0) n.y = -n.y;
        if (s % 7 == 0) n.z = -n.z;

        if (s % 5 == 0)
        {
            float m = s % 100;
            uv.x += m * m / 10000f;
        }
        if (s % 3 == 0)
        {
            float m = s % 50;
            uv.y += m * m / 2500f;
        }
    }
}
