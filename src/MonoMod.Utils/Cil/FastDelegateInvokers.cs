using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;
using ReflMethodAttributes = System.Reflection.MethodAttributes;
using ReflTypeAttributes = System.Reflection.TypeAttributes;
using System.Collections.Generic;

namespace MonoMod.Cil
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class GetFastDelegateInvokersArrayAttribute : Attribute
    {
        public int MaxParams { get; }
        public GetFastDelegateInvokersArrayAttribute(int maxParams)
            => MaxParams = maxParams;
    }

    public static partial class FastDelegateInvokers
    {
        private static readonly (MethodInfo, Type, Type)[] invokers = GetInvokers();

        private const int MaxFastInvokerParams = 16;
        private static readonly ConditionalWeakTable<MethodSignature, Tuple<Type, Type>> delegateCache = new();

        private static int unique;
        public static (Type Orig, Type Hook) GetDelegateType(MethodSignature info)
        {
            var sig = info;
            if (sig.ParameterCount > 0)
            {
                if (sig.Parameters.Skip(1).Any(x => x.IsByRefLike() || x.IsByRef)
                    || sig.Parameters.First().IsByRefLike()
                    || sig.ParameterCount > MaxFastInvokerParams
                    || sig.ReturnType.IsByRefLike() || sig.ReturnType.IsByRef)
                {
                    var t = delegateCache.GetValue(sig, k =>
                    {
#if NETCOREAPP1_0_OR_GREATER || NET5_0_OR_GREATER
                        //before we beat alc
                        var asm = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(new AssemblyName($"MonoModModuleFor{Interlocked.Increment(ref unique)}"), System.Reflection.Emit.AssemblyBuilderAccess.RunAndCollect);
                        var mod = asm.DefineDynamicModule("MainModule");
                        static System.Reflection.Emit.MethodBuilder MakeDelegate(System.Reflection.Emit.TypeBuilder type, Type ret, Type[] param)
                        {

                            type.SetParent(typeof(MulticastDelegate));
                            var ctor = type.DefineMethod(".ctor", ReflMethodAttributes.Public | ReflMethodAttributes.SpecialName | ReflMethodAttributes.RTSpecialName, typeof(void), [typeof(object), typeof(IntPtr)]);

                            ctor.SetImplementationFlags(System.Reflection.MethodImplAttributes.Runtime);

                            var inv = type.DefineMethod("Invoke", ReflMethodAttributes.Public | ReflMethodAttributes.Virtual | ReflMethodAttributes.HideBySig | ReflMethodAttributes.NewSlot, ret, param);

                            inv.SetImplementationFlags(System.Reflection.MethodImplAttributes.Runtime);
                            return inv;
                        }
                        var orig = mod.DefineType("Orig", ReflTypeAttributes.Public | ReflTypeAttributes.Sealed | ReflTypeAttributes.Class);
                        var oi = MakeDelegate(orig, k.ReturnType, k.Parameters.ToArray());
                        var Orig = orig.CreateType()!;

                        static IEnumerable<T> PrePend<T>(IEnumerable<T> self, T add)
                        {
                            yield return add;
                            foreach (var i in self)
                            {
                                yield return i;
                            }
                        }
                        var hook = mod.DefineType("Hook", ReflTypeAttributes.Public | ReflTypeAttributes.Sealed | ReflTypeAttributes.Class);
                        var hi = MakeDelegate(hook, k.ReturnType, PrePend(k.Parameters, Orig).ToArray());

                        var Hook = hook.CreateType()!;
                        return new(Orig, Hook);
#else
                        static TypeDefinition CreateDelegate(ModuleDefinition import, MethodDefinition invoke, bool nested)
                        {
                            TypeDefinition dele = new("", "", TypeAttributes.Sealed | TypeAttributes.Class,
                            import.ImportReference(typeof(MulticastDelegate)));

                            var ctor = new MethodDefinition(
                            ".ctor",
                            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                            import.TypeSystem.Void);
                            ctor.IsRuntime = true;
                            ctor.Parameters.Add(new ParameterDefinition(import.TypeSystem.Object));
                            ctor.Parameters.Add(new ParameterDefinition(import.TypeSystem.IntPtr));
                            dele.Methods.Add(ctor);

                            invoke.Name = "Invoke";
                            invoke.Attributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot;
                            dele.Methods.Add(invoke);

                            invoke.IsRuntime = true;
                            if (nested)
                            {
                                dele.IsNestedAssembly = true;
                            }

                            foreach (var p in invoke.Parameters)
                            {
                                p.HasDefault = false;
                            }

                            return dele;
                        }

                        var mod = ModuleDefinition.CreateModule($"MonoModModuleFor{Interlocked.Increment(ref unique)}", new ModuleParameters());
                        var t = new MethodDefinition("", MethodAttributes.Public, mod.ImportReference(k.ReturnType));
                        t.Parameters.AddRange(k.Parameters.Select(x => new ParameterDefinition(mod.ImportReference(x))));
                        var u = t.Clone();

                        var or = CreateDelegate(mod, t, false);
                        or.Name = "Orig";
                        u.Parameters.Insert(0, new(or));
                        var wi = CreateDelegate(mod, u, false);
                        wi.Name = "Hook";

                        mod.Types.Add(or);
                        mod.Types.Add(wi);

                        MemoryStream ms = new();
                        mod.Write(ms);
                        ms.Seek(0, SeekOrigin.Begin);

                        var asm = Assembly.Load(ms.ToArray());
                        return new(asm.GetType("Orig")!, asm.GetType("Hook")!);
#endif
                    });
                    return (t.Item1, t.Item2);
                }
                else
                {
                    var index = 0;
                    index |= sig.ReturnType != typeof(void) ? 0b01 : 0b00;
                    index |= sig.Parameters.First().IsByRef ? 0b10 : 0b00;
                    index |= (sig.ParameterCount - 1) << 2;
                    var (_, o, w) = invokers[index];
                    var param = sig.Parameters.Select(x => x.IsByRef ? x.GetElementType()! : x).ToList();
                    if (sig.ReturnType != typeof(void))
                    {
                        param.Insert(0, sig.ReturnType);
                    }
                    var p = param.ToArray();
                    return (o.MakeGenericType(p), w.MakeGenericType(p));
                }
            }
            else
            {
                if (sig.ReturnType != typeof(void))
                {
                    var orig = typeof(Func<>).MakeGenericType(sig.ReturnType);
                    return (orig, typeof(Func<,>).MakeGenericType(orig, sig.ReturnType));
                }
                else
                {
                    return (typeof(Action), typeof(Action<Action>));
                }
            }
        }

        [GetFastDelegateInvokersArray(MaxFastInvokerParams)]
        private static partial (MethodInfo, Type, Type)[] GetInvokers();

        private static (MethodInfo Invoker, Type Delegate)? TryGetInvokerForSig(MethodSignature sig)
        {
            // if the signature doesn't take any arguments, we don't need an invoker in the first place
            if (sig.ParameterCount == 0)
                return null;

            // if the signature takes more parameters than our max, we don't have a pregenerated invoker for it
            if (sig.ParameterCount > MaxFastInvokerParams)
                return null;

            // we want to construct an index to look up an invoker
            // this index is structured as follows (low to high bits)
            //     xyzzzzz...
            // x: has non-void return
            // y: first param is byref
            // z: number of parameters AFTER the first

            Helpers.DAssert(sig.FirstParameter is not null);

            // make sure that the return type is not byref or byreflike
            if (sig.ReturnType.IsByRef || sig.ReturnType.IsByRefLike())
                return null;
            // make sure that the first parameter is not byreflike
            if (sig.FirstParameter.IsByRefLike())
                return null;
            // make sure that the other parameters are not byref or byreflike
            if (sig.Parameters.Skip(1).Any(t => t.IsByRef || t.IsByRefLike()))
                return null;

            var index = 0;
            index |= sig.ReturnType != typeof(void) ? 0b01 : 0b00;
            index |= sig.FirstParameter.IsByRef ? 0b10 : 0b00;
            index |= (sig.ParameterCount - 1) << 2;

            var (invoker, del, _) = invokers[index];

            var typeParams = new Type[sig.ParameterCount + (index & 1)];
            // first param is always return type, if present
            var i = 0;
            if ((index & 1) != 0)
                typeParams[i++] = sig.ReturnType;
            foreach (var p in sig.Parameters)
            {
                var t = p;
                if (t.IsByRef)
                    t = t.GetElementType()!;
                typeParams[i++] = t;
            }
            Helpers.Assert(i == typeParams.Length);

            return (invoker.MakeGenericMethod(typeParams), del.MakeGenericType(typeParams));
        }

        private static readonly ConditionalWeakTable<Type, Tuple<MethodInfo?, Type>> invokerCache = new();
        public static (MethodInfo Invoker, Type Delegate)? GetDelegateInvoker(Type delegateType)
        {
            Helpers.ThrowIfArgumentNull(delegateType);
            if (!typeof(Delegate).IsAssignableFrom(delegateType))
                throw new ArgumentException("Argument not a delegate type", nameof(delegateType));

            var tuple = invokerCache.GetValue(delegateType, static delegateType =>
            {

                var delInvoke = delegateType.GetMethod("Invoke")!;
                var sig = MethodSignature.ForMethod(delInvoke, ignoreThis: true);

                if (sig.ParameterCount == 0)
                {
                    return new(null, delegateType);
                }

                var builtinInvoker = TryGetInvokerForSig(sig);
                if (builtinInvoker is { } p)
                {
                    return new(p.Invoker, p.Delegate);
                }

                var argTypes = new Type[sig.ParameterCount + 1];
                var i = 0;
                foreach (var param in sig.Parameters)
                {
                    argTypes[i++] = param;
                }
                argTypes[sig.ParameterCount] = delegateType;

                using (var dmdInvoke = new DynamicMethodDefinition(
                    $"MMIL:Invoke<{delInvoke.DeclaringType?.FullName}>",
                    delInvoke.ReturnType, argTypes
                ))
                {
                    var il = dmdInvoke.GetILProcessor();

                    // Load the delegate reference first.
                    il.Emit(OpCodes.Ldarg, sig.ParameterCount);

                    // Load the rest of the args
                    for (i = 0; i < sig.ParameterCount; i++)
                        il.Emit(OpCodes.Ldarg, i);

                    // Invoke the delegate and return its result.
                    il.Emit(OpCodes.Callvirt, delInvoke);
                    il.Emit(OpCodes.Ret);

                    var invoker = dmdInvoke.Generate();
                    return new(invoker, delegateType);
                }
            });

            if (tuple.Item1 is null)
                return null;
            return (tuple.Item1, tuple.Item2);
        }
    }
}
