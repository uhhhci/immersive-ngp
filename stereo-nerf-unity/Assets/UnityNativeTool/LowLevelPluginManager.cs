﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Threading;
using System.IO;
using UnityEngine;

namespace UnityNativeTool.Internal
{
    [DisableMocking]
    static class LowLevelPluginManager
    {
        private static bool _triedLoadingStubPlugin = false;
        private static IntPtr _unityInterfacePtr = IntPtr.Zero;

        public static void OnDllLoaded(NativeDll dll)
        {
            if (_triedLoadingStubPlugin && _unityInterfacePtr == IntPtr.Zero)
               return;

            var unityPluginLoadFunc = new NativeFunction("UnityPluginLoad", dll) {
                delegateType = typeof(UnityPluginLoadDel)
            };

            DllManipulator.LoadTargetFunction(unityPluginLoadFunc, true);
            if (unityPluginLoadFunc.@delegate == null)
                return;

            if (!_triedLoadingStubPlugin)
            {
                try
                {
                    _unityInterfacePtr = GetUnityInterfacesPtr();
                    if (_unityInterfacePtr == IntPtr.Zero)
                        throw new Exception($"{nameof(GetUnityInterfacesPtr)} returned null");
                }
                catch (DllNotFoundException)
                {
                    Debug.LogWarning("StubLluiPlugin not found. UnityPluginLoad and UnityPluginUnload callbacks won't fire. If need them, please read the README at the github's repo, otherwise you may just comment out this warning.");
                }
                finally
                {
                    _triedLoadingStubPlugin = true;
                }
            }

            if (_unityInterfacePtr != IntPtr.Zero)
                ((UnityPluginLoadDel)unityPluginLoadFunc.@delegate)(_unityInterfacePtr);
        }

        public static void OnBeforeDllUnload(NativeDll dll)
        {
            if (_unityInterfacePtr == IntPtr.Zero)
                return;

            var unityPluginUnloadFunc = new NativeFunction("UnityPluginUnload", dll) {
                delegateType = typeof(UnityPluginUnloadDel)
            };

            DllManipulator.LoadTargetFunction(unityPluginUnloadFunc, true);
            if (unityPluginUnloadFunc.@delegate != null)
                ((UnityPluginUnloadDel)unityPluginUnloadFunc.@delegate)();
        }
        
        public static void ResetStubPlugin()
        {
            _triedLoadingStubPlugin = false;
            _unityInterfacePtr = IntPtr.Zero;
        }

        delegate void UnityPluginLoadDel(IntPtr unityInterfaces);
        delegate void UnityPluginUnloadDel();
        
        [DllImport("StubLluiPlugin")]
        private static extern IntPtr GetUnityInterfacesPtr();
    }
}