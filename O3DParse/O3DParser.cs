using System;
using System.Text;

namespace O3DParse;

public static class O3DParser
{
    /// <summary>
    /// Loads an O3D file from a file path.
    /// </summary>
    /// <param name="path">The path to the O3D file to load.</param>
    /// <returns>The parsed O3D file.</returns>
    /// <exception cref="O3DException"></exception>
    /// <exception cref="IOException"></exception>
    public static O3DFile ReadO3D(string path)
    {
        O3DFile o3d = new();
        using FileStream f = File.OpenRead(path);
        using O3DBinaryReader reader = new(f, OmsiEncoding.Windows1252);

        if (reader.ReadUInt16() != 0x1984)
            throw new O3DException("O3D file does not have a valid header!");

        o3d.version = reader.ReadByte();

        // Version 3 and up carry the extended options byte (bit 0: 32-bit triangle indices,
        // bit 1: alternative scramble seed) and widen the vertex/triangle counts to 32-bit;
        // version 4 and up carry the scramble key.
        bool wideCounts = false;
        bool altSeed = false;
        if (o3d.version >= 3)
        {
            o3d.extendedOptions = (O3DExtendedOptions)reader.ReadByte();
            wideCounts = true;
            altSeed = (o3d.extendedOptions & O3DExtendedOptions.ALT_ENCRYPTION_SEED) != 0;
        }

        bool scrambled = false;
        if (o3d.version >= 4)
        {
            o3d.encryptionKey = reader.ReadUInt32();
            // Any key other than 0xFFFFFFFF means the vertex records are scrambled (the key
            // itself is in the header). The original also refuses keys outside its
            // registration list; that check is deliberately not reproduced.
            scrambled = o3d.encryptionKey != 0xffffffff;
        }

        ushort state = scrambled ? O3DScramble.InitialState(o3d.encryptionKey, o3d.version, altSeed) : (ushort)0;
        uint n16 = 0;
        uint fb = 0;

        while (reader.PeekChar() != -1)
        {
            switch (reader.ReadByte())
            {
                case 0x17:
                    {
                        uint len = wideCounts ? reader.ReadUInt32() : reader.ReadUInt16();
                        if (len > 1000000)
                            throw new O3DException($"Tried to read {len} vertices! That's quite a lot, is the file corrupted?");
                        o3d.vertices = new O3DVert[len];
                        n16 = len % 0xFDE8;
                        fb = 0;
                        for (int i = 0; i < o3d.vertices.Length; i++)
                        {
                            O3DVec3 pos = reader.ReadVec3();
                            O3DVec3 normal = reader.ReadVec3();
                            O3DVec2 uv = reader.ReadVec2();
                            if (scrambled)
                            {
                                if (o3d.encryptionKey == 0)
                                    state = altSeed ? (ushort)0x130 : (ushort)0;
                                state = (ushort)((state * n16 + n16 * fb) % 8000);
                                fb = O3DScramble.FracByte(pos);
                                O3DScramble.Unscramble(state, ref pos, ref normal, ref uv);
                            }
                            o3d.vertices[i] = new(pos, normal, uv);
                        }
                        break;
                    }
                case 0x49:
                    {
                        uint len = wideCounts ? reader.ReadUInt32() : reader.ReadUInt16();
                        if (len > 1000000)
                            throw new O3DException($"Tried to read {len} triangles! That's quite a lot, is the file corrupted?");
                        o3d.triangles = new O3DTri[len];
                        if ((o3d.extendedOptions & O3DExtendedOptions.LONG_TRIANGLE_INDICES) != 0)
                            for (int i = 0; i < o3d.triangles.Length; i++)
                                o3d.triangles[i] = new(reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt16());
                        else
                            for (int i = 0; i < o3d.triangles.Length; i++)
                                o3d.triangles[i] = new(reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16());
                        break;
                    }
                case 0x26:
                    {
                        // The material count is always 16-bit, even in files with 32-bit counts elsewhere
                        uint len = reader.ReadUInt16();
                        if (len > 10000)
                            throw new O3DException($"Tried to read {len} materials! That's quite a lot, is the file corrupted?");
                        o3d.materials = new O3DMaterial[len];
                        for (int i = 0; i < o3d.materials.Length; i++)
                            o3d.materials[i] = new(reader.ReadVec4(), reader.ReadVec3(), reader.ReadVec3(), reader.ReadSingle(), reader.ReadString());
                        break;
                    }
                case 0x54:
                    {
                        // The bone count is always 16-bit, even in files with 32-bit counts elsewhere
                        uint len = reader.ReadUInt16();
                        if (len > 1000000)
                            throw new O3DException($"Tried to read {len} bones! That's quite a lot, is the file corrupted?");
                        o3d.bones = new O3DBone[len];
                        for (int i = 0; i < o3d.bones.Length; i++)
                            o3d.bones[i] = new(reader.ReadString(), reader.ReadWeights((o3d.extendedOptions & O3DExtendedOptions.LONG_TRIANGLE_INDICES) != 0));
                        break;
                    }
                case 0x79:
                    o3d.transform = reader.ReadMatrix();
                    break;
                default:
                    // Any other byte is passed over, one at a time, as the original loader does
                    // (the tag case has no else, the loop reads the next byte). Protected v7
                    // files carry junk bytes inside their transform section; refusing them
                    // loses whole vehicle packs.
                    break;
            }
        }

        if (o3d.triangles is { Length: > 0 })
            o3d.triangles = DropBadTriangles(o3d.triangles, o3d.vertices != null ? (uint)o3d.vertices.Length : 0);

        return o3d;
    }

