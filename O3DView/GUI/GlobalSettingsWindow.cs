using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace O3DView.GUI;

public class GlobalSettingsWindow : IGUIWindow
{
    private readonly GUIController controller;
    private readonly Program program;

    public static IGUIWindow Construct(GUIController guiController, Program program)
    {
        var ret = new GlobalSettingsWindow(guiController, program);

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

    private GlobalSettingsWindow(GUIController controller, Program program)
    {
        this.controller = controller;
        this.program = program;
    }

    public bool DrawWindow()
    {
        ImGui.Begin("GlobalSettings");
        ImGui.SetWindowSize(new Vector2(280, 100), ImGuiCond.Once);
        ImGui.SetWindowPos(new Vector2(1, 400), ImGuiCond.Once);
        //SnapWindowToEdge(ref inspectorWindowSnap);

        // TODO: Move to own window...
        if (ImGui.CollapsingHeader("Global Settings", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.SliderFloat("Lightmap Strength", ref program.lightmapStrength, 0, 2);
            ImGui.SliderFloat("Nightmap Strength", ref program.nightmapStrength, 0, 2);
        }

        ImGui.End();
        return true;
    }
}
