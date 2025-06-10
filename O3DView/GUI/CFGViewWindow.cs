using ImGuiNET;
using O3DParse.Ini;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static O3DView.GUI.CFGEditor;
using O3DView.Gizmos;

namespace O3DView.GUI;

public class CFGViewWindow : IGUIWindow
{
    private readonly GUIController controller;
    private readonly Program program;

    private bool isOpen;
    private WindowSnapEdge windowSnap;
    //private object? cfgObj;

    private readonly Dictionary<Type, CFGFieldCache> cfgFieldCache = [];

    public static IGUIWindow Construct(GUIController guiController, Program program)
    {
        var ret = new CFGViewWindow(guiController, program);

        guiController.RegisterWindow(ret, new()
        {
            window = ret,
            toggle = true,
            category = GUIController.MenuItemCategory.Tools,
            name = "CFG Viewer",
            shortcut = "",
            action = (open) => ret.isOpen = open,
        });

        return ret;
    }

    private CFGViewWindow(GUIController controller, Program program)
    {
        this.controller = controller;
        this.program = program;
    }

    public bool DrawWindow()
    {
        if (!isOpen)
            return false;

        if (ImGui.Begin("CFG Viewer", ref isOpen))
        {
            ImGui.SetWindowSize(new(1000, 600), ImGuiCond.Once);
            controller.SnapWindowToEdge(ref windowSnap);

            ImGui.TextWrapped("This window allows you to view the contents of the last loaded CFG/SCO/BUS/OVH file as OMSI " +
                "would read it. This can be helpful when diagnosing weird behaviours with CFG files and to help understand " +
                "the implicit hierarchy between the different tags. Currently, this window is read only.");
            ImGui.Spacing();

            object? target = null;
            switch (program.SelectedObject)
            {
                case CFGObject cfgObj:
                    ImGui.Text($"Loaded: '{cfgObj.path}'");
                    target = cfgObj.cfg;
                    break;
                case MapTileObject mapTileObj:
                    ImGui.Text($"Loaded: '{mapTileObj.path}'");
                    target = mapTileObj.map;
                    break;
                case GenericSplineMesh splineMesh:
                    ImGui.Text($"Loaded: '{splineMesh.name}'");
                    target = splineMesh.SplineData;
                    break;
                case PathMesh pathMesh:
                    ImGui.Text($"Loaded: '{pathMesh.name}'");
                    target = pathMesh.PathData;
                    break;
                case SceneObject obj:
                    ImGui.Text($"Loaded: '{obj.name}'");
                    target = obj;
                    break;
            }

            if (target == null)
            {
                ImGui.End();
                return isOpen;
            }

            var type = target.GetType();
            ImGui.Text($"Type: {target.GetType().Name}");
            ImGui.Spacing();
            if (!cfgFieldCache.TryGetValue(type, out var cache))
            {
                cache = BuildCFGFieldCache(type);
                cfgFieldCache.Add(type, cache);
            }

            DrawCFGFieldCache(target, cache);

            ImGui.End();
        }
        return isOpen;
    }
}
