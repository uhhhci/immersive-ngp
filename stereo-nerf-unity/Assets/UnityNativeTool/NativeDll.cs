using System;
using System.Collections.Generic;

namespace UnityNativeTool
{
    public class NativeDll
    {
        public readonly string name;
        public string path;
        public IntPtr handle = IntPtr.Zero;
        public bool loadingError = false;
        public bool symbolError = false;
        public readonly List<NativeFunction> functions = new List<NativeFunction>();

        public NativeDll(string name, string path)
        {
            this.name = name;
            this.path = path;
        }

        public void ResetAsUnloaded()
        {
            handle = IntPtr.Zero;
            loadingError = false;
            symbolError = false;

            foreach (var func in functions)
            {
                func.@delegate = null;
            }
        }
    }
}

namespace UnityNativeTool.Internal
{
    public class NativeDllInfo
    {
        public string name;
        public string path;
        public bool isLoaded;
        public bool loadingError;
        public bool symbolError;
        public IList<string> loadedFunctions;

        public NativeDllInfo(string name, string path, bool isLoaded, bool loadingError, bool symbolError, IList<string> loadedFunctions)
        {
            this.name = name;
            this.path = path;
            this.isLoaded = isLoaded;
            this.loadingError = loadingError;
            this.symbolError = symbolError;
            this.loadedFunctions = loadedFunctions;
        }
    }
}