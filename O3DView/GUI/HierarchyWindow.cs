using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace O3DView.GUI;

public class HierarchyWindow : IGUIWindow
{
    private readonly GUIController controller;
    private readonly Program program;

    private WindowSnapEdge windowSnap;
    private string searchQuery;
    private Regex? searchRegex;

    public static IGUIWindow Construct(GUIController guiController, Program program)
    {
        var ret = new HierarchyWindow(guiController, program);

        guiController.RegisterWindow(ret);
        /*guiController.RegisterMenuItem(ret, new()
        {
            window = ret,
            toggle = true,
            category = GUIController.MenuItemCategory.Help,
            name = "About",
            shortcut = "",
            action = (open) => ret.isOpen = open,
        });*/

        return ret;
    }

    private HierarchyWindow(GUIController controller, Program program)
    {
        this.controller = controller;
        this.program = program;
        this.searchQuery = string.Empty;
    }

    public bool DrawWindow()
    {
        ImGui.Begin("Hierarchy");
        ImGui.SetWindowSize(new Vector2(280, 320), ImGuiCond.Once);
        ImGui.SetWindowPos(new Vector2(controller.Window.Size.X - 281, 50), ImGuiCond.Once);
        controller.SnapWindowToEdge(ref windowSnap);

        if (ImGui.CollapsingHeader("Scene Objects"))
        {
            ImGui.PushID("Meshes");
            if (ImGui.InputText("Search", ref searchQuery, 128))
            {
                if (string.IsNullOrEmpty(searchQuery))
                    searchRegex = null;
                else
                {
                    var escaped = new StringBuilder(Regex.Escape(searchQuery.ToLower()));
                    escaped = escaped.Replace("\\?", ".");
                    escaped = escaped.Replace("\\*", ".*");
                    searchRegex = new(escaped.ToString(), RegexOptions.CultureInvariant);
                }
            }
            if (program.SceneGraph?.SceneObjects != null)
            {
                if (ImGui.BeginTable("MeshesTab", 2, ImGuiTableFlags.SizingFixedFit))
                {
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed);
                    var scObjs = program.SceneGraph.SceneObjects;
                    //ImGui.BeginMultiSelect(ImGuiMultiSelectFlags.None);
                    for (int i = 0; i < scObjs.Count; i++)
                    {
                        DrawSceneObjTree(scObjs[i], i);
                    }
                    ImGui.EndTable();
                    //ImGui.EndMultiSelect();
                }
            }
            ImGui.PopID();
        }
        if (ImGui.CollapsingHeader("Lights"))
        {
            if (program.SceneGraph?.Lights != null)
            {
                ImGui.PushID("Lights");
                if (ImGui.BeginTable("LightsTab", 2, ImGuiTableFlags.SizingFixedFit))
                {
                    ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                    var lights = program.SceneGraph.Lights;
                    for (int i = 0; i < lights.Count; i++)
                    {
                        ImGui.TableNextColumn();
                        var light = lights[i];
                        ImGui.SetNextItemAllowOverlap();
                        ImGui.PushID(i);
                        ImGui.Checkbox("", ref lights[i].on);
                        ImGui.TableNextColumn(); ImGui.SameLine();
                        if (ImGui.Selectable(light.name, program.SelectedObject == light))
                        {
                            program.Select(light);
                        }
                        ImGui.PopID();
                    }
                    ImGui.EndTable();
                }
                ImGui.PopID();
            }
        }
        if (ImGui.CollapsingHeader("Cameras"))
        {
            if (program.SceneGraph?.camera is Camera camera)
            {
                ImGui.PushID("Cameras");
                if (ImGui.BeginTable("CamerasTab", 2, ImGuiTableFlags.SizingFixedFit))
                {
                    ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);

                    ImGui.TableNextColumn();
                    ImGui.SetNextItemAllowOverlap();
                    bool t = true;
                    ImGui.Checkbox("", ref t);
                    ImGui.TableNextColumn(); ImGui.SameLine();
                    if (ImGui.Selectable(camera.name, program.SelectedObject == camera))
                    {
                        program.Select(camera);
                    }

                    ImGui.EndTable();
                }
                ImGui.PopID();
            }
        }

        ImGui.End();
        return true;
    }

    private bool IsFiltered(SceneObject obj)
    {
        if (searchRegex != null && !searchRegex.Match(obj.name.ToLowerInvariant()).Success)
        {
            // This object is filtered, check it's children
            if (obj.children != null && obj.children.Count > 0)
            {
                bool filtered = true;
                foreach (var child in obj.children)
                    filtered &= IsFiltered(child);
                return filtered;
            }

            return true;
        }

        return false;
    }

    private void DrawSceneObjTree(SceneObject obj, int index)
    {
        // Determine whether this node is filtered
        //if (!obj.name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
        if (IsFiltered(obj))
            return;

        ImGuiTreeNodeFlags flags = (ImGuiTreeNodeFlags)(1 << 13) | ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.SpanAvailWidth
            | ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick;

        if (program.SelectedObject == obj)
            flags |= ImGuiTreeNodeFlags.Selected;
        if (obj.children == null || obj.children.Count == 0)
            flags |= ImGuiTreeNodeFlags.Leaf;

        ImGui.PushID(index);
        //ImGui.SetNextItemSelectionUserData(index);
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.SetNextItemAllowOverlap();
        bool open = ImGui.TreeNodeEx(obj.name, flags, obj.name);

        if (ImGui.IsItemClicked())
        {
            program.Select(obj);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenOverlappedByItem | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem))
        {
            //ImGui.SetNextItemAllowOverlap();
            ImGui.SameLine();
            if (ImGui.ArrowButton("Up", ImGuiDir.Up))
            {
                if (index > 0)
                {
                    if (obj.Parent?.children is IList<SceneObject> siblings)
                        (siblings[index - 1], siblings[index]) = (siblings[index], siblings[index - 1]);
                    else
                        program.SceneGraph?.Swap(index, index - 1);
                }
            }
            //ImGui.SetNextItemAllowOverlap();
            ImGui.SameLine();
            if (ImGui.ArrowButton("Down", ImGuiDir.Down))
            {
                if (obj.Parent?.children is IList<SceneObject> siblings && index + 1 < siblings.Count)
                    (siblings[index + 1], siblings[index]) = (siblings[index], siblings[index + 1]);
                else
                    program.SceneGraph?.Swap(index, index + 1);

            }

            /*if (obj is SDFObject sdf)
            {
                ImGui.SameLine();
                if (sdf.Parent == null)
                {
                    if (ImGui.Button("G"))
                    {
                        if (index == 0)
                        {
                            var g = program.SDFManager?.CreateSDF(SDFShape.Group);
                            for (int i = program.SceneGraph?.SceneObjects?.Count - 1 ?? 0; i > index; i--)
                                program.SceneGraph?.Swap(index, index - 1);
                            program.SceneGraph?.SetParent(obj, g);
                        }

                        if (program.SceneGraph?.SceneObjects[index-1] is SDFObject prevSDF)
                        {
                            if (prevSDF.shape == SDFShape.Group)
                                program.SceneGraph?.SetParent(obj, prevSDF);
                            else
                            {
                                var g = program.SDFManager?.CreateSDF(SDFShape.Group);
                                for (int i = program.SceneGraph?.SceneObjects?.Count - 1 ?? 0; i > index; i--)
                                    program.SceneGraph?.Swap(index, index - 1);
                                program.SceneGraph?.SetParent(prevSDF, g);
                                program.SceneGraph?.SetParent(obj, g);
                            }
                        }
                    }
                    ImGui.SetItemTooltip("Group this item with the item above it.");
                }
                else
                {
                    if (ImGui.Button("U"))
                    {
                        program.SceneGraph?.SetParent(obj, null);
                    }
                    ImGui.SetItemTooltip("Ungroup this item from it's current group.");
                }
            }*/
        }

        ImGui.TableNextColumn();
        ImGui.PushID("EnableCheckbox");
        switch (obj)
        {
            case Mesh mesh:
                ImGui.Checkbox("", ref mesh.visible);
                break;
            case Light light:
                ImGui.Checkbox("", ref light.on);
                break;
            case LODObject lod:
                ImGui.Checkbox("", ref lod.visible);
                break;
            //else if (obj is SDFObject sdf)
            //    ImGui.Checkbox("", ref sdf.enabled);
            default:
                {
                    ImGui.BeginDisabled();
                    bool tmp = true;
                    ImGui.Checkbox("", ref tmp);
                    ImGui.EndDisabled();
                    break;
                }
        }
        ImGui.PopID();

        if (open)
        {
            if (obj.children != null)
            {
                for (int i = 0; i < obj.children.Count; i++)
                {
                    DrawSceneObjTree(obj.children[i], i);
                }
            }

            ImGui.TreePop();
        }

        ImGui.PopID();
    }
}
