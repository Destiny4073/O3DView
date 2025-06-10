using System.Numerics;
using O3DParse;
using O3DParse.Ini;
using Silk.NET.OpenGL;

namespace O3DView
{
    public class Material
    {
        public Vector4 albedo;
        public Vector3 spec;
        public Vector3 emission;
        public BlendMode blendMode;
        public DepthMode depthMode;
        public float roughness;
        public Texture2D? texture;
        public float alphaScale = 1;

        public Texture2D? transMap;
        public Texture2D? nightMap;
        public Texture2D? lightMap;
        public float lightMapStrength = 1;
        public Texture2D? envMap;
        public float envMapStrength;
        public Texture2D? bumpMap;
        public float bumpMapStrength;
        public bool isShadow;
        //public Texture2D? envMapMask;

        public RenderQueue renderQueue => blendMode == BlendMode.Opaque || blendMode == BlendMode.Cutout ? RenderQueue.Opaque : RenderQueue.Transparent;
        public MatlCommand? MatlCommand => matlCommand;

        public Shader shader;
        private readonly GL gl;
        private Texture2D[] boundTextures = new Texture2D[6];
        private MatlCommand? matlCommand;
        private string? basePath;
        private string? textureName;

        public Material(GL gl, Shader shader)
        {
            this.shader = shader;
            this.gl = gl;
        }

        public void UpdateFromO3D(O3DMaterial o3d, string o3dPath)
        {
            basePath = o3dPath;
            albedo = o3d.diffuse;
            spec = o3d.spec;
            roughness = 1 / (o3d.specPower + 1);
            emission = o3d.emission;
            if (texture is O3DTexture o3dTex && o3dTex.FileName != o3d.textureName)
            {
                texture.Dispose();
                texture = null;
            }
            texture ??= TextureManager.CreateOrUseCached(gl, o3d.textureName, basePath);
            textureName = o3d.textureName;
        }

        /*public void UpdateTextureFromPath(string path)
        {
            texture ??= TextureManager.CreateOrUseCached(gl, path, basePath ?? string.Empty);
        }*/

        /// <summary>
        /// Re-resolves all the textures used by this material. Call this when a texture change is executed.
        /// </summary>
        public void UpdateTextures()
        {
            if (basePath == null)
                return;
            if (textureName != null)
                texture = TextureManager.CreateOrUseCached(gl, textureName, basePath);

            if (matlCommand == null)
                return;
            if (!string.IsNullOrEmpty(matlCommand.transmap?.path))
                transMap = TextureManager.CreateOrUseCached(gl, matlCommand.transmap.path, basePath);
            if (!string.IsNullOrEmpty(matlCommand.nightmap?.path))
                nightMap = TextureManager.CreateOrUseCached(gl, matlCommand.nightmap.path, basePath);
            if (!string.IsNullOrEmpty(matlCommand.lightmap?.path))
                lightMap = TextureManager.CreateOrUseCached(gl, matlCommand.lightmap.path, basePath);
            if (!string.IsNullOrEmpty(matlCommand.envmap?.path))
                envMap = TextureManager.CreateOrUseCached(gl, matlCommand.envmap.path, basePath);
            if (!string.IsNullOrEmpty(matlCommand.bumpmap?.path))
                bumpMap = TextureManager.CreateOrUseCached(gl, matlCommand.bumpmap.path, basePath);
        }

