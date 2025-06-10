using O3DParse.Ini;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace O3DView;

public class VariableManager
{
    private readonly Dictionary<string, OSCVariable> variables;
    private readonly Dictionary<string, List<OSCVariable>> variablesCategorised;
    private readonly ReadOnlyDictionary<string, OSCVariable> variablesRO;
    private readonly ReadOnlyDictionary<string, List<OSCVariable>> variablesCategorisedRO;
    private readonly ArrayList<string> colourSchemes;
    private readonly List<CTIObject> ctis;
    //private readonly Dictionary<string, List<Material>> ctcControlledMaterials;
    //private readonly Dictionary<string, string> ctcTextures;
    private readonly object lockObj = new();
    private readonly string categorySpecial = "Special";
    private readonly string categoryFloat = "Float Vars";
    private readonly string categoryString = "String Vars";

    public ReadOnlyDictionary<string, OSCVariable> Variables => variablesRO;
    public ReadOnlyDictionary<string, List<OSCVariable>> VariablesCategorised => variablesCategorisedRO;
    public ArrayList<string> ColourSchemes => colourSchemes;

    public VariableManager()
    {
        variables = [];
        variablesRO = new(variables);
        variablesCategorised = [];
        variablesCategorisedRO = new(variablesCategorised);
        colourSchemes = [];
        colourSchemes.defaultValue = string.Empty;
        //ctcControlledMaterials = [];
        ctis = [];
    }

    public void Clear()
    {
        variables.Clear();
        variablesCategorised.Clear();
        //ctcControlledMaterials.Clear();
        colourSchemes.Clear();
        colourSchemes.Add("DEFAULT");
        ctis.Clear();
        TextureManager.UnReplaceTextures();
    }

