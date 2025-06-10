using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace O3DParse.Ini;

// With thanks to https://roadhog123.co.uk/omsi-wiki/quick-reference/keywords
// for help with some of the keyword definitions
[Serializable]
[OmsiIniCommand("model")]
public class OmsiCFGFile
{
    [OmsiIniCommandFile] public string? path;

    public VFDCommand? vfd;
    public DetailFactorCommand? detailFactor;
    public TexDetailFactorCommand? texDetailFactor;
    public NoDistanceCheckCommand? noDistanceCheck;
    public TerrainHoleCommand? terrainHole;
    public ItemCommand[]? items; // Not sure if these are valid
    //public SetVarCommand[]? setVars; // Not sure if these are valid
    public CTCCommand? ctc;
    public TextTextureEnhCommand[]? textTextures;
    //public TextTextureEnhCommand[]? textTexturesEnh;
    public ScriptTextureCommand[]? scriptTextures;
    public LODCommand[]? lods;

    public MeshCommand[]? meshes; // Normally meshes belong to a LOD, but it's possible to define a model without lods
    public InteriorLightCommand[]? interiorLights;
    public SpotLightCommand[]? spotLights;
    public LightEnh2Command[]? lights;          // Technically [light]s belong to a [mesh], but it's not uncommon for authors to place 
    //public LightEnhCommand[]? lightEnhs;    // all their lights after all the meshes (and spotlights), omsi doesn't care because it 
    //public LightEnh2Command[]? lightEnh2s;  // just adds them to the last mesh, but our parser can't do that, so we put them here...

    [OmsiIniComments] public string? comments;
    [OmsiIniComments(true)] public string? postComments;
}

public struct RGBColour
{
    public byte r;
    public byte g;
    public byte b;

    public readonly Vector3 ToVector3 => new Vector3(r, g, b) * (1 / 255f);
    public RGBColour() { }
    public RGBColour(Vector3 vec)
    {
        r = (byte)(vec.X * 255);
        g = (byte)(vec.Y * 255);
        b = (byte)(vec.Z * 255);
    }
}

public struct RGBAColour
{
    public byte r;
    public byte g;
    public byte b;
    public byte a;

    public readonly Vector4 ToVector4 => new Vector4(r, g, b, a) * (1 / 255f);
    public RGBAColour() { }
    public RGBAColour(Vector4 vec)
    {
        r = (byte)(vec.X * 255);
        g = (byte)(vec.Y * 255);
        b = (byte)(vec.Z * 255);
        a = (byte)(vec.W * 255);
    }
}

public struct BGRAColour
{
    public byte b;
    public byte g;
    public byte r;
    public byte a;

    /// <summary>
    /// Returns an RGBA Vector4 in the range 0-1
    /// </summary>
    public readonly Vector4 ToVector4 => new Vector4(r, g, b, a) * (1 / 255f);

    public BGRAColour() { }
    public BGRAColour(Vector4 vec)
    {
        r = (byte)(vec.X * 255);
        g = (byte)(vec.Y * 255);
        b = (byte)(vec.Z * 255);
        a = (byte)(vec.W * 255);
    }
}

[OmsiIniCommand("VFDmaxmin")]
public class VFDCommand
{
    public Vector3 min;
    public Vector3 max;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("detail_factor")]
public class DetailFactorCommand
{
    public float detailFactor;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("tex_detail_factor")]
public class TexDetailFactorCommand
{
    public float detailFactor;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("noDistanceCheck")]
public class NoDistanceCheckCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("terrainhole")]
public class TerrainHoleCommand
{
    public string path = string.Empty;

    [OmsiIniComments] public string? comments;
}

#region CTI Commands
[OmsiIniCommand("item")]
public class ItemCommand
{
    public string name = string.Empty;
    public string ctcTexture = string.Empty;
    public string path = string.Empty;

    public SetVarCommand[]? setVars;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("setvar")]
public class SetVarCommand
{
    public string var = string.Empty;
    public float value;

    [OmsiIniComments] public string? comments;
}
#endregion

[OmsiIniCommand("CTC")]
public class CTCCommand
{
    public string colourScheme = string.Empty;
    public string path = string.Empty;
    public string unk = string.Empty;

    [OmsiIniComments] public string? comments;
    public CTCTextureCommand[]? textures;
}

[OmsiIniCommand("CTCTexture")]
public class CTCTextureCommand
{
    public string name = string.Empty;
    public string path = string.Empty;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("texttexture")]
public class TextTextureCommand
{
    public string stringVar = string.Empty;
    public string fontName = string.Empty;
    public uint width;
    public uint height;
    public bool fullcolour;
    public RGBColour colour;

