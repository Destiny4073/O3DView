#if ALLOW_DECRYPTION
using O3DParse.Encryption;
#endif
using O3DParse.Ini;
using O3DParse;
using O3DView.GUI;
using O3DView.Gizmos;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace O3DView;

public class FileLoader
{
    private readonly Program program;
    private readonly Renderer renderer;
    private readonly SceneGraph sceneGraph;
    private readonly GUIController guiController;
    private readonly Settings settings;
    private readonly Stopwatch loadFilesSW;
    private readonly ConcurrentQueue<LoadObjectItem> fileLoadQueue;
    private bool progressBarVisible;

    internal FileLoader(Program program, Renderer renderer)
    {
        this.program = program;
        this.renderer = renderer;
        this.sceneGraph = program.SceneGraph ?? throw new ArgumentNullException(nameof(Program.SceneGraph));
        this.guiController = Program.GUIController ?? throw new ArgumentNullException(nameof(Program.GUIController));
        this.settings = Program.Settings;
        loadFilesSW = new Stopwatch();
        fileLoadQueue = [];
    }

    internal static void Log(string message, string? caption = null, GUIController.Severity severity = GUIController.Severity.Info, bool popupOnError = true)
    {
        Program.Log(message, caption, severity, popupOnError);
    }

    internal void OnFrame()
    {
        loadFilesSW.Restart();
        FileLoadJob? job = null;
        do
        {
            if (fileLoadQueue.TryDequeue(out var file))
            {
                job = file.job;
                if (file.path == null || file.loadCommand == null)
                    continue;
                file.loadCommand();
                job.CompleteItem();
            }
        } while (!fileLoadQueue.IsEmpty && loadFilesSW.Elapsed < settings.MaxLoadTimePerBatch);

        if (job != null)
        {
            guiController.ShowProgressBar($"Loading {job.filesToLoad} files...");
            progressBarVisible = true;
            guiController.UpdateProgress(job.filesLoaded / MathF.Max(job.filesToLoad - 1, 1));
        }
        else
        {
            if (progressBarVisible)
            {
                guiController.HideProgressBar();
                progressBarVisible = false;
            }
        }
    }

    public void CancelLoading()
    {
        guiController.ShowProgressBar($"Cancelling...");
        while (fileLoadQueue.TryDequeue(out var loadItem))
        {
            loadItem.job.CompleteItem();
        }
        guiController.HideProgressBar();
    }

    /// <summary>
    /// Queues one or more files for loading.
    /// </summary>
    /// <param name="files">An enumerable of file paths to load.</param>
    public void LoadFiles(IEnumerable<string> files)
    {
        FileLoadJob job = new();
        LoadFiles(files.Where(x =>
        {
            // Only take files that exist
            if (File.Exists(x))
                return true;
            else if (Directory.Exists(x)) // If a path is a directory recursively call LoadFiles on the o3d files contained within
                LoadFiles(Directory.GetFiles(x, "*.o3d", SearchOption.AllDirectories));
            return false;
        })
        .Select(file =>
        {
            var item = new LoadObjectItem(job, file, () =>
            {
                string ext = Path.GetExtension(file);
                switch (ext)
                {
                    case ".sco":
                        LoadSco(file, job);
                        break;
                    case ".cfg":
                        LoadCFGModel(file, job);
                        break;
                    case ".ovh":
                    case ".bus":
                        LoadBus(file, job);
                        break;
                    case ".o3d":
                    case ".rdy":
                        LoadO3D(file);
                        break;
                    case ".map":
                        LoadMapTile(file, job);
                        break;
                    case ".obj":
                        LoadOBJ(file);
                        break;
                    case ".cti":
                        LoadCTI(file);
                        break;
                    default:
                        Log($"Error Loading File: '{file}'", $"Unsupported file extension '{ext}'!", GUIController.Severity.Error);
                        break;
                }
            });
            return item;
        }), true);
    }

