using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace O3DView.GUI;

public class AboutWindow : IGUIWindow
{
    private readonly GUIController controller;
    private readonly Program program;

    private bool isOpen;
    private WindowSnapEdge windowSnap;

    public static IGUIWindow Construct(GUIController guiController, Program program)
    {
        var ret = new AboutWindow(guiController, program);

        guiController.RegisterWindow(ret, new()
        {
            window = ret,
            toggle = true,
            category = GUIController.MenuItemCategory.Help,
            name = "About",
            shortcut = "",
            action = (open) => ret.isOpen = open,
        });

        return ret;
    }

    private AboutWindow(GUIController controller, Program program)
    {
        this.controller = controller;
        this.program = program;
    }

    public bool DrawWindow()
    {
        if (!isOpen)
            return isOpen;

        ImGui.Begin("About", ref isOpen, ImGuiWindowFlags.AlwaysAutoResize);
        controller.SnapWindowToEdge(ref windowSnap);

        ImGui.Text("O3DView - A simple O3D file viewer");
        ImGui.Separator();
        ImGui.Text("Copyright Thomas Mathieson 2024 All Rights Reserved");
#if DEBUG
        string build = "Debug";
#else
        string build = "Release";
#endif
        ImGui.Text($"Version: {Application.ProductVersion}-{build}");
#if ALLOW_DECRYPTION
        ImGui.Text($"O3D decryption enabled!");
#endif
        ImGui.Separator();

        ImGui.End();
        return isOpen;
    }
}