    [OmsiIniComments] public string? comments;
}

public enum TextAlign : int
{
    Centred,
    Left,
    Right,
    Justify,
    JustifyLeft,
    JustifyRight
}

[OmsiIniCommand("texttexture_enh")]
[OmsiIniDerivedCommand<TextTextureCommand>]
public class TextTextureEnhCommand
{
    public string stringVar = string.Empty;
    public string fontName = string.Empty;
    public uint width;
    public uint height;
    public bool fullcolour;
    public RGBColour colour;
    public TextAlign textAlign = TextAlign.Left;
    public bool gridAlign = true;

    [OmsiIniComments] public string? comments;

    public TextTextureEnhCommand() { }
}

[OmsiIniCommand("scripttexture")]
public class ScriptTextureCommand
{
    public uint width, height;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("LOD")]
public class LODCommand
{
    public float viewRadius;

    [OmsiIniComments] public string? comments;
    public MeshCommand[]? meshes;
    // TODO: Do interior lights belong to a LOD?
    //public InteriorLightCommand[]? interiorLights;
    //public SpotLightCommand[]? spotLights;
}

[OmsiIniCommand("interiorlight")]
public class InteriorLightCommand
{
    public string var = string.Empty;
    public float range;
    public RGBColour colour;
    public Vector3 pos;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("spotlight")]
public class SpotLightCommand
{
    public Vector3 pos;
    public Vector3 direction;
    public Vector3 colour;
    public float range;
    public float innerRadius;
    public float outerRadius;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("mesh")]
public class MeshCommand
{
    public string path = string.Empty;

    public MeshIdentCommand? meshIdent;
    public VisibleCommand[]? visibles;
    public IlluminationInteriorCommand? illuminationInterior;
    public MatlCommand[]? matls;
    public MatlChangeCommand[]? matlChanges;
    public AnimParentCommand? animParent;
    public IsShadowCommand? isShadow;
    public ShadowCommand? shadow;
    public MouseEventCommand? mouseEvent;
    public NewAnimCommand[]? anims;
    public ViewpointCommand? viewpoint;
    public SetBoneCommand[]? bones;
    public SmoothSkinCommand? smoothSkin;
    public IlluminationCommand? illumination;
    public LightEnh2Command[]? lights;
    //public LightEnhCommand[]? lightEnhs;
    //public LightEnh2Command[]? lightEnh2s;
    public TexChangesCommand? texChanges;
    public SmokeCommand[]? smokes;
    public ParticleEmitterCommand[]? particleEmitters;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("visible")]
public class VisibleCommand
{
    public string scriptVar = string.Empty;
    public float value;

    [OmsiIniComments] public string? comments;
}

[Flags]
public enum OmsiViewpoint
{
    Always,
    Exterior = 1 << 0,
    Interior = 1 << 1,
    AI = 1 << 2,
}

[OmsiIniCommand("viewpoint")]
public class ViewpointCommand
{
    public OmsiViewpoint viewpoint;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("mesh_ident")]
public class MeshIdentCommand
{
    public string name = string.Empty;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("animparent")]
public class AnimParentCommand
{
    public string parentName = string.Empty;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("mouseevent")]
public class MouseEventCommand
{
    public string triggerName = string.Empty;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("isshadow")]
public class IsShadowCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("shadow")]
public class ShadowCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("setbone")]
public class SetBoneCommand
{
    public string name = string.Empty;
    public int value;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("smoothskin")]
public class SmoothSkinCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("illumination")]
public class IlluminationCommand
{
    public int value;

    [OmsiIniComments] public string? comments;
}

#region Anim
[OmsiIniCommand("newanim")]
public class NewAnimCommand
{
    public AnimOriginFromMeshCommand? originFromMesh;
    public AnimOriginTransCommand? originTrans;
    public AnimOriginRotXCommand? originRotX;
    public AnimOriginRotYCommand? originRotY;
    public AnimOriginRotZCommand? originRotZ;
    public AnimTransCommand? animTrans;
    public AnimRotCommand? rot;
    public AnimOffsetCommand? offset;
    public AnimMaxspeedCommand? maxspeed;
    public AnimDelayCommand? delay;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("origin_from_mesh")]
[OmsiIniVerbatimCommand]
public class AnimOriginFromMeshCommand { }

[OmsiIniCommand("origin_trans")]
[OmsiIniVerbatimCommand]
public class AnimOriginTransCommand
{
    public Vector3 origin;
}

[OmsiIniCommand("origin_rot_x")]
[OmsiIniVerbatimCommand]
public class AnimOriginRotXCommand
{
    public float rotX;
}