    /// <summary>
    /// Queues one or more files for loading.
    /// </summary>
    /// <param name="files"></param>
    public void LoadFiles(IEnumerable<LoadObjectItem> files, bool wait = false)
    {
        if (wait)
        {
            Task.Run(() =>
            {
                var job = LoadFilesInternal();

                if (job == null)
                    return;

                Thread.Sleep(50);
                Stopwatch sw = Stopwatch.StartNew();
                job.WaitForCompletion();
                sw.Stop();
                program.Focus(null);
                Log($"Loading completed in {sw.Elapsed}!");
            });
        } 
        else
        {
            LoadFilesInternal();
        }

        FileLoadJob? LoadFilesInternal()
        {
            FileLoadJob? job = null;
            foreach (var item in files)
            {
                item.job.AddItem();
                fileLoadQueue.Enqueue(item);
                job ??= item.job;
            }
            return job;
        }
    }

    private void LoadO3D(string path, SceneObject? parent = null, MeshCommand? cfgMesh = null, string? cfgPath = null)
    {
        try
        {
            var o3d = O3DParser.ReadO3D(path);
#if ALLOW_DECRYPTION
            if (o3d.encryptionKey != 0xffffffff)
                O3DEncryption.DecryptO3D(ref o3d);
#endif
            var mesh = new O3DMesh(renderer, o3d, path, cfgMesh, cfgPath);
            sceneGraph.Add(mesh, parent);
            program.VariableManager.AddSceneObject(mesh);
            Log($"Loaded file: '{path}'");
        }
        catch (Exception ex)
        {
            Log($"Error Loading O3D File: '{path}'\n{ex}", "Load O3D", GUIController.Severity.Error);
        }
    }

    private void LoadOBJ(string path, SceneObject? parent = null, MeshCommand? cfgMesh = null, string? cfgPath = null)
    {
        try
        {
            var o3d = WavefrontOBJIO.ReadOBJ(path);
            var mesh = new O3DMesh(renderer, o3d, path, cfgMesh, cfgPath);
            sceneGraph.Add(mesh, parent);
            program.VariableManager.AddSceneObject(mesh);
            Log($"Loaded file: '{path}'");
        }
        catch (Exception ex)
        {
            Log($"Error Loading OBJ File: '{path}'\n{ex}", "Load OBJ", GUIController.Severity.Error);
        }
    }

    private void LoadCTI(string path, SceneObject? parent = null)
    {
        try
        {
            string basePath = Path.GetDirectoryName(path) ?? throw new Exception($"Couldn't get directory of '{path}'!");
            string fileName = Path.GetFileNameWithoutExtension(path);

            using var fs = File.OpenRead(path);
            var cti = OmsiIniSerializer.DeserializeIniFile<OmsiCTIFile>(fs, recursive: false, filepath: path);

            var ctiSceneObj = new CTIObject(cti, fileName, path);
            sceneGraph.Add(ctiSceneObj, parent);
            program.VariableManager.AddSceneObject(ctiSceneObj);

            Log($"Loaded CTI: '{path}'");
        }
        catch (Exception ex)
        {
            Log($"Error Loading CTI File: '{path}'\n{ex}", "Load CTI", GUIController.Severity.Error);
        }
    }

    public void LoadBus(string path, FileLoadJob? job = null)
    {
        try
        {
            string basePath = Path.GetDirectoryName(path) ?? throw new Exception($"Couldn't get directory of '{path}'!");
            //basePath = Path.Combine(basePath, "model");

            using var fs = File.OpenRead(path);
            var cfg = OmsiIniSerializer.DeserializeIniFile<OmsiRoadVehicle>(fs, recursive: true, filepath: path);
            //lastLoadedCFG = cfg;
            //lastLoadedCFGPath = path;

            job ??= new();
            var parent = LoadMapObj(path, basePath, cfg, true, null, job);
            if (cfg.model != null)
                LoadMapObj(cfg.model.path ?? path, basePath, cfg.model, true, parent, job);

            if (cfg.pathFiles != null)
            {
                LoadFiles([new LoadObjectItem(job, path, () => {
                    LoadCabinPaths(cfg.pathFiles, parent, job);
                })]);
            }

            if (cfg.passengerCabin != null)
            {
                LoadPassengerCabin(cfg.passengerCabin, cfg.pathFiles, parent);
            }

            Log($"Loaded bus: '{path}'");
        }
        catch (Exception ex)
        {
            Log($"Error Loading Bus File: '{path}'\n{ex}", "Load Bus", GUIController.Severity.Error);
        }
    }

