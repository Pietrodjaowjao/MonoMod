using System;

namespace MonoMod.Core.Platforms.Systems
{
    internal static class ArmABI
    {
        public static TypeClassification ClassifyArm64(Type type, bool isReturn)
        {
            // This obviously wrong. However, currently the only place that ClassifyType is used is in PlatformTriple.GetRealDetourTarget
            // to detect if a function has a return buffer. On arm64, the return buffer is always passed through x8, not as a parameter, so no ABI fix is ever needed.
            // For now just always return InRegister to stop PlatformTriple.GetRealDetourTarget from generating abi fixup glue.
            // TODO: Do this properly.
            return TypeClassification.InRegister;
        }

        public static TypeClassification ClassifyArm32(Type type, bool isReturn)
        {
            if (type.IsByRef || type.IsPointer)
            {
                return TypeClassification.ByReference;
            }

            if (type.IsPrimitive || type.IsEnum)
            {
                return TypeClassification.InRegister;
            }

            if (type.IsValueType)
            {
                var size = GetArmCompatibleTypeSize(type);

                // On ARM32, small structs (< 4 or 8 bytes) may be returned in registers
                if (isReturn)
                {
                    return size <= 8 ? TypeClassification.InRegister : TypeClassification.ByReference;
                }

                // For parameters: pass large structs by reference, small ones by value
                return size <= 8 ? TypeClassification.InRegister : TypeClassification.ByReference;
            }

            return TypeClassification.ByReference;
        }

        public static int GetArmCompatibleTypeSize(Type type)
        {
            return type switch
            {
                _ when type == typeof(int) => 4,
                _ when type == typeof(float) => 4,
                _ when type == typeof(double) => 8,
                _ when type == typeof(IntPtr) => IntPtr.Size,
                _ => throw new NotSupportedException($"Can't determine size of {type} without runtime marshalling.")
            };
        }
    }
}