[OmsiIniCommand("origin_rot_y")]
[OmsiIniVerbatimCommand]
public class AnimOriginRotYCommand
{
    public float rotY;
}

[OmsiIniCommand("origin_rot_z")]
[OmsiIniVerbatimCommand]
public class AnimOriginRotZCommand
{
    public float rotZ;
}

[OmsiIniCommand("anim_trans")]
[OmsiIniVerbatimCommand]
public class AnimTransCommand
{
    public string animVar = string.Empty;
    public float delta;
}

[OmsiIniCommand("anim_rot")]
[OmsiIniVerbatimCommand]
public class AnimRotCommand
{
    public string animVar = string.Empty;
    public float delta;
}

[OmsiIniCommand("offset")]
[OmsiIniVerbatimCommand]
public class AnimOffsetCommand
{
    public float offset;
}

[OmsiIniCommand("maxspeed")]
[OmsiIniVerbatimCommand]
public class AnimMaxspeedCommand
{
    public float maxspeed;
}

[OmsiIniCommand("delay")]
[OmsiIniVerbatimCommand]
public class AnimDelayCommand
{
    public float delay;
}
#endregion

[Flags]
public enum OmsiLightEffect
{
    None,
    Star = 1 << 0,
    NoFog = 1 << 1,
    EffectOnly = 1 << 2,
}

[OmsiIniCommand("light")]
public class LightCommand
{
    public string brightnessVar = string.Empty;
    public float size;
    public RGBColour colour;
    public Vector3 position;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("light_enh")]
public class LightEnhCommand
{
    public Vector3 position;
    public RGBColour colour;
    public float size;
    public string brightnessVar = string.Empty;
    public float brightness;
    public float zOffset;
    public OmsiLightEffect effect;
    public float fadeTime;
    [OmsiIniOptional] public string? bitmap;

    [OmsiIniComments] public string? comments;
}

public enum OmsiLightRotationAxis
{
    DirectionVector,
    RotationAxis,
    Free
}

[OmsiIniCommand("light_enh_2")]
[OmsiIniDerivedCommand<LightEnhCommand>]
[OmsiIniDerivedCommand<LightCommand>]
public class LightEnh2Command
{
    public Vector3 position;
    public Vector3 direction;
    public Vector3 rotationAxis;
    public bool omnidirectional;
    public OmsiLightRotationAxis rotationMode;
    public RGBColour colour;
    public float size;
    public float innerConeAngle;
    public float outerConeAngle;
    public string brightnessVar = string.Empty;
    public float brightness;
    public float zOffset;
    public OmsiLightEffect effect;
    public bool coneEffect;
    public float fadeTime;
    [OmsiIniOptional] public string? bitmap;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("illumination_interior")]
public class IlluminationInteriorCommand
{
    public int lightA, lightB, lightC, lightD;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("texchanges")]
public class TexChangesCommand
{
    public string path = string.Empty;
    // TODO: Implement texchanges

    [OmsiIniComments] public string? comments;
}

#region Particle System Commands
[OmsiIniCommand("smoke")]
public class SmokeCommand
{
    public Vector3 pos;
    public Vector3 direction;
    // All of these can be either an OSC variable name, or a float
    public string? initialSpeed;
    public string? speedVariation;
    public string? emissionRate;
    public string? lifespan;
    public string? drag;
    public string? gravity;
    public string? initialSize;
    public string? growRate;
    public string? initialAlpha;
    public string? alphaVariation;
    public Vector3 colour;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("particle_emitter")]
public class ParticleEmitterCommand
{
    // TODO: Implement particle emitter, this command is quite complex.

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("PS_attachTo")]
public class PSAttachToCommand
{
    public int unk1;
    public int unk2;
    public int unk3;
    // TODO: Work out what these mean...

    [OmsiIniComments] public string? comments;
}

// TODO: There are a bunch of other PS "commands" such as:
// --PS_instExplosion_partcount--
// --PS_livetime--
// --PS_emitter_livetime--
// etc...
#endregion

#region Matl Commands
[OmsiIniCommand("matl")]
public class MatlCommand
{
    public string path = string.Empty;
    public uint? index;