    public void LoadPassengerCabin(OmsiPassengerCabin passengerCabin, OmsiPaths? paths, SceneObject parent)
    {
        var cabinParent = new PseudoObject($"Passenger Cabin");
        cabinParent.metadata = passengerCabin;
        sceneGraph.Add(cabinParent, parent);

        if (passengerCabin.ticketSale != null)
        {
            var gizmo = new BillboardGizmo<TicketSaleCommand>("Ticket Sale Point", "gizmo_ticket_sale.png", Program.GetResourcePath("dummy"), passengerCabin.ticketSale, renderer);
            gizmo.transform.Pos = passengerCabin.ticketSale.pos.XiZY();
            gizmo.OnModified += (o, d) => gizmo.transform.Pos = o!.pos.XiZY();
            sceneGraph.Add(gizmo, cabinParent);
        }
        if (passengerCabin.ticketSaleChangePoint != null)
        {
            var gizmo = new BillboardGizmo<TicketSaleChangePoint2Command>("Ticket Change Point", "gizmo_ticket_change.png", Program.GetResourcePath("dummy"), passengerCabin.ticketSaleChangePoint, renderer);
            gizmo.transform.Pos = passengerCabin.ticketSaleChangePoint.moneyPos.XiZY();
            gizmo.OnModified += (o, d) => gizmo.transform.Pos = o!.moneyPos.XiZY();
            sceneGraph.Add(gizmo, cabinParent);
        }
        if (passengerCabin.ticketSaleMoneyPoint != null)
        {
            var gizmo = new BillboardGizmo<TicketSaleMoneyPoint2Command>("Ticket Money Point", "gizmo_ticket_money.png", Program.GetResourcePath("dummy"), passengerCabin.ticketSaleMoneyPoint, renderer);
            gizmo.transform.Pos = passengerCabin.ticketSaleMoneyPoint.moneyPos.XiZY();
            gizmo.OnModified += (o, d) => gizmo.transform.Pos = o!.moneyPos.XiZY();
            sceneGraph.Add(gizmo, cabinParent);
        }
        if (passengerCabin.stamper != null)
        {
            var gizmo = new BillboardGizmo<StamperCommand>("Stamper", "gizmo_stamper.png", Program.GetResourcePath("dummy"), passengerCabin.stamper, renderer);
            gizmo.transform.Pos = passengerCabin.stamper.pos.XiZY();
            gizmo.OnModified += (o, d) => gizmo.transform.Pos = o!.pos.XiZY();
            sceneGraph.Add(gizmo, cabinParent);
        }
        if (passengerCabin.linkToNextVeh != null)
        {
            var gizmo = new PathPointGizmo<LinkToNextVehCommand>(paths!, (cmd, pth) =>
            {
                int point = cmd.pathPoint;
                if (pth?.points != null && pth.points.Length > point && point >= 0)
                    return pth.points[point].position.XiZY();
                return Vector3.Zero;
            }, "Link To Next Veh", "gizmo_link_next.png", Program.GetResourcePath("dummy"), passengerCabin.linkToNextVeh, renderer);

            sceneGraph.Add(gizmo, cabinParent);
        }
        if (passengerCabin.linkToPrevVeh != null)
        {
            var gizmo = new PathPointGizmo<LinkToPrevVehCommand>(paths!, (cmd, pth) =>
            {
                int point = cmd.pathPoint;
                if (pth?.points != null && pth.points.Length > point && point >= 0)
                    return pth.points[point].position.XiZY();
                return Vector3.Zero;
            }, "Link To Prev Veh", "gizmo_link_prev.png", Program.GetResourcePath("dummy"), passengerCabin.linkToPrevVeh, renderer);

            sceneGraph.Add(gizmo, cabinParent);
        }
        for (int i = 0; i < passengerCabin.entries.Length; i++)
        {
            EntryCommand entry = passengerCabin.entries[i];
            var gizmo = new PathPointGizmo<EntryCommand>(paths!, (cmd, pth) =>
            {
                int point = cmd.pathPoint;
                if (pth?.points != null && pth.points.Length > point && point >= 0)
                    return pth.points[point].position.XiZY();
                return Vector3.Zero;
            }, $"Entry {i}", "gizmo_entry.png", Program.GetResourcePath("dummy"), entry, renderer);
            
            sceneGraph.Add(gizmo, cabinParent);
        }
        for (int i = 0; i < passengerCabin.exits.Length; i++)
        {
            ExitCommand exit = passengerCabin.exits[i];
            var gizmo = new PathPointGizmo<ExitCommand>(paths!, (cmd, pth) =>
            {
                int point = cmd.pathPoint;
                if (pth?.points != null && pth.points.Length > point && point >= 0)
                    return pth.points[point].position.XiZY();
                return Vector3.Zero;
            }, $"Exit {i}", "gizmo_exit.png", Program.GetResourcePath("dummy"), exit, renderer);
            
            sceneGraph.Add(gizmo, cabinParent);
        }
        for (int i = 0; i < passengerCabin.seats.Length; i++)
        {
            SeatCommand seat = passengerCabin.seats[i];
            var gizmo = new SeatGizmo($"Seat {i}", "gizmo_seat.png", Program.GetResourcePath("dummy"), seat, program, renderer);
            gizmo.transform.Pos = seat.position.XiZY();
            gizmo.OnModified += (o, d) => gizmo.transform.Pos = o!.position.XiZY();
            sceneGraph.Add(gizmo, cabinParent);
        }
    }

