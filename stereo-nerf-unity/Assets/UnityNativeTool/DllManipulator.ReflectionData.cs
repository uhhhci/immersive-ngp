using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace UnityNativeTool.Internal
{
    public partial class DllManipulator
    {
        private static readonly Type[] DELEGATE_CTOR_PARAMETERS = { typeof(object), typeof(IntPtr) };
        private static readonly Type[] MARSHAL_AS_ATTRIBUTE_CTOR_PARAMETERS = { typeof(UnmanagedType) };


        private static readonly Lazy<FieldInfo> Field_MockedNativeFunctions = new Lazy<FieldInfo>(
            () => typeof(DllManipulator).GetField(nameof(_mockedNativeFunctions), BindingFlags.NonPublic | BindingFlags.Static));

        private static readonly Lazy<FieldInfo> Field_NativeFunctionDelegate = new Lazy<FieldInfo>(
            () => typeof(NativeFunction).GetField(nameof(NativeFunction.@delegate), BindingFlags.Public | BindingFlags.Instance));

        private static readonly Lazy<MethodInfo> Method_LoadTargetFunction = new Lazy<MethodInfo>(
            () => typeof(DllManipulator).GetMethod(nameof(LoadTargetFunction), BindingFlags.NonPublic | BindingFlags.Static));

        private static readonly Lazy<FieldInfo> Field_NativeFunctionLoadLock = new Lazy<FieldInfo>(
            () => typeof(DllManipulator).GetField(nameof(_nativeFunctionLoadLock), BindingFlags.NonPublic | BindingFlags.Static));

        private static readonly Lazy<MethodInfo> Method_WriteNativeCrashLog = new Lazy<MethodInfo>(
            () => typeof(DllManipulator).GetMethod(nameof(WriteNativeCrashLog), BindingFlags.NonPublic | BindingFlags.Static));


        private static readonly Lazy<MethodInfo> Method_List_NativeFunction_get_Item = new Lazy<MethodInfo>(
            () => typeof(List<NativeFunction>).GetMethod("get_Item", BindingFlags.Public | BindingFlags.Instance));

        /// <summary>
        /// <see cref="ReaderWriterLockSlim.EnterReadLock()"/>
        /// </summary>
        private static readonly Lazy<MethodInfo> Method_Rwls_EnterReadLock = new Lazy<MethodInfo>(
            () => typeof(ReaderWriterLockSlim).GetMethod(nameof(ReaderWriterLockSlim.EnterReadLock), BindingFlags.Public | BindingFlags.Instance));

        /// <summary>
        /// <see cref="ReaderWriterLockSlim.ExitReadLock()"/>
        /// </summary>
        private static readonly Lazy<MethodInfo> Method_Rwls_ExitReadLock = new Lazy<MethodInfo>(
            () => typeof(ReaderWriterLockSlim).GetMethod(nameof(ReaderWriterLockSlim.ExitReadLock), BindingFlags.Public | BindingFlags.Instance));

        /// <summary>
        /// <see cref=UnmanagedFunctionPointerAttribute"/>
        /// </summary>
        private static readonly Lazy<ConstructorInfo> Ctor_Ufp = new Lazy<ConstructorInfo>(
            () => typeof(UnmanagedFunctionPointerAttribute).GetConstructor(new[] { typeof(CallingConvention) } ));

        /// <summary>
        /// <see cref=UnmanagedFunctionPointerAttribute.BestFitMapping"/>
        /// </summary>
        private static readonly Lazy<FieldInfo> Field_Ufpa_BestFitMapping = new Lazy<FieldInfo>(
           () => typeof(UnmanagedFunctionPointerAttribute).GetField(nameof(UnmanagedFunctionPointerAttribute.BestFitMapping), BindingFlags.Public | BindingFlags.Instance));

        /// <summary>
        /// <see cref=UnmanagedFunctionPointerAttribute.CharSet"/>
        /// </summary>
        private static readonly Lazy<FieldInfo> Field_Ufpa_CharSet = new Lazy<FieldInfo>(
           () => typeof(UnmanagedFunctionPointerAttribute).GetField(nameof(UnmanagedFunctionPointerAttribute.CharSet), BindingFlags.Public | BindingFlags.Instance));

        /// <summary>
        /// <see cref=UnmanagedFunctionPointerAttribute.SetLastError"/>
        /// </summary>
        private static readonly Lazy<FieldInfo> Field_Ufpa_SetLastError = new Lazy<FieldInfo>(
           () => typeof(UnmanagedFunctionPointerAttribute).GetField(nameof(UnmanagedFunctionPointerAttribute.SetLastError), BindingFlags.Public | BindingFlags.Instance));

        /// <summary>
        /// <see cref=UnmanagedFunctionPointerAttribute.ThrowOnUnmappableChar"/>
        /// </summary>
        private static readonly Lazy<FieldInfo> Field_Ufpa_ThrowOnUnmappableChar = new Lazy<FieldInfo>(
           () => typeof(UnmanagedFunctionPointerAttribute).GetField(nameof(UnmanagedFunctionPointerAttribute.ThrowOnUnmappableChar), BindingFlags.Public | BindingFlags.Instance));

        /// <summary>
        /// <see cref=MarshalAsAttribute"/>
        /// </summary>
        private static readonly Lazy<ConstructorInfo> Ctor_MarshalAsAttribute = new Lazy<ConstructorInfo>(
            () => typeof(MarshalAsAttribute).GetConstructor(MARSHAL_AS_ATTRIBUTE_CTOR_PARAMETERS));

        #region Mono specific

        /// <summary>
        /// DynamicMethod.CreateDynMethod()
        /// </summary>
        private static readonly Lazy<MethodInfo> Method_DynamicMethod_CreateDynMethod = new Lazy<MethodInfo>(
            () => typeof(DynamicMethod).GetMethod("CreateDynMethod", BindingFlags.NonPublic | BindingFlags.Instance));

        #endregion
    }
}
