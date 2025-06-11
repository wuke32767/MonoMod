using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Core;
using MonoMod.Core.Platforms;
using MonoMod.Logs;
using MonoMod.Utils;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using _Conv = MonoMod.RuntimeDetour.FunctionPointerConvertor<System.Delegate, System.Delegate, object>;

namespace MonoMod.RuntimeDetour
{
    sealed class UnwrappedDelegate
    {
        public nint MethodPointer;
        // only used for Intermediate<>.
        // not designed for struct.
        public object? Target;
    }
    internal sealed class FunctionPointerConvertor<TTarget, TIn, TSlot>
        where TTarget : Delegate where TIn : Delegate
    {
        public TTarget? LastDelegate;
        public TIn? LastInDelegate;
        public MethodBase? Invoker;
        public TSlot? Slot;

        public TTarget ProcessDelegate(TIn src)
        {
            if (LastInDelegate == src)
            {
                return LastDelegate!;
            }
            LastInDelegate = src;
            var uw = new UnwrappedDelegate()
            {
                MethodPointer = PlatformTriple.Current.Runtime.GetMethodEntryPoint(src.Method),
                Target = src.Target,
            };
            return LastDelegate = Invoker!.CreateDelegate<TTarget>(uw);
        }
    }
    internal sealed class CompatibleConverter<TTarget, TIn, TSlot>
        where TTarget : Delegate where TIn : Delegate
    {
        public TTarget? LastDelegate;
        public TIn? LastInDelegate;
        public TSlot? Slot;
        //public static ConditionalWeakTable<MethodInfo, MethodInfo>? CastCache;
        public TTarget CastDelegate(TIn src)
        {
            try
            {
                return LastDelegate = src.CastDelegate<TTarget>();
            }
            catch
            {
                // TODO: can this really happen?
                throw;
                MMDbgLog.Warning($"Finally, it triggered.\nFailed when casting delegate. Falling back to Unsafe.As.\n{src.Method}");
                return LastDelegate = Unsafe.As<TTarget>(src);
            }

        }
        public TTarget ProcessDelegate(TIn src)
        {
            if (LastInDelegate == src)
            {
                return LastDelegate!;
            }
            LastInDelegate = src;
            return CastDelegate(src);
        }
    }

    /// <summary>
    /// A single method hook from a source to a target, optionally allowing the target to call the original method.
    /// </summary>
    /// <remarks>
    /// <see cref="Hook"/>s, like other kinds of detours, are automatically undone when the garbage collector collects the object,
    /// or the object is disposed. Use <see cref="DetourInfo"/> to get an object which represents the hook without
    /// extending its lifetime.
    /// </remarks>
    public sealed class Hook : IDetour, IDisposable
    {

        private const bool ApplyByDefault = true;