    public CFGObject? LoadSco(string path, FileLoadJob? job = null, SceneObject? parent = null)
    {
        try
        {
            string basePath = Path.GetDirectoryName(path) ?? throw new Exception($"Couldn't get directory of '{path}'!");
            //basePath = Path.Combine(basePath, "model");

            using var fs = File.OpenRead(path);
            var cfg = OmsiIniSerializer.DeserializeIniFile<OmsiComplMapObj>(fs, recursive: true, filepath: path);
            //lastLoadedCFG = cfg;
            //lastLoadedCFGPath = path;

            var localParent = LoadMapObj(path, basePath, cfg, true, parent, job);
            if (cfg.model != null)
                LoadMapObj(path, basePath, cfg.model, true, localParent, job);
            if (cfg.paths != null)
            {
                job ??= new();
                string? splineDir = FindOmsiDir(basePath, "Splines");
                if (splineDir != null)
                {
                    splineDir = Path.Combine(splineDir, "Splines");
                    LoadFiles(cfg.paths.Select((p, i) =>
                    {
                        return new LoadObjectItem(job, path, () =>
                        {
                            LoadSplineObj(path, i, cfg, splineDir, localParent, job);
                        });
                    }));
                }
            }

            Log($"Loaded sco: '{path}'");

            return localParent;
        }
        catch (Exception ex)
        {
            Log($"Error Loading SCO File: '{path}'\n{ex}", "Load SCO", GUIController.Severity.Error);
        }
        return null;
    }

    public void LoadCFGModel(string path, FileLoadJob? job = null, SceneObject? parent = null)
    {
        try
        {
            string basePath = Path.GetDirectoryName(path) ?? throw new Exception($"Couldn't get directory of '{path}'!");

            using var fs = File.OpenRead(path);
            var cfg = OmsiIniSerializer.DeserializeIniFile<OmsiCFGFile>(fs);
            //lastLoadedCFG = cfg;
            //lastLoadedCFGPath = path;

            LoadMapObj(path, basePath, cfg, false, parent, job);

            Log($"Loaded cfg: '{path}'");
        }
        catch (Exception ex)
        {
            Log($"Error Loading CFG File: '{path}'\n{ex}", "Load CFG", GUIController.Severity.Error);
        }
    }

