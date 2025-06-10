using ImGuiNET;
using O3DParse.Ini;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace O3DView.GUI;

public class VariableWindow : IGUIWindow
{
    private readonly Program program;
    private readonly GUIController controller;

    private bool isOpen;
    private bool displaySelected;
    private WindowSnapEdge windowSnap;
    private int selectIndex;

    public static IGUIWindow Construct(GUIController guiController, Program program)
    {
        var ret = new VariableWindow(guiController, program);

        guiController.RegisterWindow(ret, new()
        {
            window = ret,
            toggle = true,
            category = GUIController.MenuItemCategory.Tools,
            name = "Variable Manager",
            shortcut = "",
            action = (open) => ret.isOpen = open,
        });

        return ret;
    }

    private VariableWindow(GUIController controller, Program program)
    {
        this.controller = controller;
        this.program = program;
    }

    public bool DrawWindow()
    {
        if (!isOpen)
            return false;

        if (ImGui.Begin("Variable Manager", ref isOpen))
        {
            ImGui.SetWindowSize(new(700, 600), ImGuiCond.Once);
            controller.SnapWindowToEdge(ref windowSnap);

            ImGui.TextWrapped("In this window all the variables referenced by the loaded file are displayed and can be edited. " +
                "Note that, tracking variable references is difficult, so sometimes the variable sliders might not update the " +
                "correct objects, in this case, press the 'Refresh Variables' button to relink all the variables.");
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1, 1), $"Selected: {program.SelectedObject?.name ?? "NONE"}");
            ImGui.Checkbox("Only Show Selected", ref displaySelected);
            if (ImGui.Button("Refresh Variables"))
            {
                program.VariableManager.Clear();
                foreach (var obj in program.SceneGraph!.SceneObjects)
                    program.VariableManager.AddSceneObject(obj);
            }
            ImGui.Spacing();

            var selected = program.SelectedObject;
            foreach (var category in program.VariableManager.VariablesCategorised.Keys)
            {
                IEnumerable<OSCVariable> vars = program.VariableManager.VariablesCategorised[category];
                if (displaySelected && selected != null)
                    vars = vars.Where(x => x.Targets.Contains(selected));

                if (ImGui.CollapsingHeader(category, ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.PushID(category);
                    if (ImGui.BeginTable("VarsTab", 2, ImGuiTableFlags.SizingStretchProp))
                    {
                        //ImGui.TableSetupColumn("Var", ImGuiTableColumnFlags.WidthStretch);
                        //ImGui.TableSetupColumn("Users", ImGuiTableColumnFlags.WidthStretch);

                        foreach (var var in vars)
                        {
                            ImGui.TableNextColumn();
                            switch (var)
                            {
                                case OSCFloatVariable floatVar:
                                    float fval = floatVar.Value;
                                    if (floatVar.min != floatVar.max)
                                    {
                                        if (ImGui.DragFloat(var.name, ref fval, (floatVar.max - floatVar.min) / 20f, floatVar.min, floatVar.max))
                                            floatVar.Value = fval;
                                    }
                                    else
                                    {
                                        if (ImGui.DragFloat(var.name, ref fval, 0.1f))
                                            floatVar.Value = fval;
                                    }
                                    break;
                                case OSCStringVariable stringVar:
                                    string sval = stringVar.Value;
                                    if (ImGui.InputText(var.name, ref sval, 32))
                                        stringVar.Value = sval;
                                    break;
                                case OSCViewpointVariable viewpointVar:
                                    {
                                        int ival = viewpointVar.Value;
                                        if (ImGui.Combo(var.name, ref ival, OSCViewpointVariable.ViewPointNames, OSCViewpointVariable.ViewPointNames.Length))
                                            viewpointVar.Value = ival;
                                    }
                                    break;
                                case OSCColourSchemeVariable colourSchemeVar:
                                    {
                                        int ival = colourSchemeVar.Value;
                                        if (ImGui.Combo(var.name, ref ival, colourSchemeVar.ColourSchemes.items, colourSchemeVar.ColourSchemes.count))
                                            colourSchemeVar.Value = ival;
                                    }
                                    break;
                            }
                            ImGui.TableNextColumn();
                            ImGui.PushID(var.name);
                            if (ImGui.Button($"[{var.Targets.Count} uses]"))
                            {
                                program.Select(var.Targets[selectIndex % var.Targets.Count]);
                                selectIndex++;
                            }
                            if (ImGui.BeginItemTooltip())
                            {
                                ImGui.TextWrapped("Click to cycle through the objects which reference this variable.");
                                ImGui.Text("Used by:");
                                foreach(var target in var.Targets)
                                    ImGui.TextDisabled($" - {target.name}");
                                ImGui.EndTooltip();
                            }
                            ImGui.PopID();
                        }
                        ImGui.EndTable();
                    }
                    ImGui.PopID();
                }
            }
        }
        ImGui.End();
        return isOpen;
    }
}
