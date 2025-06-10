using ImGuiNET;
#if ALLOW_DECRYPTION
using O3DParse.Encryption;
#endif
using O3DParse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using O3DParse.Ini;
using Ookii.Dialogs.WinForms;
using System.Diagnostics;
using System.Reflection;

namespace O3DView.GUI;

public class ExportWindow : IGUIWindow
{
    private readonly GUIController controller;
    private readonly Program program;
    private readonly SynchronizationContext syncContext;

    private bool isOpen;
    private WindowSnapEdge windowSnap;

    private bool onlySelected = true;
    private bool includeChildren = true;
    private bool exportAsOBJ = false;
    private bool overrideO3DSettings = false;
    private int o3dVersion = 7;
    private bool encryptO3D = true;
    private int o3dKey = 0;
    private bool includeTextures = true;
    private O3DParse.O3DExtendedOptions o3dOptions = O3DParse.O3DExtendedOptions.LONG_TRIANGLE_INDICES;
    private bool minifyCFG = false;

    private Task? exportTask;
    private Task? exportProgressTask;
    private int filesToExport = 0;
    private int filesExported = 0;
    private string version;

    public static IGUIWindow Construct(GUIController guiController, Program program)
    {
        var ret = new ExportWindow(guiController, program);

        guiController.RegisterWindow(ret, new()
        {
            window = ret,
            toggle = true,
            category = GUIController.MenuItemCategory.File,
            name = "Export",
            shortcut = "CTRL+S",
            action = (open) => ret.isOpen = open,
        });
        guiController.RegisterKeyboardShortcut(Silk.NET.Input.Key.S, ret, (kb) =>
        {
            if (kb.IsModifierKeyDown(ExtensionMethods.ModifierKeys.Ctrl))
                ret.isOpen = true;
        });

        return ret;
    }

    private ExportWindow(GUIController controller, Program program)
    {
        this.controller = controller;
        this.program = program;
        syncContext = SynchronizationContext.Current ?? new SynchronizationContext();
        version = Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? "DEV";
    }