    private CFGObject LoadMapObj(string path, string basePath, OmsiCFGFile cfg, bool modelSubdir = false, SceneObject? parent = null, FileLoadJob? job = null)
    {
        List<(SceneObject? parent, MeshCommand[] meshes)> meshes = [];
        InteriorLightCommand[] interiorLights = cfg.interiorLights ?? [];

        string fullPath = Path.Combine(basePath, path);
        var parentObj = new CFGObject(cfg, fullPath)
        {
            name = Path.GetFileNameWithoutExtension(path)
        };
        sceneGraph.Add(parentObj, parent);
        program.VariableManager.AddSceneObject(parentObj);

        if (cfg.lods != null)
        {
            foreach (var lod in cfg.lods)
            {
                var lodParent = new LODObject(lod, cfg.lods);
                sceneGraph.Add(lodParent, parentObj);
                meshes.Add((lodParent, lod.meshes ?? []));
            }
        }

        if (cfg.meshes != null)
            meshes.Add((parentObj, cfg.meshes));

        if (modelSubdir)
            basePath = Path.Combine(basePath, "model");

        job ??= new();
        foreach (var lod in meshes)
        {
            LoadFiles(lod.meshes.Select(x =>
            {
                if (x.path.EndsWith(".x"))
                {
                    Log($"X file importing is not supported: {x.path}", severity: GUIController.Severity.Warning);
                    return new LoadObjectItem(job);
                }
                string childPath = Path.Combine(basePath, x.path);
                return new LoadObjectItem(job, childPath, () => LoadO3D(childPath, lod.parent, x, fullPath));
            }));
        }

        if (interiorLights.Length > 0)
        {
            var lightsParent = new PseudoObject("Interior Lights");
            sceneGraph.Add(lightsParent, parentObj);
            foreach (var light in interiorLights)
            {
                var l = new Light()
                {
                    ambient = Vector3.Zero,
                    colour = light.colour.ToVector3,
                    intensity = 1,
                    range = light.range,
                    on = true,
                    mode = LightMode.Point,
                };
                l.transform.Pos = new(light.pos.X, light.pos.Z, light.pos.Y);
                sceneGraph.Add(l, lightsParent);
            }
        }
        return parentObj;
    }

    private GenericSplineMesh? LoadSplineObj(string path, int splineIndex, OmsiMapTile cfg, string splineDir, SceneObject? parent = null, FileLoadJob? job = null)
    {
        try
        {
            var mesh = new GenericSplineMesh(renderer, splineDir, cfg.splines![splineIndex], path);
            sceneGraph.Add(mesh, parent);
            program.VariableManager.AddSceneObject(mesh);
            Log($"Loaded spline: '{splineIndex}'");
            return mesh;
        }
        catch (Exception ex)
        {
            Log($"Error Loading Spline {splineIndex} in {path ?? "NONE"}\n{ex}", "Load Spline", GUIController.Severity.Error);
        }

        return null;
    }

    private PathMesh? LoadSplineObj(string path, int pathIndex, OmsiComplMapObj cfg, string splineDir, SceneObject? parent = null, FileLoadJob? job = null)
    {
        try
        {
            var mesh = new PathMesh(renderer, splineDir, cfg, pathIndex, path);
            sceneGraph.Add(mesh, parent);
            program.VariableManager.AddSceneObject(mesh);
            Log($"Loaded spline: '{pathIndex}'");
            return mesh;
        }
        catch (Exception ex)
        {
            Log($"Error Loading Spline {pathIndex} in {cfg.friendlyName?.name ?? "NONE"}\n{ex}", "Load Spline", GUIController.Severity.Error);
        }

        return null;
    }

