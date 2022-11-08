using System;

namespace UnityNativeTool
{
    public struct NativeFunctionIdentity
    {
        public string symbol;
        public string containingDllName;

        public NativeFunctionIdentity(string symbol, string containingDllName)
        {
            this.symbol = symbol;
            this.containingDllName = containingDllName;
        }

        public override bool Equals(object obj)
        {
            if (obj is NativeFunctionIdentity other)
                return symbol == other.symbol && containingDllName == other.containingDllName;

            return false;
        }

        public override int GetHashCode()
        {
            int h1 = symbol.GetHashCode();
            int h2 = containingDllName.GetHashCode();
            uint num = (uint)((h1 << 5) | (int)((uint)h1 >> 27));
            return ((int)num + h1) ^ h2;
        }
    }

    public class NativeFunction
    {
        public readonly NativeFunctionIdentity identity;
        public NativeDll containingDll;
        public Type delegateType = null;
        public Delegate @delegate = null;

        public NativeFunction(string symbol, NativeDll containingDll)
        {
            this.identity = new NativeFunctionIdentity(symbol, containingDll.name);
            this.containingDll = containingDll;
        }
    }
}