    public void AddSceneObject(SceneObject sceneObject)
    {
        switch (sceneObject)
        {
            case CFGObject cfg:
                {
                    if (cfg.cfg.textTextures is TextTextureEnhCommand[] textTextures)
                    {
                        foreach (var textTexture in textTextures)
                            CreateStringVariable(textTexture.stringVar, categoryString, cfg, val => { });
                    }
                    if (cfg.cfg.lights is LightEnh2Command[] lights)
                    {
                        foreach (var light in lights)
                            CreateFloatVariable(light.brightnessVar, categoryFloat, cfg, val => { }, light.brightness);
                    }
                    if (cfg.cfg.ctc is CTCCommand ctc)
                    {
                        /*
                        Add a cti:
                        -> Register the cti scene object
                        -> Add the CTI's colourscheme names to the list
                        Colourscheme changes:
                        -> see which textures are affected by the cti 
                        -> cti tex name to path
                        -> TextureManager replace textures
                        -> update materials
                        -> set setvars
                         */

                        //ctc.textures[0].
                        CreateColourSchemeVariable(/*ctc.colourScheme*/"Colour Scheme", categorySpecial, cfg, val =>
                        {
                            foreach (var ctcTex in cfg.ctcTextureNames.Values)
                                TextureManager.ReplaceTexture(ctcTex.path, null);

                            foreach (var cti in ctis)
                            {
                                if (!cti.active)
                                    continue;
                                string scheme = colourSchemes[val];
                                if (cti.colourSchemes.TryGetValue(scheme, out var colourScheme))
                                {
                                    foreach (var ctiItem in colourScheme)
                                    {
                                        if (cfg.ctcTextureNames.TryGetValue(ctiItem.ctcTexture, out var origPath))
                                            TextureManager.ReplaceTexture(origPath.path, TextureManager.ResolveO3DTexPath(cti.basePath, ctiItem.path));

                                        // Apply setvars
                                        if (ctiItem.setVars != null)
                                        {
                                            foreach (var setvar in ctiItem.setVars)
                                            {
                                                if (variables.TryGetValue(setvar.var, out var variable) && variable is OSCFloatVariable floatVar)
                                                    floatVar.Value = setvar.value;
                                            }
                                        }
                                    }
                                }
                            }

                            foreach (var ctcMat in cfg.ctcControlledMaterials)
                                ctcMat.UpdateTextures();
                        }, 0);
                        cfg.ctcControlledMaterials.Clear();
                        if (ctc.textures != null)
                        {
                            foreach (var tex in ctc.textures)
                            {
                                // Search for materials controlled by this ctc texture
                                FindCTCMaterials(tex.path, cfg, cfg.ctcControlledMaterials);
                            }
                        }
                    }
                    if (cfg.cfg.interiorLights is InteriorLightCommand[] interiorLights)
                    {
                        var lightObjs = cfg.children?.Where(x => x is Light).Select(x => (Light)x).ToArray() ?? [];
                        int len = Math.Min(lightObjs.Length, interiorLights.Length);
                        for (int i = 0; i < len; i++)
                        {
                            InteriorLightCommand? interiorLight = interiorLights[i];
                            var lightObj = lightObjs[i];
                            CreateFloatVariable(interiorLight.var, categoryFloat, lightObj, val => lightObj.intensity = val, lightObj.intensity);
                        }
                    }
                    //cfg.cfg.detailFactor.Value.detailFactor
                    //cfg.cfg.noDistanceCheck
                    //cfg.cfg.texDetailFactor
                    /*if (cfg.cfg.setVars is SetVarCommand[] setvars)
                    {
                        foreach (var setVar in setvars)
                            CreateFloatVariable(setVar.var, categoryFloat, cfg, null, setVar.value);
                    }*/
                    break;
                }
            case LODObject lod:
                {
                    CreateFloatVariable("LOD View Radius", categorySpecial, lod, val =>
                    {
                        //foreach (var child in cfg.children.Where(x => x is CFGObject))
                        if (val >= lod.lodCommand.viewRadius)
                            lod.visible = true;
                        else
                            lod.visible = false;
                    }, 1, 0, 1);
                }
                break;
            case O3DMesh o3d:
                if (o3d.CFGMesh is MeshCommand cfgMesh)
                {
                    if (cfgMesh.viewpoint is ViewpointCommand viewpoint)
                    {
                        CreateViewpointVariable("viewpoint", categorySpecial, o3d, val =>
                        {
                            int ival = (1 << val) >> 1;
                            int viewPointVal = (int)viewpoint.viewpoint;

                            o3d.visible = viewPointVal == 0 || ival == 0 || (ival & viewPointVal) != 0;
                        });
                    }
                    if (cfgMesh.visibles is VisibleCommand[] visibles)
                    {
                        foreach (var visible in visibles)
                            CreateFloatVariable(visible.scriptVar, categoryFloat, o3d, val => o3d.visible = val == visible.value, 0, 0, 1);
                    }
                    if (cfgMesh.lights is LightEnh2Command[] lights)
                    {
                        foreach (var light in lights)
                        {
                            CreateFloatVariable(light.brightnessVar, categoryFloat, o3d,
                                val => light.brightness = val, light.brightness);
                        }
                    }
                    //o3d.CFGMesh.Value.anims[0].animTrans.Value.animVar;
                    //o3d.CFGMesh.Value.anims[0].rot.Value.animVar;
                    //o3d.CFGMesh.Value.bones
                    if (cfgMesh.matls is MatlCommand[] matls)
                    {
                        foreach (var matl in matls)
                        {
                            if (o3d.submeshes.FirstOrDefault(x => x.mat.MatlCommand == matl).mat is not Material renderMat)
                                continue;

                            if (matl.alphascale is AlphascaleCommand alphaScale)
                            {
                                CreateFloatVariable(alphaScale.var, categoryFloat, o3d, val => renderMat.alphaScale = val, 1, 0, 1);
                            }
                            if (matl.freetexes is MatlFreetexCommand[] freetexes)
                            {
                                foreach (var freetex in freetexes)
                                    CreateStringVariable(freetex.varName, categoryString, o3d, val =>
                                    {
                                        TextureManager.ReplaceTexture(freetex.path, val);
                                        renderMat.UpdateTextures();
                                    }, freetex.path);
                            }
                            if (matl.lightmap is MatlLightmapCommand lightmap && lightmap.var != null)
                            {
                                CreateFloatVariable(lightmap.var, categoryFloat, o3d, val => renderMat.lightMapStrength = val, 0, 0, 1);
                            }
                            if (matl.texcoordTransX is MatlTexCoordTransXCommand texcoordTransX)
                                CreateFloatVariable(texcoordTransX.var, categoryFloat, o3d, val => { });
                            if (matl.texcoordTransY is MatlTexCoordTransYCommand texcoordTransY)
                                CreateFloatVariable(texcoordTransY.var, categoryFloat, o3d, val => { });
                        }
                    }
                    /*if (cfgMesh.matlChanges is MatlChangeCommand[] matlChanges)
                    {
                        foreach (var matl in matlChanges)
                        {
                            if (o3d.submeshes.FirstOrDefault(x => x.mat.MatlCommand == matl).mat is not Material renderMat)
                                continue;

                            if (matl.alphascale is AlphascaleCommand alphaScale)
                            {
                                CreateFloatVariable(alphaScale.var, categoryFloat, o3d, val => renderMat.alphaScale = val, 1);
                            }
                            if (matl.freetexes is MatlFreetexCommand[] freetexes)
                            {
                                foreach (var freetex in freetexes)
                                    CreateStringVariable(freetex.varName, categoryString, o3d, renderMat.UpdateTextureFromPath, freetex.path);
                            }
                            if (matl.lightmap is MatlLightmapCommand lightmap && lightmap.var != null)
                            {
                                CreateFloatVariable(lightmap.var, categoryFloat, o3d, val => renderMat.lightMapStrength = val, 0);
                            }
                            if (matl.texcoordTransX is MatlTexCoordTransXCommand texcoordTransX)
                                CreateFloatVariable(texcoordTransX.var, categoryFloat, o3d, val => { });
                            if (matl.texcoordTransY is MatlTexCoordTransYCommand texcoordTransY)
                                CreateFloatVariable(texcoordTransY.var, categoryFloat, o3d, val => { });
                        }
                    }*/
                    //o3d.CFGMesh.Value.matlChanges[0].var
                    //o3d.CFGMesh.Value.matlChanges[0].items[0].alphascale
                    //o3d.CFGMesh.Value.matlChanges[0].items[0].raindropMap.Value.var
                    //o3d.CFGMesh.Value.matlChanges[0].items[0].freetexes[0].varName
                    //o3d.CFGMesh.Value.matlChanges[0].items[0].lightmap.Value.var
                    //o3d.CFGMesh.Value.matlChanges[0].items[0].texcoordTransX.Value.var
                    //o3d.CFGMesh.Value.matlChanges[0].items[0].texcoordTransY.Value.var
                    //o3d.CFGMesh.Value.matlChanges[0].items[0].useTextTexture.Value.var
                    /*if (cfgMesh.texChanges is TexChangesCommand texChanges)
                    {
                        texChanges.
                    }*/
                }
                break;
            case CTIObject cti:
                ctis.Add(cti);
                cti.UpdateColourSchemes();
                colourSchemes.AddRange(cti.colourSchemes.Keys);
                break;
            default:
                break;
        }

        if (sceneObject.children == null)
            return;

        foreach (var child in sceneObject.children)
        {
            AddSceneObject(child);
        }
    }

