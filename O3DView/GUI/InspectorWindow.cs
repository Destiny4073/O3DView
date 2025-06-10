using ImGuiNET;
using O3DParse.Ini;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static O3DView.GUI.CFGEditor;
using O3DView.Gizmos;

namespace O3DView.GUI;

public class InspectorWindow : IGUIWindow
{
    private readonly GUIController controller;
    private readonly Program program;

    private WindowSnapEdge windowSnap;
    private readonly string[] blendModes;
    private readonly string[] depthModes;
    private readonly string[] lightModes;

    private readonly Dictionary<Type, CFGFieldCache> cfgFieldCache = [];

    public static IGUIWindow Construct(GUIController guiController, Program program)
    {
        var ret = new InspectorWindow(guiController, program);

        guiController.RegisterWindow(ret);

        return ret;
    }

    private InspectorWindow(GUIController controller, Program program)
    {
        this.controller = controller;
        this.program = program;

        blendModes = Enum.GetNames(typeof(Material.BlendMode));
        depthModes = Enum.GetNames(typeof(Material.DepthMode));
        lightModes = Enum.GetNames(typeof(LightMode));
    }

    public bool DrawWindow()
    {
        ImGui.Begin("Inspector");
        ImGui.SetWindowSize(new Vector2(280, 320), ImGuiCond.Once);
        ImGui.SetWindowPos(new Vector2(1, 50), ImGuiCond.Once);
        controller.SnapWindowToEdge(ref windowSnap);

        ImGui.TextColored(new Vector4(0.8f, 0.8f, 1, 1), $"Selected: {program.SelectedObject?.name ?? "NONE"}");
        if (program.SelectedObject is SceneObject obj)
        {
            ImGui.InputText("Name", ref obj.name, 255);

            if (ImGui.Button(obj.Parent?.name ?? "NONE"))
            {
                if (obj.Parent != null)
                    program.Select(obj.Parent);
            }
            ImGui.SameLine();
            //ImGui.LabelText("Parent", "");
            ImGui.Text("Parent");
        }
        ImGui.Spacing();

        if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (program.SelectedObject != null)
            {
                ref var xform = ref program.SelectedObject.transform;
                var p = xform.Pos;
                if (ImGui.DragFloat3("Position", ref p, 0.1f))
                    xform.Pos = p;
                var r = xform.EulerRot * (180 / MathF.PI);
                if (ImGui.DragFloat3("Rotation", ref r, 0.5f))
                    xform.EulerRot = r * (MathF.PI / 180);
                //var q = (xform.Rot * (180 / MathF.PI));
                //var qv = new Vector4(q.X, q.Y, q.Z, q.W);
                //if (ImGui.DragFloat4("RotationQ", ref qv, 0.5f))
                //   xform.Rot = new Quaternion(qv.X, qv.Y, qv.Z, qv.W) * (MathF.PI / 180);
                var s = xform.Scale;
                if (ImGui.DragFloat("Scale", ref s, 0.1f))
                    xform.Scale = s;
            }
        }

