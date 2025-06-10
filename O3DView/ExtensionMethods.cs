using O3DParse;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using StbImageSharp;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace O3DView
{
    public static class ExtensionMethods
    {
        public static Matrix4x4 SetTranslation(this Matrix4x4 mat, Vector3 trans)
        {
            mat.M41 = trans.X;
            mat.M42 = trans.Y;
            mat.M43 = trans.Z;
            return mat;
        }

        [Flags]
        public enum ModifierKeys
        {
            None = 0,
            Ctrl = 1,
            Shift = 2,
            Alt = 4,
            Win = 8
        }

        public static bool IsModifierKeyDown(this IKeyboard kb, ModifierKeys keys)
        {
            ModifierKeys pressed = ModifierKeys.None;
            pressed |= (kb.IsKeyPressed(Key.ControlLeft) || kb.IsKeyPressed(Key.ControlRight)) ? ModifierKeys.Ctrl : ModifierKeys.None;
            pressed |= (kb.IsKeyPressed(Key.ShiftLeft) || kb.IsKeyPressed(Key.ShiftRight)) ? ModifierKeys.Shift : ModifierKeys.None;
            pressed |= (kb.IsKeyPressed(Key.AltLeft) || kb.IsKeyPressed(Key.AltRight)) ? ModifierKeys.Alt : ModifierKeys.None;
            pressed |= (kb.IsKeyPressed(Key.SuperLeft) || kb.IsKeyPressed(Key.SuperRight)) ? ModifierKeys.Win : ModifierKeys.None;

            return pressed == keys;
        }

        public static (PixelFormat pxFmt, InternalFormat intFmt, PixelType pxType) ToOpenGLFormat(this ColorComponents components)
        {
            switch(components)
            {
                case ColorComponents.Grey:
                    return (PixelFormat.Red, InternalFormat.R8, PixelType.UnsignedByte);
                case ColorComponents.GreyAlpha:
                    return (PixelFormat.RG, InternalFormat.RG8, PixelType.UnsignedByte);
                case ColorComponents.RedGreenBlue:
                    return (PixelFormat.Rgb, InternalFormat.Rgb8, PixelType.UnsignedByte);
                case ColorComponents.RedGreenBlueAlpha:
                    return (PixelFormat.Rgba, InternalFormat.Rgba8, PixelType.UnsignedByte);
                case ColorComponents.Default:
                default:
                    throw new Exception("Unknown pixel format 'Default'!");
            }
        }

        public static Vector3 XZY(this Vector3 value) => new(value.X, value.Z, value.Y);
        public static Vector3 XiZY(this Vector3 value) => new(-value.X, value.Z, value.Y);
        public static Vector3 YXZ(this Vector3 value) => new(value.Y, value.X, value.Z);
        public static Vector3 ZYX(this Vector3 value) => new(value.Z, value.Y, value.X);

        public static Vector4 XZYW(this Vector4 value) => new(value.X, value.Z, value.Y, value.W);
        public static Vector4 YXZW(this Vector4 value) => new(value.Y, value.X, value.Z, value.W);
        public static Vector4 ZYXW(this Vector4 value) => new(value.Z, value.Y, value.X, value.W);
        public static Vector4 WZYX(this Vector4 value) => new(value.W, value.Z, value.Y, value.X);

        /// <summary>
        /// Constructs a copy of the given array without the item at the given index.
        /// </summary>
        /// <param name="arr"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static Array Without(this Array arr, int index)
        {
            var ret = Array.CreateInstance(arr.GetType().GetElementType()!, arr.Length - 1);
            if (index > 0)
                Array.Copy(arr, ret, index);
            if (index < arr.Length - 1)
                Array.Copy(arr, index + 1, ret, index, arr.Length - index - 1);
            return ret;
        }

        /// <summary>
        /// Constructs a copy of the given array with n new items at the end.
        /// </summary>
        /// <param name="arr"></param>
        /// <param name="toAdd"></param>
        /// <returns></returns>
        public static Array Grow(this Array arr, int toAdd = 1)
        {
            var ret = Array.CreateInstance(arr.GetType().GetElementType()!, arr.Length + toAdd);
            Array.Copy(arr, ret, arr.Length);
            return ret;
        }
    }
}