    private void FindCTCMaterials(string texture, SceneObject cfg, List<Material> destination)
    {
        // TODO: Should we handle matl_changes?
        //var matls = cfg.cfg.meshes?.SelectMany(x => x.matls ?? []) ?? [];
        //matls = matls.Concat(cfg.cfg?.lods?.SelectMany(x => x.meshes?.SelectMany(x => x.matls ?? []) ?? []) ?? []);
        //if (cfg.children == null)
        //    return;

        if (cfg is not O3DMesh mesh)
        {
            cfg.children?.ForEach(x => FindCTCMaterials(texture, x, destination));
            return;
        }

        var matls = mesh.submeshes.Select(x => x.mat);

        foreach (var mat in matls)
        {
            var matlCommand = mat.MatlCommand;
            if (matlCommand == null)
                continue;

            bool found = false;
            if (matlCommand.path == texture)
                found = true;
            else if (matlCommand.bumpmap?.path == texture)
                found = true;
            else if (matlCommand.envmap?.path == texture)
                found = true;
            else if (matlCommand.envmapMask?.path == texture)
                found = true;
            else if (matlCommand.lightmap?.path == texture)
                found = true;
            else if (matlCommand.nightmap?.path == texture)
                found = true;
            else if (matlCommand.transmap?.path == texture)
                found = true;

            if (found)
                destination.Add(mat);
        }
    }

    /*public void RemoveObject(SceneObject sceneObject)
    {
        List<OSCVariable> toRemove = [];
        foreach (var var in variables.Values.Where(x => x.Targets.Contains(sceneObject)))
        {
            var.Targets
        }

        if (sceneObject.children == null)
            return;

        foreach (var child in sceneObject.children)
        {
            RemoveObject(child);
        }
    }*/

    public void CreateFloatVariable(string name, string category, SceneObject target, Action<float>? onChanged,
        float defaultValue = default, float? min = null, float? max = null)
    {
        lock (lockObj)
        {
            if (!variables.TryGetValue(name, out var variable))
            {
                variable = new OSCFloatVariable(name, defaultValue);
                variables.TryAdd(name, variable);
                if (variablesCategorised.TryGetValue(category, out var categorised))
                    categorised.Add(variable);
                else
                    variablesCategorised.Add(category, [variable]);
            }
            if (variable is OSCFloatVariable floatVar)
            {
                if (onChanged != null)
                    floatVar.Subscribe(target, onChanged);

                floatVar.Value = defaultValue;
                if (min.HasValue)
                    floatVar.min = Math.Min(min.Value, floatVar.min);
                if (max.HasValue)
                    floatVar.max = Math.Max(max.Value, floatVar.max);
            }
            else
            {
                // Bad
            }
        }
    }

