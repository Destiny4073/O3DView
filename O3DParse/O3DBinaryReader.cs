using System;
using System.Buffers;
using System.Text;

namespace O3DParse;

internal class O3DBinaryReader : BinaryReader
{
    private readonly Encoding encoding;

    public O3DBinaryReader(Stream input, Encoding encoding) : base(input, encoding)
    {
        this.encoding = encoding;
    }

    public O3DVec4 ReadVec4() => new(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());

    public O3DVec3 ReadVec3() => new(ReadSingle(), ReadSingle(), ReadSingle());

    public O3DVec2 ReadVec2() => new(ReadSingle(), ReadSingle());

    public O3DTransform ReadMatrix() => new(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle(),
        ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle(),
        ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle(),
        ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());

    public O3DWeight[] ReadWeights(bool longIndices)
    {
        int nWeights = ReadUInt16();
        O3DWeight[] weights = new O3DWeight[nWeights];
        for(int i = 0; i < nWeights; i++)
        {
            weights[i] = new(longIndices?ReadUInt32():ReadUInt16(), ReadSingle());
        }

        return weights;
    }

    public override string ReadString()
    {
        //return base.ReadString();
        int len = ReadByte();

        byte[]? bytes = null;
        scoped Span<byte> buff;
        if (len <= 2048)
            buff = stackalloc byte[len];
        else
        {
            bytes = ArrayPool<byte>.Shared.Rent(len);
            buff = bytes.AsSpan(0, len);
        }

        int numRead = 0;
        while (numRead < len)
        {
            int read = BaseStream.Read(buff[numRead..]);
            numRead += read;
            if (read == 0)
                throw new EndOfStreamException();
        }
        var ret = encoding.GetString(buff[..numRead]);

        if (bytes != null)
            ArrayPool<byte>.Shared.Return(bytes);

        return ret;
    }
}
