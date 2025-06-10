using Silk.NET.OpenGL;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace O3DView;

public static class TextureManager
{
    public static readonly Dictionary<int, Material> materialCache = [];
    private static readonly ConcurrentDictionary<(string basePath, string filename), string?> resolvedPathCache = [];
    private static readonly Dictionary<string, O3DTexture> textureCache = [];
    private static readonly ConcurrentDictionary<string, string> textureChanges = [];
    //public readonly Dictionary<string>

    /// <summary>
    /// Replaces a given texture with another one, used by freetex, texchanges, and ctctexture.
    /// </summary>
    /// <param name="originalName"></param>
    /// <param name="newPath">The fully resolved path to the new texture.</param>
    /// <returns></returns>
    public static bool ReplaceTexture(string originalName, string? newPath)
    {
        textureChanges.TryRemove(originalName, out _);
        if (newPath != null)
            return textureChanges.TryAdd(originalName, newPath);
        return true;
    }

    public static void UnReplaceTextures()
    {
        textureChanges.Clear();
    }

    public static string? ResolveO3DTexPath(string basePath, string filename)
    {
        if (resolvedPathCache.TryGetValue((basePath, filename), out var res))
            return res;

        string? dir = Path.GetDirectoryName(basePath);
        string ddsName = Path.ChangeExtension(filename, ".dds");
        string testPath;
        while (!string.IsNullOrEmpty(dir))
        {
            testPath = Path.Combine(dir, ddsName);
            if (File.Exists(testPath))
                goto FoundResult;

            testPath = Path.Combine(dir, "texture", ddsName);
            if (File.Exists(testPath))
                goto FoundResult;

            testPath = Path.Combine(dir, filename);
            if (File.Exists(testPath))
                goto FoundResult;

            testPath = Path.Combine(dir, "texture", filename);
            if (File.Exists(testPath))
                goto FoundResult;

            dir = Directory.GetParent(dir)?.FullName;
        }
        resolvedPathCache.TryAdd((basePath, filename), null);
        return null;

        FoundResult:
        resolvedPathCache.TryAdd((basePath, filename), testPath);
        return testPath;
    }

    /// <summary>
    /// Gets a cached O3DTexture from it's filename.
    /// </summary>
    /// <param name="fileName">The filename of the O3D texture.</param>
    /// <param name="basePath">The folder containing the model file.</param>
    /// <returns>The cached O3DTexture, or null if one hasn't been loaded yet.</returns>
    public static O3DTexture? GetCached(string fileName, string basePath)
    {
        if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(fileName))
            return null;
        if (!textureChanges.TryGetValue(fileName, out string? resolved))
        {
            basePath = Path.GetFullPath(basePath);
            resolved = ResolveO3DTexPath(basePath, fileName);
        }
        if (resolved == null)
            return null;

        if (textureCache.TryGetValue(resolved, out O3DTexture? value))
            return value;
        return null;
    }

    /// <summary>
    /// Creates or returns a cached instance of a texture referenced by an O3D file.
    /// </summary>
    /// <param name="gl"></param>
    /// <param name="fileName"></param>
    /// <param name="basePath"></param>
    /// <returns></returns>
    public static O3DTexture? CreateOrUseCached(GL gl, string fileName, string basePath)
    {
        if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(fileName))
            return null;
        // If the texture has been changed use that one instead.
        if (!textureChanges.TryGetValue(fileName, out string? resolved))
        {
            basePath = Path.GetFullPath(basePath);
            resolved = ResolveO3DTexPath(basePath, fileName);
        }
        if (resolved == null)
            return null;

        if (textureCache.TryGetValue(resolved, out O3DTexture? value))
            return value;

        var tex = new O3DTexture(gl, fileName, basePath, resolved);
        textureCache.Add(resolved, tex);
        return tex;
    }

    public static void ReloadTextures()
    {
        //var textures = textureCache.Values.ToArray();
        foreach (var tex in textureCache.Values)
        {
            tex?.Invalidate();
            tex?.Load();
        }
        //textureCache.Clear();
    }
}