    public AlphascaleCommand? alphascale;
    public AllcolorCommand? allcolor;
    public MatlAlphaCommand? alpha;
    public MatlBumpmapCommand? bumpmap;
    public MatlEnvmapCommand? envmap;
    public MatlEnvmapMaskCommand? envmapMask;
    public MatlFreetexCommand[]? freetexes;
    public MatlLightmapCommand? lightmap;
    public MatlNightmapCommand? nightmap;
    public MatlNoZWriteCommand? noZWrite;
    public MatlNoZCheckCommand? noZCheck;
    public MatlTexAdressBorderCommand? texadressborder;
    public MatlTexAdressClampCommand? texadressclamp;
    public MatlTexAdressMirrorCommand? texadressmirror;
    public MatlTexAdressMirrorOnceCommand? texadressmirroronce;
    public MatlTransmapCommand? transmap;
    public MatlTexCoordTransXCommand? texcoordTransX;
    public MatlTexCoordTransYCommand? texcoordTransY;
    public MatlUseScriptTextureCommand? useScriptTexture;
    public MatlUseTextTextureCommand? useTextTexture;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("matl_change")]
public class MatlChangeCommand
{
    public string path = string.Empty;
    public float value;
    public string var = string.Empty;

    public MatlItemCommand[]? items;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("matl_item")]
public class MatlItemCommand
{
    public AlphascaleCommand? alphascale;
    public AllcolorCommand? allcolor;
    public MatlAlphaCommand? alpha;
    public MatlBumpmapCommand? bumpmap;
    public MatlEnvmapCommand? envmap;
    public MatlEnvmapMaskCommand? envmapMask;
    public MatlFreetexCommand[]? freetexes;
    public MatlLightmapCommand? lightmap;
    public MatlNightmapCommand? nightmap;
    public MatlNoZWriteCommand? noZWrite;
    public MatlNoZCheckCommand? noZCheck;
    public MatlRaindropMap? raindropMap;
    public MatlTexAdressBorderCommand? texadressborder;
    public MatlTexAdressClampCommand? texadressclamp;
    public MatlTexAdressMirrorCommand? texadressmirror;
    public MatlTexAdressMirrorOnceCommand? texadressmirroronce;
    public MatlTransmapCommand? transmap;
    public MatlTexCoordTransXCommand? texcoordTransX;
    public MatlTexCoordTransYCommand? texcoordTransY;
    public MatlUseScriptTextureCommand? useScriptTexture;
    public MatlUseTextTextureCommand? useTextTexture;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("alphascale")]
public class AlphascaleCommand
{
    public string var = string.Empty;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("allcolor")]
public class AllcolorCommand
{
    public Vector4 diffuse;
    public Vector3 ambient;
    public Vector3 specular;
    public Vector3 emission;
    public float specPower;

    [OmsiIniComments] public string? comments;
}

public enum MatlAlphaMode
{
    Opaque,
    Cutout,
    Blend
}

[OmsiIniCommand("matl_alpha")]
public class MatlAlphaCommand
{
    public MatlAlphaMode alphaMode;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("matl_bumpmap")]
public class MatlBumpmapCommand
{
    public string path = string.Empty;
    public float strength;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("matl_envmap")]
public class MatlEnvmapCommand
{
    public string path = string.Empty;
    public float reflectivity;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("matl_envmap_mask")]
public class MatlEnvmapMaskCommand
{ 
    public string path = string.Empty;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("matl_freetex")]
public class MatlFreetexCommand
{
    public string path = string.Empty;
    public string varName = string.Empty;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("matl_lightmap")]
public class MatlLightmapCommand
{
    public string path = string.Empty;
    [OmsiIniOptional] public string? var;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("matl_nightmap")]
public class MatlNightmapCommand
{
    public string path = string.Empty;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("matl_noZwrite")]
public class MatlNoZWriteCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("matl_noZcheck")]
public class MatlNoZCheckCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("matl_raindropmap")]
public class MatlRaindropMap
{
    public string path = string.Empty;
    public string var = string.Empty;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("matl_texadress_border")]
public class MatlTexAdressBorderCommand
{
    public BGRAColour borderColour;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("matl_texadress_clamp")]
public class MatlTexAdressClampCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("matl_texadress_mirror")]
public class MatlTexAdressMirrorCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("matl_texadress_mirroronce")]
public class MatlTexAdressMirrorOnceCommand
{
    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("matl_transmap")]
public class MatlTransmapCommand
{
    public string path = string.Empty;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("texcoordtransX")]
public class MatlTexCoordTransXCommand
{
    public string var = string.Empty;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("texcoordtransY")]
public class MatlTexCoordTransYCommand
{
    public string var = string.Empty;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("useScriptTexture")]
public class MatlUseScriptTextureCommand
{
    public int index;

    [OmsiIniComments] public string? comments;
}

[OmsiIniCommand("useTextTexture")]
public class MatlUseTextTextureCommand
{
    public int index;

    [OmsiIniComments] public string? comments;
}
#endregion
