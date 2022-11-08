//
// Following file is essentially copy of MethodInvoker.cs file from https://github.com/pardeike/Harmony
//

using System;
using System.Reflection;
using System.Reflection.Emit;

namespace UnityNativeTool.Internal
{
    // Based on https://www.codeproject.com/Articles/14593/A-General-Fast-Method-Invoker

    /// <summary>A delegate to invoke a method</summary>
    /// <param name="target">The instance</param>
    /// <param name="parameters">The method parameters</param>
    /// <returns>The method result</returns>
    ///
    public delegate object FastInvokeHandler(object target, object[] parameters);

    /// <summary>A helper class to invoke method with delegates</summary>
    public class MethodInvoker
    {
        /// <summary>Creates a fast invocation handler from a method and a module</summary>
        /// <param name="methodInfo">The method to invoke</param>
        /// <param name="module">The module context</param>
        /// <returns>The fast invocation handler</returns>
        ///
        public static FastInvokeHandler GetHandler(DynamicMethod methodInfo, Module module)
        {
            return Handler(methodInfo, module);
        }

        /// <summary>Creates a fast invocation handler from a method and a module</summary>
        /// <param name="methodInfo">The method to invoke</param>
        /// <returns>The fast invocation handler</returns>
        ///
        public static FastInvokeHandler GetHandler(MethodInfo methodInfo)
        {
            return Handler(methodInfo, methodInfo.DeclaringType.Module);
        }

        static FastInvokeHandler Handler(MethodInfo methodInfo, Module module, bool directBoxValueAccess = false)
        {
            var dynamicMethod = new DynamicMethod("FastInvoke_" + methodInfo.Name + "_" + (directBoxValueAccess ? "direct" : "indirect"), typeof(object), new Type[] { typeof(object), typeof(object[]) }, module, true);
            var il = dynamicMethod.GetILGenerator();

            if (!methodInfo.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                EmitUnboxIfNeeded(il, methodInfo.DeclaringType);
            }

            var generateLocalBoxValuePtr = true;
            var ps = methodInfo.GetParameters();
            for (var i = 0; i < ps.Length; i++)
            {
                var argType = ps[i].ParameterType;
                var argIsByRef = argType.IsByRef;
                if (argIsByRef)
                    argType = argType.GetElementType();
                var argIsValueType = argType.IsValueType;

                if (argIsByRef && argIsValueType && !directBoxValueAccess)
                {
                    // used later when storing back the reference to the new box in the array.
                    il.Emit(OpCodes.Ldarg_1);
                    il.EmitFastI4Load(i);
                }

                il.Emit(OpCodes.Ldarg_1);
                il.EmitFastI4Load(i);

                if (argIsByRef && !argIsValueType)
                {
                    il.Emit(OpCodes.Ldelema, typeof(object));
                }
                else
                {
                    il.Emit(OpCodes.Ldelem_Ref);
                    if (argIsValueType)
                    {
                        if (!argIsByRef || !directBoxValueAccess)
                        {
                            // if !directBoxValueAccess, create a new box if required
                            il.Emit(OpCodes.Unbox_Any, argType);
                            if (argIsByRef)
                            {
                                // box back
                                il.Emit(OpCodes.Box, argType);

                                // store new box value address to local 0
                                il.Emit(OpCodes.Dup);
                                il.Emit(OpCodes.Unbox, argType);
                                if (generateLocalBoxValuePtr)
                                {
                                    generateLocalBoxValuePtr = false;
                                    // Yes, you're seeing this right - a local of type void* to store the box value address!
                                    il.DeclareLocal(typeof(void*), true);
                                }
                                il.Emit(OpCodes.Stloc_0);

                                // arr and index set up already
                                il.Emit(OpCodes.Stelem_Ref);

                                // load address back to stack
                                il.Emit(OpCodes.Ldloc_0);
                            }
                        }
                        else
                        {
                            // if directBoxValueAccess, emit unbox (get value address)
                            il.Emit(OpCodes.Unbox, argType);
                        }
                    }
                }
            }

#pragma warning disable XS0001
            if (methodInfo.IsStatic)
                il.EmitCall(OpCodes.Call, methodInfo, null);
            else
                il.EmitCall(OpCodes.Callvirt, methodInfo, null);
#pragma warning restore XS0001

            if (methodInfo.ReturnType == typeof(void))
                il.Emit(OpCodes.Ldnull);
            else
                EmitBoxIfNeeded(il, methodInfo.ReturnType);

            il.Emit(OpCodes.Ret);

            var invoder = (FastInvokeHandler)dynamicMethod.CreateDelegate(typeof(FastInvokeHandler));
            return invoder;
        }

        static void EmitCastToReference(ILGenerator il, Type type)
        {
            if (type.IsValueType)
                il.Emit(OpCodes.Unbox_Any, type);
            else
                il.Emit(OpCodes.Castclass, type);
        }

        static void EmitUnboxIfNeeded(ILGenerator il, Type type)
        {
            if (type.IsValueType)
                il.Emit(OpCodes.Unbox_Any, type);
        }

        static void EmitBoxIfNeeded(ILGenerator il, Type type)
        {
            if (type.IsValueType)
                il.Emit(OpCodes.Box, type);
        }
    }
}