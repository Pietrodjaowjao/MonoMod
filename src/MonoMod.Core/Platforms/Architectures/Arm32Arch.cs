using MonoMod.Core.Utils;
using MonoMod.Utils;
using System;
using System.Collections.Generic;

namespace MonoMod.Core.Platforms.Architectures
{
    internal sealed class Arm32Arch : IArchitecture
    {
        public ArchitectureKind Target => ArchitectureKind.Arm64;

        public ArchitectureFeature Features => ArchitectureFeature.FixedInstructionSize;

        private BytePatternCollection? lazyKnownMethodThunks;
        public unsafe BytePatternCollection KnownMethodThunks => Helpers.GetOrInit(ref lazyKnownMethodThunks, &CreateKnownMethodThunks);

        public IAltEntryFactory AltEntryFactory => throw new NotImplementedException();

        private static BytePatternCollection CreateKnownMethodThunks()
        {
            const ushort An = BytePattern.SAnyValue;
            const ushort Ad = BytePattern.SAddressValue;
            // const byte Bn = BytePattern.BAnyValue;
            // const byte Bd = BytePattern.BAddressValue;

            ushort[] armInsts(params uint[] pattern)
            {
                var smallPattern = new List<ushort>();

                foreach (var item in pattern)
                {
                    if (item <= ushort.MaxValue)
                    {
                        smallPattern.Add((ushort)item);
                    }
                    else
                    {
                        smallPattern.Add((ushort)((item >> 0) & 0xFF));
                        smallPattern.Add((ushort)((item >> 8) & 0xFF));
                        smallPattern.Add((ushort)((item >> 16) & 0xFF));
                        smallPattern.Add((ushort)((item >> 24) & 0xFF));
                    }
                }

                return smallPattern.ToArray();
            }

            if (true)
            {
                return new BytePatternCollection(
                // .NET Core 6
                // Adapted from https://github.com/MonoMod/MonoMod.Common/blob/7d2819f3b2309a3127f5ff7b1b9d91a0908eece0/RuntimeDetour/Platforms/Runtime/DetourRuntimeNETPlatform.cs#L153-L201

                    // StubPrecode
                    // https://github.com/dotnet/runtime/blob/7830fddeead7907f6dd45f814fc3b8d49cd4b082/src/coreclr/vm/arm64/cgencpu.h#L567-L572
                    new(new(AddressKind.Abs32), mustMatchAtStart: true, armInsts(
                        0xE28F9004, // add r9, pc, #4  (pc em ARM32 já está adiantado)
                        0xE599A000, // ldr r10, [r9]
                        0xE599C004, // ldr r12, [r9, #4]
                        0xE12FFF1A, // bx r10
                        Ad, Ad, Ad, Ad, Ad, Ad, Ad, Ad
                    )),


                    // NDirectImportPrecode
                    // https://github.com/dotnet/runtime/blob/7830fddeead7907f6dd45f814fc3b8d49cd4b082/src/coreclr/vm/arm64/cgencpu.h#L628-L633
                    new(new(AddressKind.Abs32), mustMatchAtStart: true, armInsts(
                        0xE28FB004, // add r11, pc, #4
                        0xE5DBA000, // ldr r10, [r11]
                        0xd61f0140, // ldr r12, [r11, #4]
                        0xE12FFF1A, // bx r10
                        Ad, Ad, Ad, Ad, Ad, Ad, Ad, Ad
                    )),

                    // FixupPrecode
                    // https://github.com/dotnet/runtime/blob/7830fddeead7907f6dd45f814fc3b8d49cd4b082/src/coreclr/vm/arm64/cgencpu.h#L666-L672
                    new(new(AddressKind.Abs32), mustMatchAtStart: true, armInsts(
                        0xE59FB000, // ldr r11, [pc, #0]
                        0xE12FFF1B, // bx r11
                        Ad, Ad, Ad, Ad
                    )),

                    // ThisPtrRetBufPrecode
                    // https://github.com/dotnet/runtime/blob/4da6b9a8d55913c0ea560d63590d35dc942425be/src/coreclr/vm/arm64/stubs.cpp#L641-L647
                    new(new(AddressKind.Abs32), mustMatchAtStart: true, armInsts(
                        0xE1A0C000, // mov r12, r0
                        0xE1A00001, // mov r0, r1
                        0xE1A0100C, // mov r1, r12
                        0xE59F3000, // ldr r3, [pc, #0]
                        0xE12FFF13, // bx r3
                        Ad, Ad, Ad, Ad
                    )),

                    // .NET Core 8

                    // FixupPrecode main entry point
                    // https://github.com/dotnet/runtime/blob/17d88ed72da4b70821159169499a805b87739ee6/src/coreclr/vm/arm64/thunktemplates.S#L17-L23
                    new(new(AddressKind.Rel32 | AddressKind.Constant | AddressKind.Indirect, relativeOffset: 0, constantValue: 0x4000), mustMatchAtStart: true, armInsts(
                        0xE59FB000, // ldr r11, [pc, #0]
                        0xE12FFF1B, // bx r11
                        Ad, Ad, Ad, Ad
                    )),

                    // FixupPrecode ThePreStub entry point
                    // Same as above, except that the method hasn't been compiled yet so we jumped to the last three instructions
                    // https://github.com/dotnet/runtime/blob/17d88ed72da4b70821159169499a805b87739ee6/src/coreclr/vm/arm64/thunktemplates.S#L17-L23
                    new(new(AddressKind.PrecodeFixupThunkRel32 | AddressKind.Constant | AddressKind.Indirect, relativeOffset: 0, constantValue: 0x4008), mustMatchAtStart: true, armInsts(
                        0xE59FC000, // ldr r12, [pc, #0]
                        0xE59FB004, // ldr r11, [pc, #4]
                        0xE12FFF1B, // bx r11
                        Ad, Ad, Ad, Ad,
                        Ad, Ad, Ad, Ad
                    )),

                    // CallCountingStub
                    // https://github.com/dotnet/runtime/blob/17d88ed72da4b70821159169499a805b87739ee6/src/coreclr/vm/arm64/thunktemplates.S#L25-L37
                    new(new(AddressKind.Rel32 | AddressKind.Constant | AddressKind.Indirect, relativeOffset: 0, constantValue: 0x4008), mustMatchAtStart: true, armInsts(
                        0xE59F9000, // ldr r9, [pc, #0]
                        0xE1D9A0B0, // ldrh r10, [r9]
                        0xE250A001, // subs r10, r10, #1
                        0xE1C9A0B0, // strh r10, [r9]
                        0x0A000001, // beq +8 (salta próxima instr se zero)
                        0xE59F9004, // ldr r9, [pc, #4]
                        0xE12FFF19, // bx r9
                        Ad, Ad, Ad, Ad,
                        Ad, Ad, Ad, Ad
                    ))
                );
            }
        }

