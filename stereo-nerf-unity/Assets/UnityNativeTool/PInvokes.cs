using System;
using System.Runtime.InteropServices;

namespace UnityNativeTool.Internal
{
    [DisableMocking]
    internal static class PInvokes_Windows
    {
        /// <summary>A bit-field of flags for protections</summary>
        [Flags]
        internal enum Protection
        {
            /// <summary>No access</summary>
            PAGE_NOACCESS = 0x01,
            /// <summary>Read only</summary>
            PAGE_READONLY = 0x02,
            /// <summary>Read write</summary>
            PAGE_READWRITE = 0x04,
            /// <summary>Write copy</summary>
            PAGE_WRITECOPY = 0x08,
            /// <summary>No access</summary>
            PAGE_EXECUTE = 0x10,
            /// <summary>Execute read</summary>
            PAGE_EXECUTE_READ = 0x20,
            /// <summary>Execute read write</summary>
            PAGE_EXECUTE_READWRITE = 0x40,
            /// <summary>Execute write copy</summary>
            PAGE_EXECUTE_WRITECOPY = 0x80,
            /// <summary>guard</summary>
            PAGE_GUARD = 0x100,
            /// <summary>No cache</summary>
            PAGE_NOCACHE = 0x200,
            /// <summary>Write combine</summary>
            PAGE_WRITECOMBINE = 0x400
        }

        [DllImport("kernel32")]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32")]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32")]
        internal static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, Protection flNewProtect, out Protection lpflOldProtect);

        [DllImport("kernel32")]
        internal static extern bool FlushInstructionCache(IntPtr hProcess, IntPtr lpBaseAddress, UIntPtr dwSize);
    }

    [DisableMocking]
    internal static class PInvokes_Linux
    {
        private const string LIB_DL = "libdl.so";
        private const string LIB_C = "libc.so";

        public const int _SC_PAGE_SIZE = 30;

        [Flags]
        internal enum Prot
        {
            /// <summary>page can be read</summary>
            PROT_READ = 0x1,
            /// <summary>page can be written</summary>
            PROT_WRITE = 0x2,
            /// <summary>page can be executed</summary>
            PROT_EXEC = 0x4,
            /// <summary>page may be used for atomic ops</summary>
            PROT_SEM = 0x8,
            /// <summary>page can not be accessed</summary>
            PROT_NONE = 0x0,
            /// <summary>extend change to start of growsdown vma</summary>
            PROT_GROWSDOWN = 0x01000000,
            /// <summary>extend change to end of growsup vma</summary>
            PROT_GROWSUP = 0x02000000,
        }


        [DllImport(LIB_DL)]
        public static extern IntPtr dlopen(string filename, int flags);

        [DllImport(LIB_DL)]
        public static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport(LIB_DL)]
        public static extern int dlclose(IntPtr handle);

        [DllImport(LIB_C)]
        public static extern int mprotect(IntPtr addr, UIntPtr len, Prot prot);

        [DllImport(LIB_C)]
        public static extern IntPtr sysconf(int name);
    }

    [DisableMocking]
    internal static class PInvokes_Osx
    {
        [DllImport("libdl.dylib")]
        public static extern IntPtr dlopen(string filename, int flags);

        [DllImport("libdl.dylib")]
        public static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport("libdl.dylib")]
        public static extern int dlclose(IntPtr handle);
    }

    public enum PosixDlopenFlags : int
    {
        Lazy = 0x00001,
        Now = 0x00002,
        Lazy_Global = 0x00100 | Lazy,
        Now_Global = 0x00100 | Now
    }
}
