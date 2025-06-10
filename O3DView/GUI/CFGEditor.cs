using ImGuiNET;
using O3DParse.Ini;
using O3DParse;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;

namespace O3DView.GUI;

internal static class CFGEditor
{
    internal struct CFGFieldCache
    {
        public string name;
        public FieldInfo? field;
        public Type type;
        public CFGFieldCache[]? children;
        public bool isArray;
        public bool isNullable;
        public bool hasNullableChildren;
        public FieldUIType uiType;
        public (string name, int val)[]? enumNames;
    }

    internal enum FieldUIType
    {
        None,
        String,
        Bool,
        Byte,
        Int,
        UInt,
        Float,
        Vector2,
        Vector3,
        Vector4,
        ColourRGB,
        ColourRGBA,
        Enum
    }

    internal static FieldUIType DetermineUIType(Type type)
    {
        if (type.IsEnum)
            return FieldUIType.Enum;
        if (type == typeof(bool))
            return FieldUIType.Bool;
        else if (type == typeof(byte))
            return FieldUIType.Byte;
        else if (type == typeof(sbyte))
            return FieldUIType.Int;
        else if (type == typeof(char))
            return FieldUIType.String;
        else if (type == typeof(decimal))
            return FieldUIType.Float;
        else if (type == typeof(double))
            return FieldUIType.Float;
        else if (type == typeof(float))
            return FieldUIType.Float;
        else if (type == typeof(int))
            return FieldUIType.Int;
        else if (type == typeof(uint))
            return FieldUIType.UInt;
        else if (type == typeof(nint))
            return FieldUIType.UInt;
        else if (type == typeof(long))
            return FieldUIType.Int;
        else if (type == typeof(ulong))
            return FieldUIType.UInt;
        else if (type == typeof(short))
            return FieldUIType.Int;
        else if (type == typeof(ushort))
            return FieldUIType.UInt;
        else if (type == typeof(string))
            return FieldUIType.String;
        else if (type == typeof(Vector2))
            return FieldUIType.Vector2;
        else if (type == typeof(Vector3))
            return FieldUIType.Vector3;
        else if (type == typeof(Vector4))
            return FieldUIType.Vector4;
        else if (type == typeof(RGBColour))
            return FieldUIType.ColourRGB;
        else if (type == typeof(RGBAColour))
            return FieldUIType.ColourRGBA;

        return FieldUIType.None;
    }

    internal static string PrettyPrintName(string name)
    {
        StringBuilder sb = new(name.Length + 4);
        char prev = '\0';
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];

            if (i > 0 && char.IsUpper(c) && !char.IsUpper(prev))
                sb.Append(' ');
            else if (i > 0 && char.IsNumber(c) && !char.IsNumber(prev))
                sb.Append(' ');

            if (c == '_')
                c = ' ';

            if (i == 0)
                c = char.ToUpperInvariant(c);