        public void UpdateFromCfgMatl(MatlCommand cfgMatl, string cfgPath)
        {
            matlCommand = cfgMatl;
            basePath = cfgPath;
            if (cfgMatl.noZCheck != null)
                depthMode &= ~DepthMode.ZTest;
            if (cfgMatl.noZWrite != null)
                depthMode &= ~DepthMode.ZWrite;
            if (cfgMatl.alpha != null)
            {
                switch (cfgMatl.alpha?.alphaMode)
                {
                    case MatlAlphaMode.Opaque:
                        blendMode = BlendMode.Opaque;
                        break;
                    case MatlAlphaMode.Cutout:
                        blendMode = BlendMode.Cutout;
                        break;
                    case MatlAlphaMode.Blend:
                        blendMode = BlendMode.AlphaBlend;
                        break;
                }
            }
            if (cfgMatl.allcolor is AllcolorCommand allcolor)
            {
                albedo = allcolor.diffuse;
                spec = allcolor.specular;
                roughness = 1 / (allcolor.specPower + 1);
                emission = allcolor.emission + allcolor.ambient;
            }
            if (cfgMatl.bumpmap is MatlBumpmapCommand bumpMapCmd)
            {
                bumpMap ??= TextureManager.CreateOrUseCached(gl, bumpMapCmd.path, cfgPath);
                bumpMapStrength = bumpMapCmd.strength;
            }
            if (cfgMatl.transmap is MatlTransmapCommand transmapCmd)
                transMap ??= TextureManager.CreateOrUseCached(gl, transmapCmd.path, cfgPath);
            if (cfgMatl.envmap is MatlEnvmapCommand envmapCmd)
            {
                envMap ??= TextureManager.CreateOrUseCached(gl, envmapCmd.path, cfgPath);
                envMapStrength = envmapCmd.reflectivity;
            }
            if (cfgMatl.lightmap is MatlLightmapCommand lightmapCmd)
            {
                lightMap ??= TextureManager.CreateOrUseCached(gl, lightmapCmd.path, cfgPath);
            }
            if (cfgMatl.nightmap is MatlNightmapCommand nightmapCmd)
            {
                nightMap ??= TextureManager.CreateOrUseCached(gl, nightmapCmd.path, cfgPath);
            }
        }

        public static Material CreateFromO3D(O3DMaterial o3dMat, string o3dPath, Shader shader, GL gl, SceneGraph scene,
            MatlCommand? cfgMatl = null, string? cfgPath = null, bool isShadow = false)
        {
            // TODO: Re-enable material cache. For now it's disabled since it will combine identical materials shared by different
            // meshes/submeshes which the user may not want.
            if (TextureManager.materialCache.ContainsKey(o3dMat.GetHashCode()))
                return TextureManager.materialCache[o3dMat.GetHashCode()];

            Material mat = new(gl, shader);
            mat.matlCommand = cfgMatl;
            mat.basePath = cfgPath ?? o3dPath;
            mat.isShadow = isShadow;
            mat.UpdateFromO3D(o3dMat, o3dPath);
            if (mat.albedo.W < 1 || ((mat.texture?.IsValid ?? false) && (mat.texture.Format == PixelFormat.Rgba || mat.texture.Format == PixelFormat.Alpha)))
            {
                // This won't work correctl if the texture hasn't loaded yet...
                mat.blendMode = BlendMode.AlphaBlend;
                mat.depthMode = DepthMode.Opaque;
            }
            else
            {
                mat.blendMode = BlendMode.Opaque;
                mat.depthMode = DepthMode.Opaque;
            }

            if (cfgMatl != null && cfgPath != null)
            {
                mat.blendMode = BlendMode.Opaque;
                mat.UpdateFromCfgMatl(cfgMatl, cfgPath);
            }

            if (Program.Settings.ShouldCacheMaterials)
                TextureManager.materialCache.Add(o3dMat.GetHashCode(), mat);
            return mat;
        }

