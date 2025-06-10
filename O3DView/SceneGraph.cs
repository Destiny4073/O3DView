using O3DParse.Ini;
using O3DView.Gizmos;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace O3DView;

public class SceneGraph
{
    public Camera camera = new();

    public ReadOnlyCollection<SceneObject> SceneObjects => sceneObjectsRO;
    public ReadOnlyCollection<Light> Lights => lightsRO;

    private readonly List<SceneObject> sceneObjects = [];
    private readonly List<Light> lights = [];
    private ReadOnlyCollection<SceneObject> sceneObjectsRO;
    private ReadOnlyCollection<Light> lightsRO;

    public SceneGraph()
    {
        sceneObjectsRO = sceneObjects.AsReadOnly();
        lightsRO = lights.AsReadOnly();
    }

    public void Clear()
    {
        lights.Clear();
        sceneObjects.Clear();
    }

    public void CreateSun()
    {
        var sun = new Light
        {
            name = "Sun Light",
            colour = new(.9f, .9f, .9f),
            ambient = new(.1f, .1f, .1f),
            mode = LightMode.Directional,
            intensity = 1
        };
        Add(sun);
    }

    public void SetParent(SceneObject sceneObj, SceneObject? parent)
    {
        if (sceneObj.Parent == parent)
            return;

        if (sceneObj.Parent == null)
        {
            sceneObjects.Remove(sceneObj);
            parent!.children ??= [];
            parent.children.Add(sceneObj);
            parent.OnChildrenChanged(sceneObj, true);
            sceneObj.Parent = parent;
        }
        else
        {
            sceneObj.Parent.RemoveChild(sceneObj);
            if (parent != null)
            {
                parent.children ??= [];
                parent.children.Add(sceneObj);
                parent.OnChildrenChanged(sceneObj, true);
            }
            else
            {
                sceneObjects.Add(sceneObj);
            }
            sceneObj.Parent = parent;
        }
    }

    public void Remove(SceneObject? sceneObj)
    {
        if (sceneObj == null)
            return;

        if (!sceneObjects.Remove(sceneObj))
        {
            foreach (var root in sceneObjects)
            {
                if (root.RemoveChild(sceneObj))
                    break;
            }
        }

        if (sceneObj is Light light)
            lights.Remove(light);
    }

    public void Add(SceneObject sceneObj, SceneObject? parent = null)
    {
        if (sceneObj.Parent == null && parent == null)
            sceneObjects.Add(sceneObj);

        if (parent != null)
        {
            parent.children ??= [];
            parent.children.Add(sceneObj);
            parent.OnChildrenChanged(sceneObj, true);
            sceneObj.Parent = parent;
        }

        if (sceneObj is Light light)
            lights.Add(light);
    }

    // Ugly method...
    internal void Swap(int first, int second)
    {
        if (first < 0 || first >= sceneObjects.Count || second < 0 || second >= sceneObjects.Count)
            return;
        //throw new ArgumentOutOfRangeException();

        (sceneObjects[first], sceneObjects[second]) = (sceneObjects[second], sceneObjects[first]);
    }
}

[JsonDerivedType(typeof(SceneObject), nameof(SceneObject))]
[JsonDerivedType(typeof(CabinPathGizmo), nameof(CabinPathGizmo))]
[JsonDerivedType(typeof(Camera), nameof(Camera))]
[JsonDerivedType(typeof(CFGObject), nameof(CFGObject))]
[JsonDerivedType(typeof(GenericSplineMesh), nameof(GenericSplineMesh))]
[JsonDerivedType(typeof(GizmoMesh), nameof(GizmoMesh))]
[JsonDerivedType(typeof(Light), nameof(Light))]
[JsonDerivedType(typeof(MapTileObject), nameof(MapTileObject))]
[JsonDerivedType(typeof(Mesh), nameof(Mesh))]
[JsonDerivedType(typeof(O3DMesh), nameof(O3DMesh))]
[JsonDerivedType(typeof(PathMesh), nameof(PathMesh))]
//[JsonDerivedType(typeof(SDFObject), nameof(SDFObject))]
[JsonDerivedType(typeof(SplineMesh), nameof(SplineMesh))]
[JsonDerivedType(typeof(PseudoObject), nameof(PseudoObject))]
[JsonDerivedType(typeof(LODObject), nameof(LODObject))]
[JsonDerivedType(typeof(CTIObject), nameof(CTIObject))]
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
public class SceneObject
{
    public Transform transform = new();
    public string name = "SceneObj";
    private SceneObject? parent;
    public List<SceneObject>? children;
    protected Bounds objBounds;