    public void CreateStringVariable(string name, string category, SceneObject target, Action<string> onChanged, string defaultValue = "")
    {
        lock (lockObj)
        {
            if (!variables.TryGetValue(name, out var variable))
            {
                variable = new OSCStringVariable(name, defaultValue);
                variables.TryAdd(name, variable);
                if (variablesCategorised.TryGetValue(category, out var categorised))
                    categorised.Add(variable);
                else
                    variablesCategorised.Add(category, [variable]);
            }
            if (variable is OSCStringVariable strVar)
                strVar.Subscribe(target, onChanged);
            else
            {
                // Bad
            }
        }
    }

    public void CreateViewpointVariable(string name, string category, SceneObject target, Action<int> onChanged, OmsiViewpoint defaultValue = OmsiViewpoint.Always)
    {
        lock (lockObj)
        {
            if (!variables.TryGetValue(name, out var variable))
            {
                variable = new OSCViewpointVariable(name, defaultValue);
                variables.TryAdd(name, variable);
                if (variablesCategorised.TryGetValue(category, out var categorised))
                    categorised.Add(variable);
                else
                    variablesCategorised.Add(category, [variable]);
            }
            if (variable is OSCViewpointVariable strVar)
                strVar.Subscribe(target, onChanged);
            else
            {
                // Bad
            }
        }
    }

    public void CreateColourSchemeVariable(string name, string category, SceneObject target, Action<int> onChanged, int defaultValue)
    {
        lock (lockObj)
        {
            if (!variables.TryGetValue(name, out var variable))
            {
                variable = new OSCColourSchemeVariable(name, defaultValue, colourSchemes);
                variables.TryAdd(name, variable);
                if (variablesCategorised.TryGetValue(category, out var categorised))
                    categorised.Add(variable);
                else
                    variablesCategorised.Add(category, [variable]);
            }
            if (variable is OSCColourSchemeVariable strVar)
                strVar.Subscribe(target, onChanged);
            else
            {
                // Bad
            }
        }
    }
}

public abstract class OSCVariable
{
    protected readonly List<SceneObject> targets;
    private readonly ReadOnlyCollection<SceneObject> targetsRO;

    public readonly string name;

    public ReadOnlyCollection<SceneObject> Targets => targetsRO;

    public OSCVariable(string name)
    {
        this.name = name;
        targets = [];
        targetsRO = new(targets);
    }
}

public class OSCFloatVariable(string name, float defaultValue = 0) : OSCGenericVariable<float>(name, defaultValue)
{
    public float min;
    public float max;
}

public class OSCStringVariable(string name, string defaultValue = "") : OSCGenericVariable<string>(name, defaultValue)
{ }

public class OSCViewpointVariable : OSCGenericVariable<int>
{
    private static string[]? viewPointNames;
    public static string[] ViewPointNames
    {
        get
        {
            if (viewPointNames != null)
                return viewPointNames;

            viewPointNames = Enum.GetNames<OmsiViewpoint>();
            return viewPointNames;
        }
    }

    public OSCViewpointVariable(string name, OmsiViewpoint defaultValue = OmsiViewpoint.Always) : base(name, (int)defaultValue)
    {
    }
}

public class OSCColourSchemeVariable : OSCGenericVariable<int>
{
    private readonly ArrayList<string> colourSchemes;
    public ArrayList<string> ColourSchemes => colourSchemes;

    public OSCColourSchemeVariable(string name, int defaultValue, ArrayList<string> colourSchemes) : base(name, defaultValue)
    {
        this.colourSchemes = colourSchemes;
    }
}

public class OSCGenericVariable<T> : OSCVariable
    where T : IEquatable<T>
{
    private T value;
    private event Action<T>? OnChange;

    public T Value
    {
        get => value;
        set
        {
            var prev = this.value;
            this.value = value;
            if (!prev.Equals(value))
                OnChange?.Invoke(value);
        }
    }

    public OSCGenericVariable(string name, T defaultValue) : base(name)
    {
        this.value = defaultValue;
    }

    public void Subscribe(SceneObject target, Action<T> onChange)
    {
        OnChange += onChange;
        targets.Add(target);
    }

    public void Unsubscribe(SceneObject target, Action<T> onChange)
    {
        OnChange -= onChange;
        targets.Remove(target);
    }
}