        switch (program.SelectedObject)
        {
            case O3DMesh mesh:
                {
                    var o3d = mesh.O3D;
                    if (ImGui.CollapsingHeader("Loaded Model Properties", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        ImGui.Text("File:");
                        ImGui.Indent();
                        ImGui.TextWrapped(Path.GetFileName(mesh.O3DPath));
                        ImGui.Unindent();

                        ImGui.Text($"Version: {o3d.version}");
                        ImGui.Text($"Encryption key: 0x{o3d.encryptionKey:X8}");
                        ImGui.Text($"Extended options: {o3d.extendedOptions}");
                        ImGui.Text($"Vertices: {o3d.vertices.Length}");
                        ImGui.Text($"Triangles: {o3d.triangles.Length}");
                        ImGui.Text($"Materials: {o3d.materials.Length}");
                        ImGui.Text($"Bones: {o3d.bones.Length}");
                    }

                    if (ImGui.CollapsingHeader("Materials", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        for (int m = 0; m < o3d.materials.Length; m++)
                        {
                            ref var mat = ref o3d.materials[m];
                            var matRender = mesh.submeshes[m].mat;
                            if (ImGui.TreeNode($"Material {m}: '{mat.textureName}'"))
                            {
                                Vector4 diff = mat.diffuse;
                                if (ImGui.ColorEdit4("Diffuse", ref diff))
                                {
                                    mat.diffuse = diff;
                                    matRender.UpdateFromO3D(mat, mesh.O3DPath);
                                }
                                Vector3 spec = mat.spec;
                                if (ImGui.ColorEdit3("Specular", ref spec))
                                {
                                    mat.spec = spec;
                                    matRender.UpdateFromO3D(mat, mesh.O3DPath);
                                }
                                float specPower = mat.specPower;
                                if (ImGui.SliderFloat("Spec Power", ref specPower, 0, 128, "%3f", ImGuiSliderFlags.Logarithmic))
                                {
                                    mat.specPower = specPower;
                                    matRender.UpdateFromO3D(mat, mesh.O3DPath);
                                }
                                Vector3 emit = mat.emission;
                                if (ImGui.ColorEdit3("Emission", ref emit))
                                {
                                    mat.emission = emit;
                                    matRender.UpdateFromO3D(mat, mesh.O3DPath);
                                }
                                if (!(matRender.texture?.IsValid ?? false))
                                {
                                    ImGui.TextColored(new(1, .3f, .3f, 1), "Not Loaded!");
                                    ImGui.SameLine();
                                }
                                string tex = mat.textureName;
                                if (ImGui.InputText("Texture", ref tex, 254))
                                {
                                    mat.textureName = tex;
                                    matRender.UpdateFromO3D(mat, mesh.O3DPath);
                                }
                                ImGui.Text("Non-O3D Parameters:");
                                var blendMode = (int)matRender.blendMode;
                                if (ImGui.Combo("Blend Mode", ref blendMode, blendModes, blendModes.Length))
                                    matRender.blendMode = (Material.BlendMode)blendMode;
                                var depthMode = (int)matRender.depthMode;
                                if (ImGui.CheckboxFlags("ZTest", ref depthMode, (int)Material.DepthMode.ZTest))
                                    matRender.depthMode = (Material.DepthMode)depthMode;
                                if (ImGui.CheckboxFlags("ZWrite", ref depthMode, (int)Material.DepthMode.ZWrite))
                                    matRender.depthMode = (Material.DepthMode)depthMode;
                                ImGui.SliderFloat("Envmap Strength", ref matRender.envMapStrength, 0, 1, "%3f");
                                ImGui.SliderFloat("Bumpmap Strength", ref matRender.bumpMapStrength, 0, 1, "%3f");
                                //ImGui.BeginDisabled();
                                ImGui.Text($"Transmap: {(matRender.transMap is O3DTexture transMap ? transMap.FileName : "NONE")}");
                                ImGui.Text($"Bumpmap: {(matRender.bumpMap is O3DTexture bumpMap ? bumpMap.FileName : "NONE")}");
                                ImGui.Text($"Envmap: {(matRender.envMap is O3DTexture envMap ? envMap.FileName : "NONE")}");
                                ImGui.Text($"Lightmap: {(matRender.lightMap is O3DTexture lightMap ? lightMap.FileName : "NONE")}");
                                ImGui.Text($"Nightmap: {(matRender.nightMap is O3DTexture nightMap ? nightMap.FileName : "NONE")}");
                                //ImGui.EndDisabled();
                                ImGui.TreePop();
                            }
                        }
                    }

                    if (mesh.CFGMesh != null)
                    {
                        if (ImGui.CollapsingHeader("CFG Properties", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            var meshType = typeof(MeshCommand);
                            if (!cfgFieldCache.TryGetValue(meshType, out var cfgCache))
                            {
                                cfgCache = BuildCFGFieldCache(meshType);
                                cfgFieldCache.Add(meshType, cfgCache);
                            }

                            DrawCFGFieldCache(mesh.CFGMesh, cfgCache);
                        }
                    }

                    break;
                }

            case Light light:
                if (ImGui.CollapsingHeader("Light Settings", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Checkbox("On", ref light.on);
                    int lightMode = (int)light.mode;
                    if (ImGui.Combo("Type", ref lightMode, lightModes, lightModes.Length))
                        light.mode = (LightMode)lightMode;
                    ImGui.SliderFloat("Intensity", ref light.intensity, 0, 10, "%3f", ImGuiSliderFlags.Logarithmic);
                    ImGui.ColorEdit3("Colour", ref light.colour);
                    ImGui.ColorEdit3("Ambient", ref light.ambient);
                    ImGui.SliderFloat("Range", ref light.range, 0, 100, "%3f", ImGuiSliderFlags.Logarithmic);
                }
                break;

            case Camera camera:
                if (ImGui.CollapsingHeader("Camera Settings", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.SliderFloat("Move Speed", ref camera.moveSpeed, 0, 10, "%3f", ImGuiSliderFlags.Logarithmic);
                    ImGui.SliderFloat("Orbit Speed", ref camera.orbitSpeed, 0, 10, "%3f", ImGuiSliderFlags.Logarithmic);
                    ImGui.SliderFloat("Pan Speed", ref camera.panSpeed, 0, 10, "%3f", ImGuiSliderFlags.Logarithmic);
                    ImGui.SliderFloat("Zoom Speed", ref camera.zoomSpeed, 0, 10, "%3f", ImGuiSliderFlags.Logarithmic);
                }
                break;

            case CFGObject cfgObj:
                if (ImGui.CollapsingHeader("CFG Properties", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    var type = cfgObj.cfg.GetType();
                    if (!cfgFieldCache.TryGetValue(type, out var cache))
                    {
                        cache = BuildCFGFieldCache(type);
                        cfgFieldCache.Add(type, cache);
                    }

                    DrawCFGFieldCache(cfgObj.cfg, cache);
                }
                break;

            case MapTileObject mapTileObj:
                if (ImGui.CollapsingHeader("Map Tile Properties", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    var type = mapTileObj.map.GetType();
                    if (!cfgFieldCache.TryGetValue(type, out var cache))
                    {
                        cache = BuildCFGFieldCache(type);
                        cfgFieldCache.Add(type, cache);
                    }

                    DrawCFGFieldCache(mapTileObj.map, cache);
                }
                break;

            case PathMesh pathMesh:
                if (ImGui.CollapsingHeader("Spline Properties", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    var type = typeof(PathCommand2);
                    if (!cfgFieldCache.TryGetValue(type, out var cache))
                    {
                        cache = BuildCFGFieldCache(type);
                        cfgFieldCache.Add(type, cache);
                    }

                    ref var data = ref pathMesh.PathData;
                    if (Unsafe.IsNullRef(ref data))
                        break;

                    var ret = DrawCFGFieldCache(data, cache);
                    if (ret.ret != null)
                    {
                        data = (PathCommand2)ret.ret;
                        pathMesh.Update(false);
                    }
                    if (ret.deleted)
                        pathMesh.MapObj.paths = [..pathMesh.MapObj.paths!.TakeWhile((x, i) => i != pathMesh.PathIndex)];
                }
                break;

            case GenericSplineMesh splineMesh:
                if (ImGui.CollapsingHeader("Spline Properties", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    var type = typeof(GenericSplineCommand);
                    if (!cfgFieldCache.TryGetValue(type, out var cache))
                    {
                        cache = BuildCFGFieldCache(type);
                        cfgFieldCache.Add(type, cache);
                    }

                    var data = splineMesh.SplineData;

                    var ret = DrawCFGFieldCache(data, cache);
                    if (ret.ret != null)
                    {
                        data = (GenericSplineCommand)ret.ret;
                        splineMesh.Update(false);
                    }
                    // TODO:
                    //if (ret.deleted)
                    //    splineMesh.MapObj.paths = [.. pathMesh.MapObj.paths!.TakeWhile((x, i) => i != pathMesh.PathIndex)];
                }

                if (ImGui.CollapsingHeader("Spline Profile", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    var type = typeof(OmsiSpline);
                    if (!cfgFieldCache.TryGetValue(type, out var cache))
                    {
                        cache = BuildCFGFieldCache(type);
                        cfgFieldCache.Add(type, cache);
                    }

                    var data = splineMesh.Profile;

                    var ret = DrawCFGFieldCache(data, cache);
                    if (ret.ret != null)
                    {
                        //data = (OmsiSpline)ret.ret;
                        splineMesh.Update(false);
                    }
                }
                break;

            case CabinPathGizmo cabinPath:
                if (ImGui.CollapsingHeader("Cabin Paths", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    var type = typeof(OmsiPaths);
                    if (!cfgFieldCache.TryGetValue(type, out var cache))
                    {
                        cache = BuildCFGFieldCache(type);
                        cfgFieldCache.Add(type, cache);
                    }

                    var ret = DrawCFGFieldCache(cabinPath.Paths, cache);
                    if (ret.ret != null)
                        cabinPath.Update();
                }
                break;

            /*case SDFObject sdf:
                sdf.DrawInspector();
                break;*/

            case LODObject lod:
                if (ImGui.CollapsingHeader("LOD Properties", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    var meshType = typeof(LODCommand);
                    if (!cfgFieldCache.TryGetValue(meshType, out var cfgCache))
                    {
                        cfgCache = BuildCFGFieldCache(meshType);
                        cfgFieldCache.Add(meshType, cfgCache);
                    }

                    var ret = DrawCFGFieldCache(lod.lodCommand, cfgCache);

                    if (ret.ret != null)
                        lod.OnModify(ret.ret, ret.deleted);
                }
                break;

            case PseudoObject pseudoObject:
                if (pseudoObject.metadata != null)
                {
                    if (ImGui.CollapsingHeader("CFG Properties", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        var meshType = pseudoObject.metadata.GetType();
                        if (!cfgFieldCache.TryGetValue(meshType, out var cfgCache))
                        {
                            cfgCache = BuildCFGFieldCache(meshType);
                            cfgFieldCache.Add(meshType, cfgCache);
                        }

                        var ret = DrawCFGFieldCache(pseudoObject.metadata, cfgCache);

                        if (ret.ret != null)
                            pseudoObject.OnModify(ret.ret, ret.deleted);
                    }
                }
                break;

            case IBillboardGizmo billboardGizmo:
                if (billboardGizmo.Metadata != null)
                {
                    if (ImGui.CollapsingHeader("CFG Properties", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        var meshType = billboardGizmo.Metadata.GetType();
                        if (!cfgFieldCache.TryGetValue(meshType, out var cfgCache))
                        {
                            cfgCache = BuildCFGFieldCache(meshType);
                            cfgFieldCache.Add(meshType, cfgCache);
                        }

                        var ret = DrawCFGFieldCache(billboardGizmo.Metadata, cfgCache);

                        if (ret.ret != null)
                            billboardGizmo.OnModify(ret.ret, ret.deleted);
                    }
                }
                break;
        }
        ImGui.End();
        return true;
    }
}