    /// <summary>
    /// Leaves out triangles naming a vertex the file doesn't have (a damaged or hand-made
    /// file; every user of the mesh indexes the vertices with them).
    /// </summary>
    private static O3DTri[] DropBadTriangles(O3DTri[] triangles, uint vertCount)
    {
        int kept = 0;
        for (int i = 0; i < triangles.Length; i++)
        {
            O3DTri tri = triangles[i];
            if (tri.a < vertCount && tri.b < vertCount && tri.c < vertCount)
                triangles[kept++] = tri;
        }
        return kept == triangles.Length ? triangles : triangles[..kept];
    }

    /// <summary>
    /// Writes an O3D file to disk.
    /// </summary>
    /// <param name="path">The filepath to write to.</param>
    /// <param name="o3d">The O3D file to encrypt.</param>
    /// <exception cref="O3DException"></exception>
    /// <exception cref="IOException"></exception>
    public static void WriteO3D(string path, O3DFile o3d)
    {
        if (o3d.encryptionKey != 0xffffffff && o3d.version <= 3)
            throw new O3DException($"O3D version {o3d.version} does not support encryption!");

        bool longHeader = o3d.version >= 3;
        bool altSeed = (o3d.extendedOptions & O3DExtendedOptions.ALT_ENCRYPTION_SEED) != 0;
        // Vertices are scrambled on write for any key other than 0xFFFFFFFF
        bool scramble = o3d.version >= 4 && o3d.encryptionKey != 0xffffffff;
        ushort state = scramble ? O3DScramble.InitialState(o3d.encryptionKey, o3d.version, altSeed) : (ushort)0;

        using FileStream f = File.Open(path, FileMode.Create);
        using O3DBinaryWriter writer = new(f, OmsiEncoding.Windows1252);

        // Magic number
        writer.Write((ushort)0x1984);
        // File version
        writer.Write(o3d.version);
        if (longHeader)
        {
            // Extended header
            writer.Write((byte)o3d.extendedOptions);
            // Encryption key
            writer.Write(o3d.encryptionKey);
        }

        // Verts
        if (o3d.vertices.Length > 0)
        {
            writer.Write((byte)0x17);
            writer.WriteHeader(o3d.vertices, longHeader);
            uint n16 = (uint)o3d.vertices.Length % 0xFDE8;
            uint fb = 0;
            for (int i = 0; i < o3d.vertices.Length; i++)
            {
                O3DVec3 pos = o3d.vertices[i].pos;
                O3DVec3 normal = o3d.vertices[i].normal;
                O3DVec2 uv = o3d.vertices[i].uv;
                if (scramble)
                {
                    if (o3d.encryptionKey == 0)
                        state = altSeed ? (ushort)0x130 : (ushort)0;
                    state = (ushort)((state * n16 + n16 * fb) % 8000);
                    O3DScramble.Scramble(state, ref pos, ref normal, ref uv);
                    fb = O3DScramble.FracByte(pos);
                }
                writer.WriteVec3(pos);
                writer.WriteVec3(normal);
                writer.WriteVec2(uv);
            }
        }

        // Tris
        if (o3d.triangles.Length > 0)
        {
            writer.Write((byte)0x49);
            writer.WriteHeader(o3d.triangles, longHeader);
            for (int i = 0; i < o3d.triangles.Length; i++)
            {
                if ((o3d.extendedOptions & O3DExtendedOptions.LONG_TRIANGLE_INDICES) != 0)
                {
                    writer.Write(o3d.triangles[i].a);
                    writer.Write(o3d.triangles[i].b);
                    writer.Write(o3d.triangles[i].c);
                }
                else
                {
                    writer.Write((ushort)o3d.triangles[i].a);
                    writer.Write((ushort)o3d.triangles[i].b);
                    writer.Write((ushort)o3d.triangles[i].c);
                }
                writer.Write(o3d.triangles[i].mat);
            }
        }

        // Materials
        if (o3d.materials.Length > 0)
        {
            writer.Write((byte)0x26);
            writer.WriteHeader(o3d.materials, false);
            for (int i = 0; i < o3d.materials.Length; i++)
            {
                writer.WriteVec4(o3d.materials[i].diffuse);
                writer.WriteVec3(o3d.materials[i].spec);
                writer.WriteVec3(o3d.materials[i].emission);
                writer.Write(o3d.materials[i].specPower);
                writer.WriteString(o3d.materials[i].textureName);
            }
        }

        // Bones
        if (o3d.bones.Length > 0)
        {
            writer.Write((byte)0x54);
            // The bone count is always 16-bit, even in files with 32-bit counts elsewhere
            writer.WriteHeader(o3d.bones, false);
            for (int i = 0; i < o3d.bones.Length; i++)
            {
                writer.WriteString(o3d.bones[i].name);
                writer.WriteWeights(o3d.bones[i].weights, (o3d.extendedOptions & O3DExtendedOptions.LONG_TRIANGLE_INDICES) != 0);
            }
        }

        // Transform
        writer.Write((byte)0x79);
        writer.WriteMatrix(o3d.transform);
    }
}
