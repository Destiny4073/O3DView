using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace O3DView.GUI;

public class PerfOverlayWindow : IGUIWindow
{
    private readonly GUIController controller;
    private readonly Program program;

    private bool isOpen;

    public static IGUIWindow Construct(GUIController guiController, Program program)
    {
        var ret = new PerfOverlayWindow(guiController, program);

        guiController.RegisterWindow(ret, new()
        {
            window = ret,
            toggle = true,
            category = GUIController.MenuItemCategory.Tools,
            name = "Performance Overlay",
            shortcut = "CTRL+P",
            action = (open) => ret.isOpen = open,
        });
        guiController.RegisterKeyboardShortcut(Silk.NET.Input.Key.P, ret, kb =>
        {
            if (kb.IsModifierKeyDown(ExtensionMethods.ModifierKeys.Ctrl))
                ret.isOpen = !ret.isOpen;
        });

        return ret;
    }

    private PerfOverlayWindow(GUIController controller, Program program)
    {
        this.controller = controller;
        this.program = program;
    }

    public bool DrawWindow()
    {
        if (!isOpen)
            return false;

        var overlayFlags = ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing;
        ImGui.SetNextWindowBgAlpha(0.4f);
        ImGui.SetNextWindowPos(new(8, ImGui.GetMainViewport().Size.Y - 8), ImGuiCond.Always, new(0, 1));
        if (ImGui.Begin("Perf Overlay", ref isOpen, overlayFlags))
        {
            ImGui.Text($"Verts:      {program.RenderedVerts,7}");
            ImGui.Text($"Tris:       {program.RenderedTris,7}");
            ImGui.Text($"Draw calls: {program.DrawCalls,7}");
            ImGui.Text($"Lights:     {Math.Min(program.SceneGraph?.Lights?.Count ?? 0, Program.Settings.MaxLights),7}");
            ImGui.Text($"FPS:        {program.FPS,7:F2}");
            ImGui.Text($"Hovered:    {program.HoveredObject?.name ?? "NONE"}");
            //ImGui.Text($"Ray: p:{program.MouseRay.start} d:{program.MouseRay.dir}");
#if DEBUG
            ImGui.Text(program.debugString);
#endif
            if (ImGui.BeginPopupContextWindow())
            {
                if (ImGui.MenuItem("Close"))
                    isOpen = false;
                ImGui.EndPopup();
            }
            ImGui.End();
        }

        return isOpen;
    }
}
