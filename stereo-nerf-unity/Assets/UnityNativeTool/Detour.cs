//
// Following file is essentially copy of Memory.cs file from https://github.com/pardeike/Harmony
//

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace UnityNativeTool.Internal
{
    /// <summary>A low level memory helper</summary>
    internal static class Detour
    {
        static readonly HashSet<PlatformID> WindowsPlatformIDSet = new HashSet<PlatformID>
        {
            PlatformID.Win32NT, PlatformID.Win32S, PlatformID.Win32Windows, PlatformID.WinCE
        };

        /// <summary>Is current environment Windows?</summary>
        /// <value>True if it is Windows</value>
        ///
        internal static bool IsWindows => WindowsPlatformIDSet.Contains(Environment.OSVersion.Platform);

        /// <summary>Unprotect memory pages</summary>
        /// <param name="memory">The starting memory address</param>
        /// <param name="size">The length of memory block</param>
        ///
        internal static void UnprotectMemory(long memory, ulong size)
        {
            if (IsWindows)
            {
                var success = PInvokes_Windows.VirtualProtect(new IntPtr(memory), new UIntPtr(size), PInvokes_Windows.Protection.PAGE_EXECUTE_READWRITE, out var _);
                if (!success)
                    throw new System.ComponentModel.Win32Exception();
            }
        }

        internal static void FlushCode(long memory, ulong size)
        {
            if (IsWindows)
            {
                var processHandle = PInvokes_Windows.GetCurrentProcess();
                var success = PInvokes_Windows.FlushInstructionCache(processHandle, new IntPtr(memory), new UIntPtr(size));
                if (!success)
                    throw new System.ComponentModel.Win32Exception();
            }
        }

        /// <summary>Mark method for no inlining</summary>
        /// <param name="method">The method to change</param>
        unsafe public static void MarkForNoInlining(MethodBase method)
        {
            // TODO for now, this only works on mono
            if (Type.GetType("Mono.Runtime") != null)
            {
                var iflags = (ushort*)(method.MethodHandle.Value) + 1;
                *iflags |= (ushort)MethodImplOptions.NoInlining;
            }
        }

        /// <summary>Detours a method</summary>
        /// <param name="original">The original method</param>
        /// <param name="replacement">The replacement method</param>
        /// <returns>An error string</returns>
        ///
        public unsafe static string DetourMethod(MethodBase original, MethodBase replacement)
        {
            var originalCodeStart = GetMethodStart(original, out var exception);
            if (originalCodeStart == 0)
                return exception.Message;
            var patchCodeStart = GetMethodStart(replacement, out exception);
            if (patchCodeStart == 0)
                return exception.Message;

            return WriteJump(originalCodeStart, patchCodeStart);
        }

        /// <summary>Writes a jump to memory</summary>
        /// <param name="memory">The memory address</param>
        /// <param name="destination">Jump destination</param>
        /// <returns>An error string</returns>
        ///
        public static string WriteJump(long memory, long destination)
        {
            UnprotectMemory(memory, IntPtr.Size == sizeof(long) ? 12u : 6u);

            long writtenMemoryAddress;
            if (IntPtr.Size == sizeof(long))
            {
                if (CompareBytes(memory, new byte[] { 0xe9 }))
                {
                    var offset = ReadInt(memory + 1);
                    memory += 5 + offset;
                    UnprotectMemory(memory, 12);
                }

                writtenMemoryAddress = memory;
                memory = WriteBytes(memory, new byte[] { 0x48, 0xB8 });
                memory = WriteLong(memory, destination);
                memory = WriteBytes(memory, new byte[] { 0xFF, 0xE0 });
            }
            else
            {
                writtenMemoryAddress = memory;
                memory = WriteByte(memory, 0x68);
                memory = WriteInt(memory, (int)destination);
                memory = WriteByte(memory, 0xc3);
            }

            FlushCode(writtenMemoryAddress, IntPtr.Size == sizeof(long) ? 12u : 6u);
            return null;
        }

        static RuntimeMethodHandle GetRuntimeMethodHandle(MethodBase method)
        {
            if (method is DynamicMethod)
            {
                var noninternalInstance = BindingFlags.NonPublic | BindingFlags.Instance;

                // DynamicMethod actually generates its m_methodHandle on-the-fly and therefore
                // we should call GetMethodDescriptor to force it to be created
                //
                var m_GetMethodDescriptor = typeof(DynamicMethod).GetMethod("GetMethodDescriptor", noninternalInstance);
                if (m_GetMethodDescriptor != null)
                    return (RuntimeMethodHandle)m_GetMethodDescriptor.Invoke(method, new object[0]);

                // .Net Core
                var f_m_method = typeof(DynamicMethod).GetField("m_method", noninternalInstance);
                if (f_m_method != null)
                    return (RuntimeMethodHandle)f_m_method.GetValue(method);

                // Mono
                var f_mhandle = typeof(DynamicMethod).GetField("mhandle", noninternalInstance);
                return (RuntimeMethodHandle)f_mhandle.GetValue(method);
            }

            return method.MethodHandle;
        }

        /// <summary>Gets the start of a method in memory</summary>
        /// <param name="method">The method</param>
        /// <param name="exception">[out] Details of the exception</param>
        /// <returns>The method start address</returns>
        ///
        public static long GetMethodStart(MethodBase method, out Exception exception)
        {
            // required in .NET Core so that the method is JITed and the method start does not change
            //
            var handle = GetRuntimeMethodHandle(method);
            try
            {
                RuntimeHelpers.PrepareMethod(handle);
            }
            catch (Exception)
            {
            }

            try
            {
                exception = null;
                return handle.GetFunctionPointer().ToInt64();
            }
            catch (Exception ex)
            {
                exception = ex;
                return 0;
            }
        }

        /// <summary>Compare bytes</summary>
        /// <param name="memory">The memory address</param>
        /// <param name="values">The bytes to compare to</param>
        /// <returns>True if memory address contains the bytes</returns>
        ///
        internal static unsafe bool CompareBytes(long memory, byte[] values)
        {
            var p = (byte*)memory;
            foreach (var value in values)
            {
                if (value != *p) return false;
                p++;
            }
            return true;
        }

        /// <summary>Reads a byte</summary>
        /// <param name="memory">The memory address</param>
        /// <returns>The byte</returns>
        ///
        internal static unsafe byte ReadByte(long memory)
        {
            var p = (byte*)memory;
            return *p;
        }

        /// <summary>Reads an int</summary>
        /// <param name="memory">The memory address</param>
        /// <returns>The int</returns>
        ///
        internal static unsafe int ReadInt(long memory)
        {
            var p = (int*)memory;
            return *p;
        }

        /// <summary>Reads a long</summary>
        /// <param name="memory">The memory address</param>
        /// <returns>The long</returns>
        ///
        internal static unsafe long ReadLong(long memory)
        {
            var p = (long*)memory;
            return *p;
        }

        /// <summary>Writes a byte</summary>
        /// <param name="memory">The memory address</param>
        /// <param name="value">The byte</param>
        /// <returns>Advanced memory address</returns>
        ///
        internal static unsafe long WriteByte(long memory, byte value)
        {
            var p = (byte*)memory;
            *p = value;
            return memory + sizeof(byte);
        }

        /// <summary>Writes some bytes</summary>
        /// <param name="memory">The memory address</param>
        /// <param name="values">The bytes to write</param>
        /// <returns>Advanced memory address</returns>
        ///
        internal static unsafe long WriteBytes(long memory, byte[] values)
        {
            foreach (var value in values)
                memory = WriteByte(memory, value);
            return memory;
        }

        /// <summary>Writes an int</summary>
        /// <param name="memory">The memory address</param>
        /// <param name="value">The int</param>
        /// <returns>Advanced memory address</returns>
        ///
        internal static unsafe long WriteInt(long memory, int value)
        {
            var p = (int*)memory;
            *p = value;
            return memory + sizeof(int);
        }

        /// <summary>Writes a long</summary>
        /// <param name="memory">The memory address</param>
        /// <param name="value"> The long</param>
        /// <returns>Advanced memory address</returns>
        ///
        internal static unsafe long WriteLong(long memory, long value)
        {
            var p = (long*)memory;
            *p = value;
            return memory + sizeof(long);
        }
    }
}