        private void BindTextures()
        {
            int bindInd = 0;
            if (texture != null)
            {
                boundTextures[bindInd] = texture;
                shader.SetUniform("uMainTex", bindInd++);
                shader.SetUniform("uUseMainTex", true);
            }
            else
                shader.SetUniform("uUseMainTex", false);

            if (transMap != null)
            {
                boundTextures[bindInd] = transMap;
                shader.SetUniform("uTransmap", bindInd++);
                shader.SetUniform("uUseTransmap", true);
            }
            else
                shader.SetUniform("uUseTransmap", false);

            if (nightMap != null)
            {
                boundTextures[bindInd] = nightMap;
                shader.SetUniform("uNightmap", bindInd++);
                shader.SetUniform("uUseNightmap", true);
            }
            else
                shader.SetUniform("uUseNightmap", false);

            if (lightMap != null)
            {
                boundTextures[bindInd] = lightMap;
                shader.SetUniform("uLightmap", bindInd++);
                shader.SetUniform("uUseLightmap", true);
                shader.SetUniform("uLightmapStrength", lightMapStrength);
            }
            else
                shader.SetUniform("uUseLightmap", false);

            if (envMap != null)
            {
                boundTextures[bindInd] = envMap;
                shader.SetUniform("uEnvmap", bindInd++);
                shader.SetUniform("uUseEnvmap", true);
                shader.SetUniform("uEnvmapStrength", envMapStrength);
            }
            else
                shader.SetUniform("uUseEnvmap", false);

            if (bumpMap != null)
            {
                boundTextures[bindInd] = bumpMap;
                shader.SetUniform("uBumpmap", bindInd++);
                shader.SetUniform("uUseBumpmap", true);
                shader.SetUniform("uBumpmapStrength", bumpMapStrength);
            }
            else
                shader.SetUniform("uUseBumpmap", false);

            Texture2D.BindTextures(gl, 0, boundTextures.AsSpan()[..bindInd]);
        }

        public void Bind(bool needsToBindShader, ref (BlendingFactor src, BlendingFactor dst) lastBlendFunc, ref DepthMode lastDepthMode)
        {
            if (needsToBindShader)
                shader.Use();

            BindTextures();

            shader.SetUniform("uAlbedo", albedo);
            shader.SetUniform("uAlphaScale", alphaScale);
            shader.SetUniform("uSpec", spec);
            shader.SetUniform("uRough", roughness);
            shader.SetUniform("uEmission", emission);
            (BlendingFactor src, BlendingFactor dst) blendFunc = blendMode switch
            {
                BlendMode.Opaque => (BlendingFactor.One, BlendingFactor.Zero),
                BlendMode.Cutout => (BlendingFactor.One, BlendingFactor.Zero),
                BlendMode.AlphaBlend => (BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha),
                BlendMode.AlphaPreMul => (BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha),
                BlendMode.Add => (BlendingFactor.One, BlendingFactor.One),
                _ => throw new NotImplementedException(),
            };
            if (blendFunc != lastBlendFunc)
            {
                gl.BlendFunc(blendFunc.src, blendFunc.dst);
                lastBlendFunc = blendFunc;
            }
            if (blendMode == BlendMode.Cutout)
                shader.SetUniform("uCutout", true);
            else
                shader.SetUniform("uCutout", false);

            var _depthMode = depthMode;
            if (isShadow)
                _depthMode = DepthMode.Opaque;

            if ((_depthMode & DepthMode.ZTest) != (lastDepthMode & DepthMode.ZTest))
            {
                //if ((_depthMode & DepthMode.ZTest) != 0)
                //    gl.DepthFunc(DepthFunction.Lequal);
                //else
                //    gl.DepthFunc(DepthFunction.Always);
            }
            if ((_depthMode & DepthMode.ZWrite) != (lastDepthMode & DepthMode.ZWrite))
            {
                if ((_depthMode & DepthMode.ZWrite) != 0)
                    gl.DepthMask(true);
                else
                    gl.DepthMask(false);
            }
            lastDepthMode = _depthMode;
        }

        public enum BlendMode
        {
            Opaque,
            Cutout,
            AlphaBlend,
            AlphaPreMul,
            Add
        }

        public enum RenderQueue
        {
            PreTransparent,
            Transparent,
            PreOpaque,
            Opaque,
            PostOpaque
        }

        [Flags]
        public enum DepthMode
        {
            None,
            ZTest = 1 << 0,
            ZWrite = 1 << 1,

            Opaque = ZTest | ZWrite,
        }
    }
}