            sb.Append(c);
            prev = c;
        }

        return sb.ToString();
    }

    internal static CFGFieldCache BuildCFGFieldCache(Type type, FieldInfo? field = null, int recurseDepth = 0)
    {
        CFGFieldCache cache = new();
        var unitType = type;
        if (Nullable.GetUnderlyingType(unitType) is Type nullableType)
        {
            unitType = nullableType;
            cache.isNullable = true;
        }
        if (unitType?.IsArray ?? false)
        {
            unitType = unitType.GetElementType();
            cache.isArray = true;
        }
        if (unitType == null)
            return cache;

        cache.name = PrettyPrintName(field?.Name ?? unitType.Name);
        cache.uiType = DetermineUIType(unitType);
        cache.type = unitType;
        cache.field = field;
        if (cache.uiType == FieldUIType.Enum)
        {
            var enumMembers = unitType.GetFields();
            var vals = new (string name, int val)[enumMembers.Length - 1];
            int i = 0;
            var enumType = Enum.GetUnderlyingType(unitType);
            foreach (var member in enumMembers)
            {
                if (member.Name.Equals("value__")) 
                    continue;
                //names.Add(member.Name);
                vals[i++] = (PrettyPrintName(member.Name), Convert.ToInt32(member.GetRawConstantValue()));
            }
            cache.enumNames = vals;
        }

        if (cache.uiType == FieldUIType.None && recurseDepth < 10)
        {
            var fields = unitType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            cache.children = fields.Where(c =>
            {
                return c.GetCustomAttribute<OmsiIniCommentsAttribute>() == null
                    && c.GetCustomAttribute<OmsiIniCommandFileAttribute>() == null;
            }).OrderBy(c => c.Name)
            .Select(c =>
            {
                return BuildCFGFieldCache(c.FieldType, c, recurseDepth + 1);
            }).ToArray();
            cache.hasNullableChildren = cache.children.Any(x => x.isNullable);
        }

        return cache;
    }

    internal static (object? ret, bool deleted) DrawCFGFieldCache(object? obj, CFGFieldCache fields)
    {
        var ret = DrawCFGFieldCacheInternal(obj, fields);
        //if (ret.ret != null || ret.deleted)
        //    obj = ret.ret;
        return ret;
    }

    internal static (object? ret, bool deleted) DrawCFGFieldCacheInternal(object? obj, CFGFieldCache fields)
    {
        if (obj == null)
            return (null, false);

        if (fields.uiType != FieldUIType.None && fields.field != null)
        {
            if (obj.GetType().IsArray)
            {
                var arr = (Array)obj;
                ImGui.AlignTextToFramePadding();
                ImGui.SetNextItemAllowOverlap();
                bool open = ImGui.TreeNode($"{fields.name} ({arr.Length})");

                ImGui.SameLine();
                if (ImGui.Button("X"))
                {
                    return (null, true);
                }

                (object? ret, bool deleted) ret = (null, false);
                if (open)
                {
                    for (int i = 0; i < arr.Length; i++)
                    {
                        var itemRet = DrawSimpleField(arr.GetValue(i)!, $"{fields.name}[{i}]", fields, true);
                        if (itemRet.deleted)
                        {
                            ret.ret = arr.Without(i);
                        } 
                        else if (itemRet.ret != null)
                        {
                            arr.SetValue(itemRet.ret, i);
                            ret.ret = obj;
                        }
                    }
                    if (ImGui.Button("+"))
                    { 
                        ret.ret = arr.Grow();
                    }
                    ImGui.TreePop();
                }
                return ret;
            }
            else
            {
                return DrawSimpleField(obj, fields.name, fields, false);
            }
        }
        else
        {
            string label = fields.name;// fields.field != null ? fields.name : fields.type.Name;
            if (obj.GetType().IsArray)
            {
                var arr = (Array)obj;
                ImGui.AlignTextToFramePadding();
                ImGui.SetNextItemAllowOverlap();
                bool open = ImGui.TreeNode($"{label} ({arr.Length})");

                ImGui.SameLine();
                if (ImGui.Button("X"))
                { 
                    return (null, true);
                }

                (object? ret, bool deleted) ret = (null, false);
                if (open)
                {
                    for (int i = 0; i < arr.Length; i++)
                    {
                        var val = arr.GetValue(i);
                        var itemRet = DrawSubTree(val, fields, $"{label}[{i}]", true);
                        if (itemRet.deleted)
                        {
                            ret.ret = arr.Without(i);
                        } 
                        else if (itemRet.ret != null)
                        {
                            arr.SetValue(itemRet.ret, i);
                            ret.ret = obj;
                        }
                    }
                    if (ImGui.Button("+"))
                    {
                        ret.ret = arr.Grow();
                    }
                    ImGui.TreePop();
                }

                return ret;
            }
            else
            {
                return DrawSubTree(obj, fields, label, true);
            }
        }

        static (object? ret, bool deleted) DrawSubTree(object? obj, CFGFieldCache fields, string label, bool canDelete)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.SetNextItemAllowOverlap();
            bool leaf = fields.children == null || fields.children.Length == 0;
            bool open = ImGui.TreeNodeEx(label, leaf ? ImGuiTreeNodeFlags.Leaf : ImGuiTreeNodeFlags.None);
            object? ret = null;

            if (canDelete)
            {
                ImGui.SameLine();
                if (ImGui.Button("X"))
                    return (ret, true);
            }

            if (open)
            {
                if (fields.children != null)
                {
                    foreach (var child in fields.children)
                    {
                        if (child.field == null)
                            continue;
                        var childObj = child.field.GetValue(obj);
                        var childRet = DrawCFGFieldCacheInternal(childObj, child);
                        if (childRet.deleted)
                        {

                        }
                        else if (childRet.ret != null)
                        {
                            child.field.SetValue(obj, childRet.ret);
                            ret = obj;
                        }
                    }

                    if (fields.children.Length > 0 && fields.hasNullableChildren)
                    {
                        ImGui.PushID("Create field");
                        ImGui.SetNextItemWidth(200);
                        if (ImGui.BeginCombo("", "Create"))
                        {
                            foreach (var child in fields.children)
                            {
                                if (child.field?.GetValue(obj) != null)
                                    continue;
                                if (ImGui.Selectable(child.name))
                                {
                                    if (child.isArray)
                                        child.field?.SetValue(obj, Array.CreateInstance(child.type, 1));
                                    else
                                        // TODO: This doesn't initialise any implicitly nulllable fields such as strings and arrays, leaving them un-editable...
                                        child.field?.SetValue(obj, Activator.CreateInstance(child.type));
                                    ret = obj;
                                }
                            }
                            ImGui.EndCombo();
                        }
                        ImGui.PopID();
                    }
                }

                ImGui.TreePop();
            }

            return (ret, false);
        }
    }

    private static (object? ret, bool deleted) DrawSimpleField(object obj, string fieldName, CFGFieldCache fieldInfo, bool canDelete)
    {
        ImGui.SetNextItemWidth(200);
        object? ret = null;
        switch (fieldInfo.uiType)
        {
            case FieldUIType.String:
                string valstr = ((string)obj) ?? string.Empty;
                //ImGui.InputTextMultiline(fieldName, ref valstr, 2048, new Vector2(200, 20),)
                if (ImGui.InputText(fieldName, ref valstr, 2048))
                    ret = valstr;
                break;
            case FieldUIType.Bool:
                bool valb = (bool)obj;
                if (ImGui.Checkbox(fieldName, ref valb))
                    ret = valb;
                break;
            case FieldUIType.Int:
                var vali = unchecked((int)obj);
                if (ImGui.DragInt(fieldName, ref vali))
                    ret = vali;
                break;
            case FieldUIType.UInt:
                var valui = unchecked((int)(uint)obj);
                if (ImGui.DragInt(fieldName, ref valui, 1, 0, int.MaxValue))
                    ret = (uint)valui;
                break;
            case FieldUIType.Byte:
                var valub = unchecked((int)(byte)obj);
                if (ImGui.DragInt(fieldName, ref valub, 1, 0, 255))
                    ret = (byte)valub;
                break;
            case FieldUIType.Float:
                var valf = (float)obj;
                if (ImGui.DragFloat(fieldName, ref valf, 0.1f))
                    ret = valf;
                break;
            case FieldUIType.Vector2:
                var valv2 = (Vector2)obj;
                if (ImGui.DragFloat2(fieldName, ref valv2, 0.1f))
                    ret = valv2;
                break;
            case FieldUIType.Vector3:
                var valv3 = (Vector3)obj;
                if (ImGui.DragFloat3(fieldName, ref valv3, 0.1f))
                    ret = valv3;
                break;
            case FieldUIType.Vector4:
                var valv4 = (Vector4)obj;
                if (ImGui.DragFloat4(fieldName, ref valv4, 0.1f))
                    ret = valv4;
                break;
            case FieldUIType.ColourRGB:
                var valrgb = (RGBColour)obj;
                var valrgbv = valrgb.ToVector3;
                if (ImGui.ColorEdit3(fieldName, ref valrgbv))
                    ret = new RGBColour(valrgbv);
                break;
            case FieldUIType.ColourRGBA:
                var valrgba = (RGBAColour)obj;
                var valrgbav = valrgba.ToVector4;
                if (ImGui.ColorEdit4(fieldName, ref valrgbav))
                    ret = new RGBAColour(valrgbav);
                break;
            case FieldUIType.Enum:
                var valenum = Convert.ToInt32(obj);
                if (ImGui.BeginCombo(fieldName, Enum.GetName(fieldInfo.type, valenum)))
                {
                    foreach (var (name, val) in fieldInfo.enumNames!)
                    {
                        if (ImGui.Selectable(name))
                        {
                            ret = Enum.ToObject(fieldInfo.type, val);
                        }
                    }
                    ImGui.EndCombo();
                }
                break;
        }
        if (canDelete)
        {
            ImGui.SameLine();
            if (ImGui.Button("X"))
                return (ret, true);
        }
        return (ret, false);
    }
}
