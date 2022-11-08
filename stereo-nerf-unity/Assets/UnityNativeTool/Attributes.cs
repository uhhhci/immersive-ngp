using System;

namespace UnityNativeTool
{
    /// <summary>
    /// Member native functions in types with this attributes will be mocked. This attribute is redundant if "Mock all native functions" option is true.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class MockNativeDeclarationsAttribute : Attribute
    {

    }

    /// <summary>
    /// Native functions with this attribute will be mocked. This attribute is redundant if "Mock all native functions" option is true.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class MockNativeDeclarationAttribute : Attribute
    {

    }

    /// <summary>
    /// Applied to native function, prevents it from being mocked.
    /// Applied to class, prevents all member native functions from being mocked.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class DisableMockingAttribute : Attribute
    {

    }

    /// <summary>
    /// Such a method must be static and have one of the following signatures:
    /// <code>
    /// public static void Func()
    /// public static void Func(NativeDll dll)
    /// public static void Func(NativeDll dll, int mainThreadId)
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class TriggerAttribute : Attribute
    {
        /// <summary>
        /// Should the method always be executed on the main thread, to allow use of the Unity API.
        /// Note: this means the method is not immediately executed but put in a queue, if it is not triggered from the main thread.
        /// </summary>
        public bool UseMainThreadQueue = false;
    }
    
    /// <summary>
    /// Methods with this attribute are called directly after a native DLL has been loaded. Native functions can be used within such a method.
    /// This is called after <c>UnityPluginLoad</c>.<br/>
    /// Such method must be <see langword="static"/> and either have no parameters or one parameter of type <see cref="NativeDll"/>
    /// which indicates the state of the dll being loaded. Please treat this parameter as readonly.<br/>
    /// Preloaded: only called once all native methods have been loaded. 
    /// <br/><inheritdoc cref="TriggerAttribute"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class NativeDllLoadedTriggerAttribute : TriggerAttribute
    {

    }

    /// <summary>
    /// Methods with this attribute are called directly before a native DLL is going to be unloaded.
    /// Such method must be <see langword="static"/> and either have no parameters or one parameter of type <see cref="NativeDll"/>
    /// which indicates the state of the dll being unloaded. Please treat this parameter as readonly.
    /// <br/><inheritdoc cref="TriggerAttribute"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class NativeDllBeforeUnloadTriggerAttribute : TriggerAttribute
    {

    }

    /// <summary>
    /// Methods with this attribute are called directly after a native DLL has been unloaded.
    /// Such method must be <see langword="static"/> and either have no parameters or one parameter of type <see cref="NativeDll"/>
    /// which indicates the state of the dll being unloaded. Please treat this parameter as readonly.
    /// <br/><inheritdoc cref="TriggerAttribute"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class NativeDllAfterUnloadTriggerAttribute : TriggerAttribute
    {

    }
}