    public Bounds Bounds => objBounds;

    [JsonIgnore]
    public SceneObject? Parent
    {
        get => parent;
        internal set
        {
            // TODO: Having this setter here ignores many checks that SceneGraph would normally do which could break the scene.
            parent = value;
            transform.Parent = value?.transform;
        }
    }

    public SceneObject()
    {
        transform.TransformChanged += _ => UpdateBounds();
    }

    public SceneObject(SceneObject other) : this()
    {
        transform = new(other.transform);
        name = other.name + " - Clone";
        parent = other.Parent;
        children = null;//other.children;
        objBounds = other.objBounds;
    }

    [JsonConstructor]
    public SceneObject(Transform transform, string name, List<SceneObject>? children, Bounds bounds) : this()
    {
        this.transform = transform;
        this.name = name;
        this.children = children;
        objBounds = bounds;
        if (children != null)
        {
            //UpdateBounds();
            foreach (var child in children)
                child.Parent = this;
        }
    }

    public virtual SceneObject Clone()
    {
        return new(this);
    }

    internal void OnChildrenChanged(SceneObject? child, bool added)
    {
        if (added)
        {
            if (child != null)
                objBounds = objBounds.Union(child.Bounds);
        }
        else
        {
            UpdateBounds();
        }
    }

    public void UpdateBounds()
    {
        if (children != null && children.Count > 0)
            objBounds = children.Select(x => x.Bounds).Aggregate((last, next) => last.Union(next));
        else
            objBounds = new(transform.Pos, System.Numerics.Vector3.One * 0.5f);
    }

    internal bool RemoveChild(SceneObject sceneObj)
    {
        if (children == null)
            return false;

        if (children.Remove(sceneObj))
        {
            OnChildrenChanged(sceneObj, false);
            return true;
        }

        foreach (var child in children)
            if (child.RemoveChild(sceneObj))
            {
                OnChildrenChanged(sceneObj, false);
                return true;
            }

        return false;
    }
}

public class PseudoObject : SceneObject
{
    public object? metadata;
    public event Action<object?, bool>? OnModified;

    public PseudoObject()
    {

    }

    public PseudoObject(string name, object? metadata = null)
    {
        this.name = name;
        this.metadata = metadata;
    }

    // Called by the inspector
    internal void OnModify(object? ret, bool deleted)
    {
        OnModified?.Invoke(ret, deleted);
    }
}

public class LODObject : PseudoObject
{
    public LODCommand[] lodCommands;
    public LODCommand lodCommand;
    public bool visible;

    public LODObject(LODCommand command, LODCommand[] parentLods)
    {
        lodCommands = parentLods;
        lodCommand = command;
        metadata = command;
        name = $"LOD {command.viewRadius}";
        visible = true;
    }
}

public class CTIObject : PseudoObject
{
    public OmsiCTIFile cti;
    public bool active;
    public readonly Dictionary<string, ItemCommand[]> colourSchemes;
    public readonly string basePath;

    public CTIObject(OmsiCTIFile cti, string name, string basePath)
    {
        this.cti = cti;
        metadata = cti;
        this.name = $"CTI - {name}";
        active = true;
        colourSchemes = [];
        this.basePath = basePath;
        UpdateColourSchemes();
    }

    public void UpdateColourSchemes()
    {
        colourSchemes.Clear();
        if (cti.items == null)
            return;
        foreach(var grouping in cti.items.GroupBy(x => x.name))
        {
            colourSchemes.Add(grouping.Key, grouping.ToArray());
        }
    }
}

public class CFGObject : SceneObject
{
    public OmsiCFGFile cfg;
    public string path;

    public readonly ReadOnlyDictionary<string, CTCTextureCommand> ctcTextureNames;
    public readonly List<Material> ctcControlledMaterials;

    internal readonly Dictionary<string, CTCTextureCommand> ctcTextureNamesInternal;

    public CFGObject(OmsiCFGFile cfg, string path)
    {
        this.cfg = cfg;
        this.path = path;
        ctcTextureNamesInternal = [];
        ctcTextureNames = new(ctcTextureNamesInternal);
        ctcControlledMaterials = [];

        if (cfg.ctc?.textures != null)
        {
            foreach (var ctc in cfg.ctc.textures)
            {
                //ctcTextureNames.TryAdd(ctc.name, ctc);
                ctcTextureNamesInternal.TryAdd(ctc.name, ctc);
            }
        }
    }
}

public class MapTileObject(OmsiMapTile map, string path) : SceneObject
{
    public OmsiMapTile map = map;
    public string path = path;
}