    public bool DrawWindow()
    {
        if (!isOpen)
            return isOpen;

        if (ImGui.Begin("Export", ref isOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            controller.SnapWindowToEdge(ref windowSnap);

            ImGui.CollapsingHeader("General Settings", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.Leaf);
            ImGui.Checkbox("Only Export Selected", ref onlySelected);
            ImGui.SetItemTooltip("Exports only the currently selected object in the scene. Note that not all object types are supported.");
            ImGui.BeginDisabled(!onlySelected);
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1, 1), $"Selected: {program.SelectedObject?.name ?? "NONE"}");
            ImGui.Checkbox("Include Children", ref includeChildren);
            ImGui.SetItemTooltip("Also export all the child objects of the selected object.");
            ImGui.EndDisabled();
            ImGui.Checkbox("Export As OBJ", ref exportAsOBJ);

            ImGui.BeginDisabled(!exportAsOBJ);
            ImGui.CollapsingHeader("O3D Settings", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.Leaf);
            ImGui.Checkbox("Export Referenced Textures", ref includeTextures);
            ImGui.SetItemTooltip("When exporting to a directory different fromt he one the model was imported from; this option allows the referenced textures to be copied to the output directory.");
            ImGui.Checkbox("Override O3D Export Settings", ref overrideO3DSettings);
            ImGui.SetItemTooltip("When enabled, overrides the O3D export settings with those set here. When disabled, uses the O3D settings that were imported with the O3D file (recommended).");
            ImGui.BeginDisabled(!overrideO3DSettings);
            ImGui.DragInt("O3D Version", ref o3dVersion, 0.1f, 1, 7, "%d", ImGuiSliderFlags.AlwaysClamp);
            ImGui.SetItemTooltip("Sets the file format version of the exported O3D file. OMSI 2 supports all version, OMSI 1 and the OMSI SDK tools only support version 3 and below.");
            ImGui.BeginDisabled(o3dVersion <= 3);
            ImGui.Checkbox("Encrypt O3D", ref encryptO3D);
            ImGui.SetItemTooltip("Encrypts the O3D file. Note that this option is not supported in public releases of O3DView.");
            ImGui.InputInt("Encryption Key", ref o3dKey);
            //ImGui.InputScalar("Encryption Key", ImGuiDataType.U32, ref o3dKey);
            int o3dOptionsInt = (int)o3dOptions;
            if (ImGui.CheckboxFlags("Long Triangle Indices", ref o3dOptionsInt, (int)O3DParse.O3DExtendedOptions.LONG_TRIANGLE_INDICES))
                o3dOptions = (O3DParse.O3DExtendedOptions)o3dOptionsInt;
            if (ImGui.CheckboxFlags("Alternative Encryption Seed", ref o3dOptionsInt, (int)O3DParse.O3DExtendedOptions.ALT_ENCRYPTION_SEED))
                o3dOptions = (O3DParse.O3DExtendedOptions)o3dOptionsInt;
            ImGui.EndDisabled();
            ImGui.EndDisabled();
            ImGui.EndDisabled();

            ImGui.CollapsingHeader("CFG Settings", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.Leaf);
            ImGui.Checkbox("Minify CFG", ref minifyCFG);
            ImGui.SetItemTooltip("Removes all comments and unneeded blank lines from the exported CFG file.");

            ImGui.CollapsingHeader("To Export", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.Leaf);
            ImGui.BeginListBox("");
            if (onlySelected)
            {
                if (program.SelectedObject != null)
                {
                    DrawScObjChildren(program.SelectedObject, includeChildren);
                }
            }
            else
            {
                foreach (var obj in program.SceneGraph!.SceneObjects)
                {
                    DrawScObjChildren(obj, true);
                }
            }
            ImGui.EndListBox();

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Spacing();

            if (ImGui.Button("Export", new(150, 30)))
            {
                if (onlySelected)
                {
                    if (program.SelectedObject != null)
                        ExportFile(program.SelectedObject, includeChildren);
                }
                else
                {
                    foreach (var obj in program.SceneGraph!.SceneObjects)
                        ExportFile(obj, includeChildren);
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Export As", new(150, 30)))
            {
                string? dstPath = null;
                bool export = false;
                //CommonDialog dialog;

                (string ext, string filter)? filter = null;
                if (onlySelected)
                    filter = GetFileExtension(program.SelectedObject);

                if (filter == null)
                {
                    using VistaFolderBrowserDialog dialog = new()
                    {
                        Description = "Export To",
                        ShowNewFolderButton = true,
                        UseDescriptionForTitle = true,
                    };
                    var res = new DialogResult[1] { DialogResult.None };
                    syncContext.Send(x =>
                    {
                        if (x is DialogResult[] res)
                            res[0] = dialog.ShowDialog();
                    }, res);
                    if (res[0] == DialogResult.OK)
                    {
                        dstPath = dialog.SelectedPath;
                        export = true;
                    }
                }
                else
                {
                    using SaveFileDialog dialog = new()
                    {
                        DefaultExt = filter.Value.ext,
                        AddExtension = true,
                        Title = "Export As",
                        Filter = filter.Value.filter
                    };
                    var res = new DialogResult[1] { DialogResult.None };
                    syncContext.Send(x =>
                    {
                        if (x is DialogResult[] res)
                            res[0] = dialog.ShowDialog();
                    }, res);
                    if (res[0] == DialogResult.OK)
                    {
                        dstPath = dialog.FileName;
                        export = true;
                    }
                }

                if (export)
                {
                    DoExport(dstPath);
                }
            }

            ImGui.End();
        }
        return isOpen;
    }

    private void DoExport(string? dstPath)
    {
        exportTask = Task.Run(() =>
        {
            filesExported = 0;
            if (onlySelected)
            {
                filesToExport = 1;
                if (program.SelectedObject != null)
                    ExportFile(program.SelectedObject, includeChildren, dstPath);
            }
            else
            {
                filesToExport = program.SceneGraph!.SceneObjects.Count;
                foreach (var obj in program.SceneGraph!.SceneObjects)
                    ExportFile(obj, includeChildren, dstPath);
            }
        }).ContinueWith(_ =>
        {
            exportTask = null;
        });
        exportProgressTask = Task.Run(() =>
        {
            if (Program.GUIController?.IsProgressBarVisible ?? true)
                return;
            Program.GUIController.ShowProgressBar($"Exporting {filesToExport} files...");
            Stopwatch sw = Stopwatch.StartNew();
            while (!exportTask?.IsCompleted ?? false)
            {
                Program.GUIController.ShowProgressBar($"Exporting {filesToExport} files...");
                Program.GUIController.UpdateProgress(filesExported / (float)filesToExport);
                Thread.Sleep(33);
            }
            Program.Log($"Exported {filesExported} files in {sw.Elapsed.TotalSeconds:F4} s");
            Program.GUIController.HideProgressBar();
        });
    }

    private static (string ext, string filter)? GetFileExtension(SceneObject? obj)
    {
        return obj switch
        {
            O3DMesh => ((string ext, string filter)?)(".o3d", "O3D Files (*.o3d)|*.o3d|All Files (*.*)|*.*"),
            CFGObject cfg => cfg.cfg switch
            {
                OmsiRoadVehicle => ((string ext, string filter)?)(".bus", "BUS Files (*.bus)|*.bus|OVH Files (*.ovh)|*.ovh|All Files (*.*)|*.*"),
                OmsiComplMapObj => ((string ext, string filter)?)(".sco", "SCO Files (*.sco)|*.sco|All Files (*.*)|*.*"),
                OmsiCFGFile => ((string ext, string filter)?)(".cfg", "CFG Files (*.cfg)|*.cfg|All Files (*.*)|*.*"),
                _ => ((string ext, string filter)?)(".cfg", "CFG Files (*.cfg)|*.cfg|All Files (*.*)|*.*"),
            },
            _ => null,
        };
    }

    private static int DrawScObjChildren(SceneObject obj, bool includeChildren = false)
    {
        int drawn = 0;
        switch (obj)
        {
            case O3DMesh:
            case CFGObject:
                ImGui.Selectable(obj.name);
                drawn++;
                break;
            default:
                break;
        }

        if (obj.children == null || !includeChildren)
            return drawn;

        foreach (var child in obj.children)
        {
            drawn += DrawScObjChildren(child, includeChildren);
        }
        return drawn;
    }

    private bool ExportFile(SceneObject obj, bool includeChildren, string? path = null, string? rootDir = null)
    {
        string? exportPath = path;
        try
        {
            switch (obj)
            {
                case O3DMesh o3d:
                    {
                        O3DFile o3dFile = new(o3d.O3D);// Create a deep copy of the o3d file before setting override settings

                        var mat = o3d.transform.Matrix;
                        //var rotMat = Matrix4x4.CreateFromQuaternion(o3d.transform.Rot);
                        for (int i = 0; i < o3dFile.vertices.Length; i++)
                        {
                            ref var vert = ref o3dFile.vertices[i];
                            vert.pos = Vector3.Transform((Vector3)vert.pos, mat);
                            vert.normal = Vector3.TransformNormal(vert.normal, mat);
                        }

                        if (overrideO3DSettings)
                        {
                            o3dFile.version = (byte)o3dVersion;
                            o3dFile.extendedOptions = o3dOptions;
                            o3dFile.encryptionKey = encryptO3D ? unchecked((uint)o3dKey) : 0xffffffff;
                        }

#if ALLOW_DECRYPTION
                        if (o3dFile.encryptionKey != 0xffffffff && o3dFile.version > 3)
                        {
                            o3dFile = new(o3dFile); // Create a deep copy of the o3d file before encrypting it
                            O3DEncryption.EncryptO3D(ref o3dFile, o3dFile.encryptionKey, (o3dFile.extendedOptions & O3DExtendedOptions.ALT_ENCRYPTION_SEED) != 0);
                        }
#endif
                        path = ResolveExportDir(path, ref rootDir, o3d.O3DPath);

                        if (exportAsOBJ)
                        {
                            path = Path.ChangeExtension(path, ".obj");
                            WavefrontOBJIO.WriteOBJ(o3dFile, path, o3d.name);
                        }
                        else
                        {
                            O3DParser.WriteO3D(path, o3dFile);
                        }

                        if (includeTextures)
                        {
                            foreach (var sm in o3d.submeshes)
                            {
                                if (sm.mat.bumpMap is O3DTexture bump)
                                    TryCopyTexture(bump, path, ref rootDir);
                                if (sm.mat.transMap is O3DTexture trans)
                                    TryCopyTexture(trans, path, ref rootDir);
                                if (sm.mat.envMap is O3DTexture env)
                                    TryCopyTexture(env, path, ref rootDir);
                                if (sm.mat.lightMap is O3DTexture light)
                                    TryCopyTexture(light, path, ref rootDir);
                                if (sm.mat.nightMap is O3DTexture night)
                                    TryCopyTexture(night, path, ref rootDir);
                                if (sm.mat.texture is O3DTexture tex)
                                    TryCopyTexture(tex, path, ref rootDir);
                            }
                        }
                    }
                    break;
                case CFGObject cfg:
                    {
                        path = ResolveExportDir(path, ref rootDir, cfg.path);
                        //path = ResolveExportDir(path, ref rootDir, cfg.path);

                        if (!cfg.cfg.comments?.Contains("O3DView") ?? false)
                            cfg.cfg.comments = $"Exported using O3DView version {version}\r\n\r\n{cfg.cfg.comments}";

                        using var fs = File.OpenWrite(path);
                        switch (cfg.cfg)
                        {
                            case OmsiRoadVehicle rv:
                                OmsiIniSerializer.SerializeIniFile(rv, fs, false, minifyCFG);
                                break;
                            case OmsiComplMapObj cmpl:
                                OmsiIniSerializer.SerializeIniFile(cmpl, fs, false, minifyCFG);
                                break;
                            case OmsiCFGFile model:
                                OmsiIniSerializer.SerializeIniFile(model, fs, false, minifyCFG);
                                break;
                            default:
                                throw new Exception($"Unsupported CFG export type: {cfg.cfg.GetType().Name}!");
                        }
                    }
                    break;
                default:
                    path = null;
                    break;
            }
        }
        catch (Exception ex)
        {
            Program.Log($"Error saving {obj.name}: \n{ex.Message}", "Save Error", GUIController.Severity.Error);
            filesExported++;
            return false;
        }

        if (path != null)
            Program.Log($"Saved '{obj.name}' as '{path}'");

        if (obj.children == null || !includeChildren)
        {
            filesExported++;
            return true;
        }

        filesToExport += obj.children.Count;

        //if (path != null)
        //    path = Path.GetDirectoryName(path);

        foreach (var child in obj.children)
        {
            if (!ExportFile(child, includeChildren, exportPath, rootDir))
                return false;
        }

        filesExported++;
        return true;
    }

    private static void TryCopyTexture(O3DTexture tex, string? path, ref string? rootDir)
    {
        if (tex.ResolvedPath == null)
            return;
        string dst = ResolveExportDir(path, ref rootDir, tex.ResolvedPath);
        if (!File.Exists(dst))
            File.Copy(tex.ResolvedPath, dst);
    }

    private static string ResolveExportDir(string? path, ref string? rootDir, string objPath)
    {
        if (path != null && rootDir == null && !Path.HasExtension(path))
        {
            // Handle the case of saving to a directory
            rootDir = Path.GetDirectoryName(objPath);
            path = Path.Combine(path, Path.GetFileName(objPath));
        }

        if (rootDir != null && path != null)
        {
            string dir;
            if (!Path.HasExtension(path))
                dir = path;
            else
                dir = Path.GetDirectoryName(path) ?? "";
            path = Path.Combine(dir, Path.GetRelativePath(rootDir, objPath));
        }
        path ??= objPath;
        rootDir ??= Path.GetDirectoryName(objPath);

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "");

        return path;
    }
}
