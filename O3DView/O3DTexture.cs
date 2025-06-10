using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace O3DView;

public class O3DTexture : Texture2D
{
    public string FileName => filename;
    public string? ResolvedPath => resolvedPath;

    private readonly string filename;
    private readonly string basePath;
    private string? resolvedPath;

    private static readonly ConcurrentQueue<(O3DTexture tex, TextureResult data)> textureResults = [];

    public O3DTexture(GL gl, string fileName, string basePath, string? resolvedPath = null) : base(gl)
    {
        this.filename = fileName;
        this.basePath = basePath;
        this.resolvedPath = resolvedPath;
        isValid = false;
        Load();
    }

    #region Async Texture Loading
    internal static void LoadTextureResults()
    {
        // This method gets called every frame by Program in the main thread to synchornise any loaded TextureResults.
        int loaded = 0;
        int maxTexturesLoadedPerFrame = Program.Settings.MaxTexturesLoadedPerFrame;
        while (!textureResults.IsEmpty && loaded < maxTexturesLoadedPerFrame)
        {
            if (textureResults.TryDequeue(out var res))
            {
                res.tex.LoadTextureResult(res.data);
                loaded++;
            }
        }
    }

    private void LoadTextureResult(in TextureResult t)
    {
        var img = t;
        isValid = true;
        Bind();
        if (img.compression != TextureCompressionType.None)
        {
            gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            for (int m = 0; m < img.data.Length; m++)
            {
                var mip = img.data[m];
                gl.CompressedTexImage2D<byte>(TextureTarget.Texture2D, m, img.internalFormat, mip.width, mip.height, 0, (uint)mip.data.Length, mip.data.AsSpan());
            }
        }
        else
        {
            gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            for (int m = 0; m < img.data.Length; m++)
            {
                var mip = img.data[m];
                gl.TexImage2D<byte>(TextureTarget.Texture2D, m, img.internalFormat, mip.width, mip.height, 0, img.pixelFormat, img.pixelType, mip.data.AsSpan());
            }
        }
        gl.GenerateMipmap(TextureTarget.Texture2D);
        gl.TextureParameter(handle, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        gl.TextureParameter(handle, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        gl.TextureParameter(handle, TextureParameterName.TextureMaxAnisotropy, 8);
        Unbind();

        //isValid = false;
    }
    #endregion

    public void Load()
    {
        Task.Run(() =>
        {
            resolvedPath ??= TextureManager.ResolveO3DTexPath(basePath, filename);
            if (resolvedPath == null)
                return;

            TextureResult res;
            try
            {
                res = ReadTexture(resolvedPath);
            }
            catch (Exception ex)
            {
                Program.Log($"Error loading '{resolvedPath}':\n{ex.Message}", "Error while loading texture", GUI.GUIController.Severity.Warning);
                return;
            }

            textureResults.Enqueue((this, res));
        });

        //isValid = true;
    }

    private static TextureResult ReadTexture(string path)
    {
        // This function is thread-safe so it can be run in a task to load textures asynchronously.
        switch (Path.GetExtension(path))
        {
            case ".dds":
                try
                {
                    using var f = File.OpenRead(path);
                    var img = DDSReader.ReadFromStream(f);

                    if (img.textureType != TextureType.Texture2D)
                        throw new NotImplementedException("Can't load non-2D texture!");

                    return new TextureResult()
                    {
                        pixelFormat = img.format,
                        internalFormat = img.internalFormat,
                        pixelType = img.pixelType,
                        compression = img.compression,
                        data = img.data[0].mipMaps.Select(x => new TextureResult.MipMap()
                        {
                            width = x.width,
                            height = x.height,
                            data = x.data
                        }).ToArray()
                    };
                }
                catch (Exception)
                {
                    throw;
                }
            case ".png":
            case ".jpg":
            case ".jpeg":
            case ".tga":
            case ".bmp":
                try
                {
                    using var f = File.OpenRead(path);
                    var img = ImageResult.FromStream(f);
                    var (pixelFormat, internalFormat, pixelType) = img.Comp.ToOpenGLFormat();
                    return new TextureResult()
                    {
                        pixelFormat = pixelFormat,
                        internalFormat = internalFormat,
                        pixelType = pixelType,
                        compression = TextureCompressionType.None,
                        data = [new() { width = (uint)img.Width, height = (uint)img.Height, data = img.Data }]
                    };
                }
                catch (Exception)
                {
                    throw;
                }
            default:
                throw new NotImplementedException();
        }
    }

    internal struct TextureResult
    {
        public PixelFormat pixelFormat;
        public InternalFormat internalFormat;
        public PixelType pixelType;
        public TextureCompressionType compression;
        public MipMap[] data;

        public struct MipMap
        {
            public uint width, height;
            public byte[] data;
        }
    }
}