        // Note: We don't provide all variants with IDetourFactory because providing IDetourFactory is expected to be fairly rare
        #region Constructor overloads
        #region No targetObj
        /// <summary>
        /// Constructs a <see cref="Hook"/> using the methods specified by expression trees. Each expression tree must consist only of
        /// a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        public Hook(Expression<Action> source, Expression<Action> target)
            : this(Helpers.ThrowIfNull(source).Body, Helpers.ThrowIfNull(target).Body) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> using the methods specified by expression trees. Each expression tree must consist only of
        /// a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        public Hook(Expression source, Expression target)
            : this(((MethodCallExpression)Helpers.ThrowIfNull(source)).Method,
                  ((MethodCallExpression)Helpers.ThrowIfNull(target)).Method)
        { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring <paramref name="source"/> to <paramref name="target"/>.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        public Hook(MethodBase source, MethodInfo target)
            : this(source, target, DetourContext.GetDefaultConfig()) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> using the methods specified by expression trees. Each expression tree must consist only of
        /// a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(Expression<Action> source, Expression<Action> target, bool applyByDefault)
            : this(Helpers.ThrowIfNull(source).Body, Helpers.ThrowIfNull(target).Body, applyByDefault) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> using the methods specified by expression trees. Each expression tree must consist only of
        /// a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(Expression source, Expression target, bool applyByDefault)
            : this(((MethodCallExpression)Helpers.ThrowIfNull(source)).Method,
                  ((MethodCallExpression)Helpers.ThrowIfNull(target)).Method, applyByDefault)
        { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring <paramref name="source"/> to <paramref name="target"/>.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(MethodBase source, MethodInfo target, bool applyByDefault)
            : this(source, target, DetourContext.GetDefaultConfig(), applyByDefault) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> using the provided <see cref="DetourConfig"/> and methods specified by expression trees. Each expression tree must consist only of
        /// a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        public Hook(Expression<Action> source, Expression<Action> target, DetourConfig? config)
            : this(Helpers.ThrowIfNull(source).Body, Helpers.ThrowIfNull(target).Body, config) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> using the provided <see cref="DetourConfig"/> and methods specified by expression trees. Each expression tree must consist only of
        /// a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        public Hook(Expression source, Expression target, DetourConfig? config)
            : this(((MethodCallExpression)Helpers.ThrowIfNull(source)).Method,
                  ((MethodCallExpression)Helpers.ThrowIfNull(target)).Method, config)
        { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring <paramref name="source"/> to <paramref name="target"/>, using the provided <see cref="DetourConfig"/>.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        public Hook(MethodBase source, MethodInfo target, DetourConfig? config)
            : this(source, target, config, ApplyByDefault) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> using the provided <see cref="DetourConfig"/> and methods specified by expression trees. Each expression tree must consist only of
        /// a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(Expression<Action> source, Expression<Action> target, DetourConfig? config, bool applyByDefault)
            : this(Helpers.ThrowIfNull(source).Body, Helpers.ThrowIfNull(target).Body, config, applyByDefault) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> using the provided <see cref="DetourConfig"/> and methods specified by expression trees. Each expression tree must consist only of
        /// a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(Expression source, Expression target, DetourConfig? config, bool applyByDefault)
            : this(((MethodCallExpression)Helpers.ThrowIfNull(source)).Method,
                  ((MethodCallExpression)Helpers.ThrowIfNull(target)).Method, config, applyByDefault)
        { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring <paramref name="source"/> to <paramref name="target"/>, using the provided <see cref="DetourConfig"/>.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(MethodBase source, MethodInfo target, DetourConfig? config, bool applyByDefault)
            : this(source, target, DetourContext.GetDefaultFactory(), config, applyByDefault) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring <paramref name="source"/> to <paramref name="target"/>, using the provided <see cref="DetourConfig"/>
        /// and <see cref="IDetourFactory"/>.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="factory">The <see cref="IDetourFactory"/> to use when manipulating this <see cref="Hook"/>.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(MethodBase source, MethodInfo target, IDetourFactory factory, DetourConfig? config, bool applyByDefault)
            : this(source, target, null, factory, config, applyByDefault) { }
        #endregion
        #region With targetObj
        /// <summary>
        /// Constructs a <see cref="Hook"/> using the methods specified by expression trees, and specified target object. Each expression tree must consist only of
        /// a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="targetObj">The <see langword="this"/> object to call the target method on.</param>
        public Hook(Expression<Action> source, Expression<Action> target, object? targetObj)
            : this(Helpers.ThrowIfNull(source).Body, Helpers.ThrowIfNull(target).Body, targetObj) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> using the methods specified by expression trees, and specified target object. Each expression tree must consist only of
        /// a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="targetObj">The <see langword="this"/> object to call the target method on.</param>
        public Hook(Expression source, Expression target, object? targetObj)
            : this(((MethodCallExpression)Helpers.ThrowIfNull(source)).Method,
                  ((MethodCallExpression)Helpers.ThrowIfNull(target)).Method, targetObj)
        { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring <paramref name="source"/> to <paramref name="target"/>, calling <paramref name="target"/> on <paramref name="targetObj"/>.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="targetObj">The <see langword="this"/> object to call the target method on.</param>
        public Hook(MethodBase source, MethodInfo target, object? targetObj)
            : this(source, target, targetObj, DetourContext.GetDefaultConfig()) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> using the methods specified by expression trees, and specified target object. Each expression tree must consist only of
        /// a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="targetObj">The <see langword="this"/> object to call the target method on.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(Expression<Action> source, Expression<Action> target, object? targetObj, bool applyByDefault)
            : this(Helpers.ThrowIfNull(source).Body, Helpers.ThrowIfNull(target).Body, targetObj, applyByDefault) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> using the methods specified by expression trees, and specified target object. Each expression tree must consist only of
        /// a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="targetObj">The <see langword="this"/> object to call the target method on.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(Expression source, Expression target, object? targetObj, bool applyByDefault)
            : this(((MethodCallExpression)Helpers.ThrowIfNull(source)).Method,
                  ((MethodCallExpression)Helpers.ThrowIfNull(target)).Method, targetObj, applyByDefault)
        { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring <paramref name="source"/> to <paramref name="target"/>, calling <paramref name="target"/> on <paramref name="targetObj"/>.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="targetObj">The <see langword="this"/> object to call the target method on.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(MethodBase source, MethodInfo target, object? targetObj, bool applyByDefault)
            : this(source, target, targetObj, DetourContext.GetDefaultConfig(), applyByDefault) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> using the provided <see cref="DetourConfig"/>, methods specified by expression trees, and specified target object.
        /// Each expression tree must consist only of a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="targetObj">The <see langword="this"/> object to call the target method on.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        public Hook(Expression<Action> source, Expression<Action> target, object? targetObj, DetourConfig? config)
            : this(Helpers.ThrowIfNull(source).Body, Helpers.ThrowIfNull(target).Body, targetObj, config) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> using the provided <see cref="DetourConfig"/>, methods specified by expression trees, and specified target object.
        /// Each expression tree must consist only of a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="targetObj">The <see langword="this"/> object to call the target method on.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        public Hook(Expression source, Expression target, object? targetObj, DetourConfig? config)
            : this(((MethodCallExpression)Helpers.ThrowIfNull(source)).Method,
                  ((MethodCallExpression)Helpers.ThrowIfNull(target)).Method, targetObj, config)
        { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring <paramref name="source"/> to <paramref name="target"/>, calling <paramref name="target"/> on <paramref name="targetObj"/>,
        /// using the provided <see cref="DetourConfig"/>..
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="targetObj">The <see langword="this"/> object to call the target method on.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        public Hook(MethodBase source, MethodInfo target, object? targetObj, DetourConfig? config)
            : this(source, target, targetObj, config, ApplyByDefault) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> using the provided <see cref="DetourConfig"/>, methods specified by expression trees, and specified target object.
        /// Each expression tree must consist only of a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="targetObj">The <see langword="this"/> object to call the target method on.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(Expression<Action> source, Expression<Action> target, object? targetObj, DetourConfig? config, bool applyByDefault)
            : this(Helpers.ThrowIfNull(source).Body, Helpers.ThrowIfNull(target).Body, targetObj, config, applyByDefault) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> using the provided <see cref="DetourConfig"/>, methods specified by expression trees, and specified target object.
        /// Each expression tree must consist only of a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="targetObj">The <see langword="this"/> object to call the target method on.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(Expression source, Expression target, object? targetObj, DetourConfig? config, bool applyByDefault)
            : this(((MethodCallExpression)Helpers.ThrowIfNull(source)).Method,
                  ((MethodCallExpression)Helpers.ThrowIfNull(target)).Method, targetObj, config, applyByDefault)
        { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring <paramref name="source"/> to <paramref name="target"/>, calling <paramref name="target"/> on <paramref name="targetObj"/>,
        /// using the provided <see cref="DetourConfig"/>.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="targetObj">The <see langword="this"/> object to call the target method on.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(MethodBase source, MethodInfo target, object? targetObj, DetourConfig? config, bool applyByDefault)
            : this(source, target, targetObj, DetourContext.GetDefaultFactory(), config, applyByDefault) { }
        #endregion
        #region Delegate target
        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring the method specified by the provided expression tree to the provided delegate.
        /// The expression tree must consist only of a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The method to detour.</param>
        /// <param name="target">The target delegate.</param>
        public Hook(Expression<Action> source, Delegate target)
            : this(Helpers.ThrowIfNull(source).Body, target) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring the method specified by the provided expression tree to the provided delegate.
        /// The expression tree must consist only of a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The method to detour.</param>
        /// <param name="target">The target delegate.</param>
        public Hook(Expression source, Delegate target)
            : this(((MethodCallExpression)Helpers.ThrowIfNull(source)).Method, target) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring the provided method to the provided delegate.
        /// </summary>
        /// <param name="source">The method to detour.</param>
        /// <param name="target">The target delegate.</param>
        public Hook(MethodBase source, Delegate target)
            : this(source, target, DetourContext.GetDefaultConfig()) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring the method specified by the provided expression tree to the provided delegate.
        /// The expression tree must consist only of a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The method to detour.</param>
        /// <param name="target">The target delegate.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(Expression<Action> source, Delegate target, bool applyByDefault)
            : this(Helpers.ThrowIfNull(source).Body, target, applyByDefault) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring the method specified by the provided expression tree to the provided delegate.
        /// The expression tree must consist only of a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The method to detour.</param>
        /// <param name="target">The target delegate.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(Expression source, Delegate target, bool applyByDefault)
            : this(((MethodCallExpression)Helpers.ThrowIfNull(source)).Method, target, applyByDefault) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring the provided method to the provided delegate.
        /// </summary>
        /// <param name="source">The method to detour.</param>
        /// <param name="target">The target delegate.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(MethodBase source, Delegate target, bool applyByDefault)
            : this(source, target, DetourContext.GetDefaultConfig(), applyByDefault) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring the method specified by the provided expression tree to the provided delegate.
        /// The expression tree must consist only of a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The method to detour.</param>
        /// <param name="target">The target delegate.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        public Hook(Expression<Action> source, Delegate target, DetourConfig? config)
            : this(Helpers.ThrowIfNull(source).Body, target, config) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring the method specified by the provided expression tree to the provided delegate.
        /// The expression tree must consist only of a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The method to detour.</param>
        /// <param name="target">The target delegate.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        public Hook(Expression source, Delegate target, DetourConfig? config)
            : this(((MethodCallExpression)Helpers.ThrowIfNull(source)).Method, target, config) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring the provided method to the provided delegate, using the provided <see cref="DetourConfig"/>.
        /// </summary>
        /// <param name="source">The method to detour.</param>
        /// <param name="target">The target delegate.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        public Hook(MethodBase source, Delegate target, DetourConfig? config)
            : this(source, target, config, ApplyByDefault) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring the method specified by the provided expression tree to the provided delegate.
        /// The expression tree must consist only of a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The method to detour.</param>
        /// <param name="target">The target delegate.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(Expression<Action> source, Delegate target, DetourConfig? config, bool applyByDefault)
            : this(Helpers.ThrowIfNull(source).Body, target, config, applyByDefault) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring the method specified by the provided expression tree to the provided delegate.
        /// The expression tree must consist only of a single methodcall, which will be the method used for that parameter.
        /// </summary>
        /// <param name="source">The method to detour.</param>
        /// <param name="target">The target delegate.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(Expression source, Delegate target, DetourConfig? config, bool applyByDefault)
            : this(((MethodCallExpression)Helpers.ThrowIfNull(source)).Method, target, config, applyByDefault) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring the provided method to the provided delegate, using the provided <see cref="DetourConfig"/>.
        /// </summary>
        /// <param name="source">The method to detour.</param>
        /// <param name="target">The target delegate.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(MethodBase source, Delegate target, DetourConfig? config, bool applyByDefault)
            : this(source, target, DetourContext.GetDefaultFactory(), config, applyByDefault) { }

        /// <summary>
        /// Constructs a <see cref="Hook"/> detouring the provided method to the provided delegate, using the provided <see cref="DetourConfig"/>
        /// and <see cref="IDetourFactory"/>.
        /// </summary>
        /// <param name="source">The method to detour.</param>
        /// <param name="target">The target delegate.</param>
        /// <param name="factory">The <see cref="IDetourFactory"/> to use when manipulating this <see cref="Hook"/>.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(MethodBase source, Delegate target, IDetourFactory factory, DetourConfig? config, bool applyByDefault)
            : this(source, GetDelegateHookInfo(Helpers.ThrowIfNull(target), out var targetObj), targetObj, factory, config, applyByDefault) { }
        #endregion
        #endregion

        private static MethodInfo GetDelegateHookInfo(Delegate del, out object? target)
        {
            if (del.GetInvocationList().Length == 1)
            {
                target = del.Target;
                return del.Method;
            }
            else
            {
                target = del;
                return del.GetType().GetMethod("Invoke") ?? throw new InvalidOperationException("Could not get Invoke method of delegate");
            }
        }

        private readonly IDetourFactory factory;
        IDetourFactory IDetourBase.Factory => factory;

        /// <summary>
        /// Gets the <see cref="DetourConfig"/> associated with this <see cref="Hook"/>, if any.
        /// </summary>
        public DetourConfig? Config { get; }

        /// <summary>
        /// Gets the method which is being hooked.
        /// </summary>
        public MethodBase Source { get; }
        /// <summary>
        /// Gets the method which is the target of the hook.
        /// </summary>
        public MethodInfo Target { get; }
        MethodInfo IDetour.PublicTarget => Target;

        private readonly Delegate realTarget;
        MethodInfo IDetour.InvokeTarget => Target;

        Delegate IDetour.InvokDelegate => realTarget;

        LegacyTrampolineData? LegacyTrampoline;
        IDetourTrampoline IDetour.NextTrampoline => LegacyTrampoline!;
        [SuppressMessage("Reliability", "CA2002:Do not lock on objects with weak identity",
            Justification = "This type is never available externally, and will never be locked on externally.")]
        private sealed class LegacyTrampolineData : IDetourTrampoline, IDisposable
        {
            private readonly MethodInfo trampoline;
            public object? slot;
            public Delegate? Target;
            DataScope<DynamicReferenceCell> refs;
            public static FieldInfo getSlot = typeof(LegacyTrampolineData).GetField(nameof(slot))!;
            public static FieldInfo getTarget = typeof(LegacyTrampolineData).GetField(nameof(Target))!;
            public MethodBase TrampolineMethod => trampoline;

            public LegacyTrampolineData(MethodSignature sig, Type invoke)
            {
                var dmd = sig.CreateDmd($"LegacyTrampoline<{sig}>");
                var il = dmd.GetILProcessor();
                refs = il.EmitNewReference(this, out _);
                il.Emit(OpCodes.Ldfld, getTarget);
                foreach (var i in dmd.Definition.Parameters)
                {
                    il.Emit(OpCodes.Ldarg, i);
                }
                il.Emit(OpCodes.Call, invoke.GetMethod("Invoke")!);
                il.Emit(OpCodes.Ret);
                trampoline = dmd.Generate();
            }

            public void Dispose()
            {
                lock (this)
                {
                    refs.Dispose();
                }
            }

            public void StealTrampolineOwnership()
            {
            }

            public void ReturnTrampolineOwnership()
            {
            }

        }
        private sealed class TrampolineData : IDetourTrampoline, IDisposable
        {

            private readonly MethodInfo trampoline;
            private bool alive, hasOwnership;

            public MethodBase TrampolineMethod => trampoline;

            public TrampolineData(MethodSignature sig)
            {
                trampoline = TrampolinePool.Rent(sig);
                alive = hasOwnership = true;
            }

            public void Dispose()
            {
                lock (this)
                {
                    if (!alive)
                    {
                        return;
                    }
                    alive = false;

                    if (hasOwnership)
                    {
                        TrampolinePool.Return(trampoline);
                    }
                }
            }

            public void StealTrampolineOwnership()
            {
                lock (this)
                {
                    Helpers.Assert(alive && hasOwnership);
                    hasOwnership = false;
                }
            }

            public void ReturnTrampolineOwnership()
            {
                lock (this)
                {
                    Helpers.Assert(!hasOwnership);

                    if (!alive)
                    {
                        TrampolinePool.Return(trampoline);
                    }
                    else
                    {
                        hasOwnership = true;
                    }
                }
            }

        }

        private readonly DetourManager.ManagedDetourState state;
        private readonly DetourManager.SingleManagedDetourState detour;

        /// <summary>
        /// Constructs a <see cref="Hook"/> using the specified source and target methods, specified target object, specified <see cref="IDetourFactory"/> and <see cref="DetourConfig"/>,
        /// specifying whether or not the hook should be applied when the constructor exits.
        /// </summary>
        /// <param name="source">The source method.</param>
        /// <param name="target">The target method.</param>
        /// <param name="targetObject">The <see langword="this"/> object to call the target method on.</param>
        /// <param name="factory">The <see cref="IDetourFactory"/> to use when manipulating this <see cref="Hook"/>.</param>
        /// <param name="config">The <see cref="DetourConfig"/> to use for this <see cref="Hook"/>.</param>
        /// <param name="applyByDefault">Whether or not this hook should be applied when the constructor finishes.</param>
        public Hook(MethodBase source, MethodInfo target, object? targetObject, IDetourFactory? factory, DetourConfig? config, bool applyByDefault)
        {
            Helpers.ThrowIfArgumentNull(source);
            Helpers.ThrowIfArgumentNull(target);
            Helpers.ThrowIfArgumentNull(factory);

            state = DetourManager.GetDetourState(source);

            this.factory = factory;
            Config = config;
            Source = PlatformTriple.Current.GetIdentifiable(source);

            Target = target;
            realTarget = PrepareRealTarget(targetObject, config?.CelesteLegacyDetour ?? false);

            MMDbgLog.Trace($"Creating Hook from {Source} to {Target}");

            detour = new(this);

            if (applyByDefault)
            {
                Apply();
            }
        }

        private Delegate PrepareRealTarget(object? target, bool trampoline = false)
        {
            CheckSupported();

            var builder = state.Builder;
            var srcSig = MethodSignature.ForMethod(Source);
            var dstSig = MethodSignature.ForMethod(Target, ignoreThis: true); // the dest sig we don't want to consider its this param
            var hookSig = MethodSignature.ForMethod(builder.Hook.GetMethod("Invoke")!, ignoreThis: true);

            if (target is null && !Target.IsStatic)
            {
                throw new ArgumentException("Target method is nonstatic, but no target object was provided");
            }

            if (target is not null && Target.IsStatic)
            {
                throw new ArgumentException("Target method is static, but a target object was provided");
            }

            Type? nextDelegateType = null;
            if (dstSig.ParameterCount == srcSig.ParameterCount + 1)
            {
                // the dest method has a delegate as its first parameter
                nextDelegateType = dstSig.FirstParameter;
                Helpers.DAssert(nextDelegateType is not null);
                dstSig = new MethodSignature(dstSig.ReturnType, dstSig.Parameters.Skip(1));
            }

            if (!srcSig.IsCompatibleWith(dstSig))
            {
                throw new ArgumentException("Target method is not compatible with source method");
            }

            if (nextDelegateType is null)
            {
                using var dmd = hookSig.CreateDmd(DebugFormatter.Format($"Hook<{Target.GetID()}>"));
                using ILContext il = new(dmd.Definition);
                var ic = dmd.GetILProcessor();
                var i = 1;

                if (target is not null)
                {
                    dmd.Definition.Parameters.Insert(0, new(il.Import(target.GetType())));
                    ic.Emit(OpCodes.Ldarg_0);
                    i++;
                }

                if (trampoline)
                {
                    LegacyTrampoline?.Dispose();
                    LegacyTrampoline = new(dstSig, builder.Orig);
                    if (target is not null)
                    {
                        ic.Emit(OpCodes.Ldfld, LegacyTrampolineData.getSlot);
                        LegacyTrampoline.slot = target;
                        dmd.Definition.Parameters[0] = new(il.Import(typeof(LegacyTrampolineData)));
                    }
                    else
                    {
                        dmd.Definition.Parameters.Insert(0, new(il.Import(typeof(LegacyTrampolineData))));
                        i++;
                    }
                    target = LegacyTrampoline;
                    ic.Emit(OpCodes.Ldarg_0);
                    ic.Emit(OpCodes.Ldarg_1);
                    ic.Emit(OpCodes.Stfld, LegacyTrampolineData.getTarget);
                }

                for (; i < dmd.Definition.Parameters.Count; i++)
                {
                    ic.Emit(OpCodes.Ldarg, i);
                }

                ic.Emit(OpCodes.Call, Target);
                ic.Emit(OpCodes.Ret);
                return dmd.Generate().CreateDelegate(builder.Hook, target);
            }

            // we want to check that the delegate invoke is also compatible with the source sig
            var invokeSig = MethodSignature.ForMethod(nextDelegateType.GetMethod("Invoke")!, ignoreThis: true);
            // if it takes a delegate parameter, the trampoline signature should match that delegate

            if (!invokeSig.IsCompatibleWith(srcSig))
            {
                throw new ArgumentException("Target method's delegate parameter is not compatible with the source method");
            }

            if (nextDelegateType == builder.Orig)
            {
                MMDbgLog.Warning("[ColdPatch.Hook] Generated type was referenced. Is it intended?");
                try
                {
                    return Delegate.CreateDelegate(builder.Hook, target, Target);
                }
                catch { }
            }

            {
                var needPointer = !srcSig.IsDelegateCompatibleWith(invokeSig);
                var _type = needPointer ? typeof(FunctionPointerConvertor<,,>) : typeof(CompatibleConverter<,,>);
                var covtype = _type.MakeGenericType(nextDelegateType, builder.Orig, target?.GetType() ?? typeof(object));

                var wrap = Activator.CreateInstance(covtype);

                if (needPointer)
                {
                    var pointer = covtype.GetField(nameof(_Conv.Invoker))!;
                    using var inv = invokeSig.CreateDmd(DebugFormatter.Format($"Calli<{Target.GetID()}>"));
                    var call = new Mono.Cecil.CallSite(inv.Definition.ReturnType);
                    call.Parameters.Add(new(inv.Module.TypeSystem.Object));
                    call.Parameters.AddRange(inv.Definition.Parameters);

                    var box = typeof(UnwrappedDelegate);
                    inv.Definition.Parameters.Insert(0, new(inv.Module.ImportReference(box)));

                    var ili = inv.GetILProcessor();
                    ili.Emit(OpCodes.Ldarg_0);
                    ili.Emit(OpCodes.Ldfld, box.GetField(nameof(UnwrappedDelegate.Target))!);
                    for (var i = 1; i < inv.Definition.Parameters.Count; i++)
                    {
                        ili.Emit(OpCodes.Ldarg, i);
                    }
                    ili.Emit(OpCodes.Ldarg_0);
                    ili.Emit(OpCodes.Ldfld, box.GetField(nameof(UnwrappedDelegate.MethodPointer))!);
                    ili.Emit(OpCodes.Tail);
                    ili.Emit(OpCodes.Calli, call);
                    ili.Emit(OpCodes.Ret);

                    pointer.SetValue(wrap, inv.Generate());
                }

                using var dmd = hookSig.CreateDmd(DebugFormatter.Format($"Hook<{Target.GetID()}>"));
                var il = (dmd.Module);
                var ic = dmd.GetILProcessor();
                dmd.Definition.Parameters.Insert(0, new(il.ImportReference(covtype)));

                if (target is not null)
                {
                    var slot = covtype.GetField(nameof(_Conv.Slot))!;
                    slot.SetValue(wrap, target);
                    ic.Emit(OpCodes.Ldarg_0);
                    ic.Emit(OpCodes.Ldfld, slot);
                }

                ic.Emit(OpCodes.Ldarg_0);
                ic.Emit(OpCodes.Ldarg_1);
                ic.Emit(OpCodes.Callvirt, covtype.GetMethod(nameof(_Conv.ProcessDelegate))!);

                for (var i = 2; i < dmd.Definition.Parameters.Count; i++)
                {
                    ic.Emit(OpCodes.Ldarg, i);
                }
                ic.Emit(OpCodes.Call, Target);
                ic.Emit(OpCodes.Ret);
                var ret = dmd.Generate();
                var test1 = MethodSignature.ForMethod(ret);
                return ret.CreateDelegate(builder.Hook, wrap);
            }
        }

        private void CheckSupported()
        {
            if (Source.IsGenericMethod || Source.DeclaringType is { IsGenericType: true })
                throw new ArgumentException("Source method is generic, generic hooks are not supported");
            if (Source.GetMethodBody()?.GetILAsByteArray() is null)
            {
                throw new NotSupportedException("Source does not have il body");
            }
        }

        private void CheckDisposed()
        {
            if (disposedValue)
                throw new ObjectDisposedException(ToString());
        }

        /// <summary>
        /// Applies this hook if it was not already applied.
        /// </summary>
        public void Apply()
        {
            CheckDisposed();

            var lockTaken = false;
            try
            {
                state.detourLock.Enter(ref lockTaken);
                if (IsApplied)
                    return;
                MMDbgLog.Trace($"Applying Hook from {Source} to {Target}");
                state.AddDetour(detour, !lockTaken);
            }
            finally
            {
                if (lockTaken)
                    state.detourLock.Exit(true);
            }
        }

        /// <summary>
        /// Undoes this hook if it was applied.
        /// </summary>
        public void Undo()
        {
            CheckDisposed();

            var lockTaken = false;
            try
            {
                state.detourLock.Enter(ref lockTaken);
                if (!IsApplied)
                    return;
                MMDbgLog.Trace($"Undoing Hook from {Source} to {Target}");
                state.RemoveDetour(detour, !lockTaken);
            }
            finally
            {
                if (lockTaken)
                    state.detourLock.Exit(true);
            }
        }

        private bool disposedValue;
        /// <summary>
        /// Gets whether or not this hook is valid and can be manipulated.
        /// </summary>
        public bool IsValid => !disposedValue;
        /// <summary>
        /// Gets whether or not this hook is currently applied.
        /// </summary>
        public bool IsApplied => detour.IsApplied;
        /// <summary>
        /// Gets the <see cref="DetourInfo"/> associated with this hook.
        /// </summary>
        public DetourInfo DetourInfo => state.Info.GetDetourInfo(detour);

        private void Dispose(bool disposing)
        {
            if (!disposedValue && detour is not null)
            {
                detour.IsValid = false;
                if (!(AppDomain.CurrentDomain.IsFinalizingForUnload() || Environment.HasShutdownStarted))
                    Undo();

                disposedValue = true;
            }
            LegacyTrampoline?.Dispose();
        }

        /// <summary>
        /// Cleans up and undoes the hook, if needed.
        /// </summary>
        ~Hook()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