    private void LoadMapTile(string path, FileLoadJob job, SceneObject? parent = null)
    {
        try
        {
            string basePath = Path.GetDirectoryName(path) ?? throw new Exception($"Couldn't get directory of '{path}'!");

            using var fs = File.OpenRead(path);
            var cfg = OmsiIniSerializer.DeserializeIniFile<OmsiMapTile>(fs);
            //lastLoadedCFG = cfg;
            //lastLoadedCFGPath = path;
            string? omsiDir = FindOmsiDir(basePath, "Splines");
            if (omsiDir == null)
                return;
            string splinesDir = omsiDir;//Path.Combine(omsiDir, "Splines");

            string fullPath = Path.Combine(basePath, path);
            var parentObj = new MapTileObject(cfg, fullPath)
            {
                name = Path.GetFileNameWithoutExtension(path)
            };
            sceneGraph.Add(parentObj, parent);
            program.VariableManager.AddSceneObject(parentObj);

            if (cfg.terrain != null)
            {
                
            }

            if (cfg.sceneObjects != null)
            {
                var scObjsParent = new PseudoObject("Scenery Objects");
                sceneGraph.Add(scObjsParent, parentObj);
                job ??= new();
                LoadFiles(cfg.sceneObjects.Select((x, i) =>
                {
                    string childPath = Path.Combine(omsiDir, x.path ?? "");
                    return new LoadObjectItem(job, childPath, () =>
                    {
                        var obj = LoadSco(childPath, job, scObjsParent);
                        if (obj != null)
                        {
                            obj.transform.Pos = new(-x.posX, x.posY, x.posZ);
                            obj.transform.EulerRot = new(0, -x.heading * MathF.PI / 180, 0);
                        }
                    });
                }));
            }

            if (cfg.splines != null)
            {
                var splinesParent = new PseudoObject("Splines");
                sceneGraph.Add(splinesParent, parentObj);
                job ??= new();
                LoadFiles(cfg.splines.Select((x,i) =>
                {
                    string childPath = Path.Combine(splinesDir, x.path ?? "");
                    return new LoadObjectItem(job, childPath, () => LoadSplineObj(path, i, cfg, splinesDir, splinesParent, job));
                }));
            }

            if (cfg.attachObjects != null)
            {
                var scObjsParent = new PseudoObject("Attach Objects");
                sceneGraph.Add(scObjsParent, parentObj);
            }

            if (cfg.splineAttachements != null)
            {
                var scObjsParent = new PseudoObject("Spline Attachements");
                sceneGraph.Add(scObjsParent, parentObj);
            }

            if (cfg.water != null)
            {
                var scObjsParent = new PseudoObject("Water");
                sceneGraph.Add(scObjsParent, parentObj);
            }

            Log($"Loaded map tile: '{path}'");
        }
        catch (Exception ex)
        {
            Log($"Error Loading map tile: '{path}'\n{ex}", "Load CFG", GUIController.Severity.Error);
        }
    }

    private CabinPathGizmo? LoadCabinPaths(OmsiPaths paths, SceneObject? parent = null, FileLoadJob? job = null)
    {
        try
        {
            var mesh = new CabinPathGizmo(paths, renderer);
            sceneGraph.Add(mesh, parent);
            program.VariableManager.AddSceneObject(mesh);
            Log($"Loaded cabin paths for: '{parent?.name ?? "NONE"}'");
            return mesh;
        }
        catch (Exception ex)
        {
            Log($"Error Loading Cabin Paths for {parent?.name ?? "NONE"}\n{ex}", "Load Cabin Paths", GUIController.Severity.Error);
        }

        return null;
    }

    private static string? FindOmsiDir(string basePath, string query)
    {
        string? ret = basePath;
        do
        {
            if (Directory.Exists(Path.Combine(ret, query)))
                return ret;
            ret = Path.GetDirectoryName(ret);
        } while (ret != null);
        return ret;
    }

    public class FileLoadJob
    {
        public readonly CountdownEvent filesRemaining;
        public int filesToLoad;
        public int filesLoaded;

        public FileLoadJob()
        {
            this.filesRemaining = new(0);
        }

        public void AddItem(int n = 1)
        {
            filesRemaining.Reset(filesRemaining.CurrentCount + n);
            Interlocked.Add(ref filesToLoad, n);
        }

        public void CompleteItem(int n = 1)
        {
            filesRemaining.Signal(n);
            Interlocked.Add(ref filesLoaded, n);
        }

        public void WaitForCompletion()
        {
            while (filesLoaded != filesToLoad)
            {
                filesRemaining.Wait();
            }
        }

        public void WaitForCompletion(TimeSpan timeout)
        {
            if (filesLoaded != filesToLoad)
            {
                filesRemaining.Wait(timeout);
            }
        }
    }

    public readonly struct LoadObjectItem
    {
        //public SceneObject? parent;
        public readonly FileLoadJob job;
        public readonly string? path;
        public readonly Action? loadCommand;
        //public MeshCommand? cfgMesh;
        //public string? cfgPath;

        public LoadObjectItem(FileLoadJob job, string? path = null, Action? loadCommand = null)
        {
            this.job = job;
            this.path = path;
            this.loadCommand = loadCommand;
        }

        /*public LoadObjectItem(string path)
        {
            this.path = path;
        }

        public LoadObjectItem(string path, MeshCommand? cfgMesh, string? cfgPath, SceneObject? parent)
        {
            this.path = path;
            this.cfgMesh = cfgMesh;
            this.cfgPath = cfgPath;
            this.parent = parent;
        }*/
    }
}