        private sealed class Abs32Kind : DetourKindBase
        {
            public static readonly Abs32Kind Instance = new();

            public override int Size => 4 + 4 + 4;

            public override int GetBytes(IntPtr from, IntPtr to, Span<byte> buffer, object? data, out IDisposable? allocHandle)
            {
                /*
                * ARM Thumb not considered here; this assumes ARM state.
                *
                * ldr r12, [pc, #0] ; PC is 8 bytes ahead, so this fetches the address just after these instructions
                * bx  r12           ; Branch to r12
                * <target>          ; 32-bit absolute address to jump to
                */
                Unsafe.WriteUnaligned(ref buffer[0], 0xE59FC000); // ldr r12, [pc, #0]
                Unsafe.WriteUnaligned(ref buffer[4], 0xE12FFF1C); // bx r12
                Unsafe.WriteUnaligned(ref buffer[8], (uint)to.ToInt32());

                allocHandle = null;
                return Size;
            }

            public override bool TryGetRetargetInfo(NativeDetourInfo orig, IntPtr to, int maxSize, out NativeDetourInfo retargetInfo)
            {
                // we can always trivially retarget an abs64 detour (change the absolute constant)
                retargetInfo = orig with { To = to };
                return true;
            }

            public override int DoRetarget(NativeDetourInfo origInfo, IntPtr to, Span<byte> buffer, object? data,
                out IDisposable? allocationHandle, out bool needsRepatch, out bool disposeOldAlloc)
            {
                needsRepatch = true;
                disposeOldAlloc = true;

                return GetBytes(origInfo.From, to, buffer, data, out allocationHandle);
            }
        }

        private readonly ISystem system;

        public Arm32Arch(ISystem system)
        {
            this.system = system;
        }

        public NativeDetourInfo ComputeDetourInfo(IntPtr from, IntPtr target, int maxSizeHint = -1)
        {
            if (maxSizeHint < 0)
            {
                maxSizeHint = int.MaxValue;
            }

            if (maxSizeHint < Abs32Kind.Instance.Size)
            {
                MMDbgLog.Warning($"Size too small for all known detour kinds; defaulting to Abs64. provided size: {maxSizeHint}");
            }

            return new(from, target, Abs32Kind.Instance, null);
        }

        public int GetDetourBytes(NativeDetourInfo info, Span<byte> buffer, out IDisposable? allocHandle)
        {
            return DetourKindBase.GetDetourBytes(info, buffer, out allocHandle);
        }

        public NativeDetourInfo ComputeRetargetInfo(NativeDetourInfo detour, IntPtr to, int maxSizeHint = -1)
        {
            if (DetourKindBase.TryFindRetargetInfo(detour, to, maxSizeHint, out var retarget))
            {
                // the detour knows how to retarget itself, we'll use that
                return retarget;
            }
            else
            {
                // the detour doesn't know how to retarget itself, lets just compute a new detour to our new target
                return ComputeDetourInfo(detour.From, to, maxSizeHint);
            }
        }

        public int GetRetargetBytes(NativeDetourInfo original, NativeDetourInfo retarget, Span<byte> buffer,
            out IDisposable? allocationHandle, out bool needsRepatch, out bool disposeOldAlloc)
        {
            return DetourKindBase.DoRetarget(original, retarget, buffer, out allocationHandle, out needsRepatch, out disposeOldAlloc);
        }

        public ReadOnlyMemory<IAllocatedMemory> CreateNativeVtableProxyStubs(IntPtr vtableBase, int vtableSize)
        {
            throw new NotImplementedException();
        }

        public IAllocatedMemory CreateSpecialEntryStub(IntPtr target, IntPtr argument)
        {
            throw new NotImplementedException();
        }
    }
}