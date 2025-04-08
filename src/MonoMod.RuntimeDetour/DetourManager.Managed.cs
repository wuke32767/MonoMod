#pragma warning disable CS0618
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Core;
using MonoMod.Core.Platforms;
using MonoMod.Logs;
using MonoMod.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace MonoMod.RuntimeDetour
{
    public static partial class DetourManager
    {
        #region Detour chain
        internal sealed class EndOfChainInfo
        {
            public Delegate? Invoke;
            public static FieldInfo invoke = typeof(EndOfChainInfo).GetField(nameof(Invoke))!;
        }

        internal sealed class EntryInfo
        {
            public Delegate? NextEntry;
            public Delegate? Hooker;
            public static FieldInfo entry = typeof(EntryInfo).GetField(nameof(NextEntry))!;
            public static FieldInfo hooker = typeof(EntryInfo).GetField(nameof(Hooker))!;
            public static MethodInfo throws = ((Delegate)ThrowIfNull).Method;
            public static object ThrowIfNull(object check)
            {
                if (check is null)
                {
                    throw new InvalidOperationException("Detour has been removed");
                }
                return check;
            }
            /// <summary>
            /// preview of <see cref="SignatureBuilder.BuildEntry(EntryInfo)"/>
            /// </summary>
            object Invoke(params object[] param)
            {
                return ((Delegate)ThrowIfNull(Hooker!)).DynamicInvoke(NextEntry, param)!;
            }
        }
        internal abstract class ManagedChainNode
        {

            //public ManagedChainNode? Prev;
            public ManagedChainNode? Next;
            public EntryInfo? Info;

            public abstract MethodBase Entry { get; }
            public abstract MethodBase? NextTrampoline { get; }
            public abstract DetourConfig? Config { get; }
            public virtual bool DetourToFallback => true;
            public abstract Delegate InvokeEntry { get; }

            private ICoreDetour? trampolineDetour;
            private bool hasStolenTrampoline;

            public bool IsApplied { get; private set; }

            private void UndoTrampolineDetour()
            {
                var detour = Interlocked.Exchange(ref trampolineDetour, null);
                if (detour is not null)
                {
                    detour.Undo();
                    // TODO: cache trampolineDetours for a time, so they can be reused
                    detour.Dispose();
                }
            }

            public virtual void UpdateDetour(IDetourFactory factory, Delegate fallback, SignatureBuilder builder)
            {
                Helpers.Assert(!hasStolenTrampoline);
                Helpers.Assert(Info is not null);

                if (Next?.Info is null && DetourToFallback)
                {
                    Info.Hooker = fallback;
                    Info.NextEntry = null;
                }
                else
                {
                    Helpers.Assert(Next is not null, "Unreachable: TODO: Generate a empty method");
                    Info.Hooker = Next!.InvokeEntry;
                    Info.NextEntry = builder.BuildEntry(Next.Info!);
                }

                IsApplied = true;
            }
            public virtual void UpdateEntry(SignatureBuilder info)
            {
                if (Info is not null)
                {
                    Info.Hooker = null;
                    Info.NextEntry = null;
                }
                Info = new();
            }

            public void Remove()
            {
                if (!hasStolenTrampoline)
                {
                    UndoTrampolineDetour();
                }
                //Prev = null;
                Next = null;
                IsApplied = false;
            }

            public void StealTrampoline(IDetourFactory factory)
            {
            }
            protected virtual void StealTrampolineInner() => throw new NotSupportedException("Can't steal ManagedChainNode trampoline");

            public virtual void ReturnStolenTrampoline()
            {
            }
            protected virtual void ReturnStolenTrampolineInner() => throw new NotSupportedException("Can't steal ManagedChainNode trampoline");

        }

        internal sealed class ManagedDetourChainNode : ManagedChainNode
        {
            public ManagedDetourChainNode(SingleManagedDetourState detour)
            {
                Detour = detour;
            }

            public readonly SingleManagedDetourState Detour;

            public override MethodBase Entry => Detour.InvokeTarget;
            public override MethodBase NextTrampoline => Detour.NextTrampoline.TrampolineMethod;
            public override DetourConfig? Config => Detour.Config;
            public IDetourFactory Factory => Detour.Factory;
            public override Delegate InvokeEntry => Detour.InvokeDelegate;

            protected override void StealTrampolineInner() => Detour.NextTrampoline.StealTrampolineOwnership();
            protected override void ReturnStolenTrampolineInner() => Detour.NextTrampoline.ReturnTrampolineOwnership();
        }

        internal sealed class ManagedDetourSyncInfo : DetourSyncInfo
        {
            public Delegate? Entry;
            //public int HasStolenTrampolines;
            //public readonly ConcurrentQueue<ManagedChainNode> TrampolineStealers = new();

            public void StealTrampoline(IDetourFactory factory, ManagedChainNode node)
            {
            }

            public void ReturnStolenTrampolines()
            {
            }

        }

        private static readonly MethodInfo ManagedDetourSyncInfo_ReturnStolenTrampolines = typeof(ManagedDetourSyncInfo).GetMethod(nameof(ManagedDetourSyncInfo.ReturnStolenTrampolines))!;

        // The root node is the existing method. It's NextTrampoline is the method, which is the same
        // as the entry point, because we want to detour the entry point. Entry should never be targeted though.
        internal sealed class RootManagedChainNode : ManagedChainNode
        {
            public override MethodBase Entry { get; }
            public override MethodBase? NextTrampoline { get; }
            public override DetourConfig? Config => null;
            public override bool DetourToFallback => true; // we do want to detour to fallback, because our sync proxy might be waiting to call the method
            public override Delegate InvokeEntry => null!;

            public Delegate SyncProxyFunc { get; private set; } = null!;
            public readonly SignatureBuilder Builder;
            public MethodSignature Sig => Builder.Sig;
            public readonly ManagedDetourSyncInfo SyncInfo = new();
            public readonly ConcurrentQueue<Action> StolenTrampolineReturners = new();
            private readonly DataScope<DynamicReferenceCell> syncProxyRefScope;

            public bool HasILHook;

            static FieldInfo? detourList = typeof(ManagedDetourState).GetField("detourList", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            static FieldInfo? _syncInfo = typeof(RootManagedChainNode).GetField("_syncInfo", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            static FieldInfo? _syncDelegate = typeof(ManagedDetourSyncInfo).GetField("Entry", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            public RootManagedChainNode(MethodBase method, SignatureBuilder ext)
            {
                Builder = ext;
                Entry = method;
                Info = new();
                //NextTrampoline = TrampolinePool.Rent(Sig);

                var orig = ext.Orig;

                DataScope<DynamicReferenceCell> refScope = default;
                SyncInfo.SyncProxy = GenerateSyncProxy(DebugFormatter.Format($"{Entry}"), Sig,
                    (method, il) => refScope = il.EmitNewTypedReference(SyncInfo, out _),
                    (method, il, loadSyncInfo) =>
                    {
                        loadSyncInfo();
                        il.Emit(OpCodes.Ldfld, _syncDelegate!);
                        il.Emit(OpCodes.Castclass, orig);
                        foreach (var p in method.Parameters)
                        {
                            il.Emit(OpCodes.Ldarg, p);
                        }
                        il.Emit(OpCodes.Call, method.Module.ImportReference(orig.GetMethod("Invoke")));
                    },
                    (method, il, loadSyncInfo) =>
                    {
                        // we keep the stolen trampolines alive a bit longer than required by only returning them once *all* threads have returned from the method
                        // but doing it this way avoids an expensive TLV lookup to track per-thread active calls
                        loadSyncInfo();
                        il.Emit(OpCodes.Call, ManagedDetourSyncInfo_ReturnStolenTrampolines);
                    });
                syncProxyRefScope = refScope;
            }

            private ICoreDetour? syncDetour;

            public override void UpdateDetour(IDetourFactory factory, Delegate fallback, SignatureBuilder PatchInfo)
            {
                base.UpdateDetour(factory, fallback, PatchInfo);

                Helpers.Assert(Info is not null);
                SyncInfo.Entry = PatchInfo.BuildEntry(Info);

                Helpers.Assert(syncDetour is not null);

                if (!HasILHook && Next is null && syncDetour.IsApplied)
                {
                    syncDetour.Undo();
                    syncDetour.Dispose();
                    syncDetour = null;
                }
                else if ((HasILHook || Next is not null) && !syncDetour.IsApplied)
                {
                    syncDetour.Apply();
                }
            }

            private MethodInfo? sourceClone;
            private DynamicMethodDefinition? sourceCloneIl;

            public void PrepareDetour(IDetourFactory factory, out MethodInfo sourceClone, out DynamicMethodDefinition? sourceCloneIl)
            {
                if (syncDetour is null)
                {
                    var detour = syncDetour = factory.CreateDetour(new(Entry, SyncInfo.SyncProxy!)
                    {
                        ApplyByDefault = false,
                        CreateSourceCloneIfNotILClone = true,
                    });

                    if (detour is ICoreDetourWithClone { SourceMethodClone: { } clone } detourWithClone)
                    {
                        // if a clone was created here, then it's not an IL-copy, and we have no choice but to throw away the old one.
                        sourceClone = this.sourceClone = clone;

                        this.sourceCloneIl?.Dispose();
                        sourceCloneIl = this.sourceCloneIl = detourWithClone.SourceMethodCloneIL;
                    }
                    else
                    {
                        // need to manually create the source clone
                        // we only do this if we don't already have one though, because we don't want to re-copy the IL body
                        sourceCloneIl = this.sourceCloneIl ??= new DynamicMethodDefinition(Entry);
                        sourceClone = this.sourceClone ??= sourceCloneIl.Generate();
                    }
                }
                else
                {
                    Helpers.Assert(this.sourceClone is not null);
                    sourceClone = this.sourceClone;
                    sourceCloneIl = this.sourceCloneIl;
                }
            }
        }
        #endregion

        #region ILHook chain
        internal sealed class ILHookEntry
        {
            public readonly SingleILHookState Hook;

            public IDetourFactory Factory => Hook.Factory;
            public DetourConfig? Config => Hook.Config;
            public ILContext.Manipulator Manip => Hook.Manip;
            public ILContext? CurrentContext;
            public ILContext? LastContext;
            public bool IsApplied;

            public ILHookEntry(SingleILHookState hook)
            {
                Hook = hook;
            }

            public void Remove()
            {
                IsApplied = false;
                LastContext?.Dispose();
            }
        }
        #endregion

        internal sealed class SignatureBuilder
        {
            internal static readonly ConcurrentDictionary<MethodSignature, SignatureBuilder> builders = new();
            public MethodSignature Sig;
            public Type Orig;
            public Type Hook;
            public MethodInfo Built;
            public MethodInfo BuiltEoc;
            public static SignatureBuilder For(MethodSignature sig) => builders.GetOrAdd(sig, _ => new(sig));
            public SignatureBuilder(MethodSignature sig)
            {
                Sig = sig;
                (Orig, Hook) = FastDelegateInvokers.GetDelegateType(Sig);
                var dmd = Sig.CreateDmd($"Intermediate<{Sig}>");
                var mod = dmd.Module;
                dmd.Definition.Parameters.Insert(0, new(mod.ImportReference(typeof(EntryInfo))));
                var il = dmd.GetILProcessor();

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, EntryInfo.hooker);
                il.Emit(OpCodes.Call, EntryInfo.throws);

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, EntryInfo.entry);

                foreach (var i in dmd.Definition.Parameters.Skip(1))
                {
                    il.Emit(OpCodes.Ldarg, i);
                }

                il.Emit(OpCodes.Callvirt, Hook.GetMethod("Invoke")!);

                il.Emit(OpCodes.Ret);
                Built = dmd.Generate();

                dmd.Definition.Name = $"EOC<{Sig}>";
                dmd.Definition.Parameters[0].ParameterType = mod.ImportReference(typeof(EndOfChainInfo));
                dmd.Definition.Parameters.Insert(1, new(mod.ImportReference(Orig)));
                dmd.Definition.Body.Instructions.Clear();
                il = dmd.GetILProcessor();

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, EndOfChainInfo.invoke);
                il.Emit(OpCodes.Call, EntryInfo.throws);

                foreach (var i in dmd.Definition.Parameters.Skip(2))
                {
                    il.Emit(OpCodes.Ldarg, i);
                }

                il.Emit(OpCodes.Callvirt, Orig.GetMethod("Invoke")!);
                il.Emit(OpCodes.Ret);
                BuiltEoc = dmd.Generate();
            }

            /// <summary>
            /// efficient version of <see cref="EntryInfo.Invoke(object[])"/>
            /// </summary>
            public Delegate BuildEntry(EntryInfo info)
            {
                return Built.CreateDelegate(Orig, info);
            }
            public Delegate MakeEoc(EndOfChainInfo info)
            {
                return BuiltEoc.CreateDelegate(Hook, info);
            }
        }
        internal sealed class ManagedDetourState
        {
            public readonly MethodBase Source;
            public MethodInfo? SourceClone;
            public DynamicMethodDefinition? SourceCloneIl;
            public readonly SignatureBuilder Builder;
            public MethodInfo? EndOfChain;
            public EndOfChainInfo EndOfChainDelegate = new();

            public ManagedDetourState(MethodBase src)
            {
                var sig = MethodSignature.ForMethod(src, false);
                Source = src;
                Builder = SignatureBuilder.For(sig);
                detourList = new(src, Builder);
            }

            private MethodDetourInfo? info;
            public MethodDetourInfo Info => info ??= new(this);

            private readonly DepGraph<ManagedChainNode> detourGraph = new();
            internal readonly RootManagedChainNode detourList;
            private ManagedChainNode? noConfigChain;

            internal SpinLock detourLock = new(true);
            internal int detourChainVersion;

            public void AddDetour(SingleManagedDetourState detour, bool takeLock = true)
            {
                ManagedDetourChainNode cnode;
                var lockTaken = false;
                try
                {
                    if (takeLock)
                        detourLock.Enter(ref lockTaken);
                    if (detour.ManagerData is not null)
                        throw new InvalidOperationException("Trying to add a detour which was already added");

                    cnode = new ManagedDetourChainNode(detour);
                    cnode.UpdateEntry(Builder);
                    detourChainVersion++;
                    if (cnode.Config is { } cfg)
                    {
                        var listNode = new DepListNode<ManagedChainNode>(cfg, cnode);
                        var graphNode = new DepGraphNode<ManagedChainNode>(listNode);

                        detourGraph.Insert(graphNode);

                        detour.ManagerData = graphNode;
                    }
                    else
                    {
                        cnode.Next = noConfigChain;
                        noConfigChain = cnode;

                        detour.ManagerData = cnode;
                    }

                    PrepareEndOfChain(detour.Factory);
                    UpdateChain(detour.Factory, out _);
                }
                finally
                {
                    if (lockTaken)
                        detourLock.Exit(true);
                }

                // TODO: make sure this ACTUALLY called outside of the lock
                InvokeDetourEvent(DetourManager.DetourApplied, DetourApplied, detour);
            }

            public void RemoveDetour(SingleManagedDetourState detour, bool takeLock = true)
            {
                ManagedDetourChainNode cnode;
                var lockTaken = false;
                try
                {
                    if (takeLock)
                        detourLock.Enter(ref lockTaken);
                    detourChainVersion++;
                    switch (Interlocked.Exchange(ref detour.ManagerData, null))
                    {
                        case null:
                            throw new InvalidOperationException("Trying to remove detour which wasn't added");

                        case DepGraphNode<ManagedChainNode> gn:
                            RemoveGraphDetour(detour, gn);
                            cnode = (ManagedDetourChainNode)gn.ListNode.ChainNode;
                            break;

                        case ManagedDetourChainNode cn:
                            RemoveNoConfigDetour(detour, cn);
                            cnode = cn;
                            break;

                        default:
                            throw new InvalidOperationException("Trying to remove detour with unknown manager data");
                    }
                }
                finally
                {
                    if (lockTaken)
                        detourLock.Exit(true);
                }

                // TODO: make sure this ACTUALLY called outside of the lock
                InvokeDetourEvent(DetourManager.DetourUndone, DetourUndone, detour);
            }

            private void RemoveGraphDetour(SingleManagedDetourState detour, DepGraphNode<ManagedChainNode> node)
            {
                node.ListNode.ChainNode.UpdateEntry(Builder);
                detourGraph.Remove(node);
                PrepareEndOfChain(detour.Factory);
                UpdateChain(detour.Factory, out var stealTrampoline);
                if (stealTrampoline)
                {
                    detourList.SyncInfo.StealTrampoline(detour.Factory, node.ListNode.ChainNode);
                }
                node.ListNode.ChainNode.Remove();
            }

            private void RemoveNoConfigDetour(SingleManagedDetourState detour, ManagedDetourChainNode node)
            {
                node.UpdateEntry(Builder);
                ref var chain = ref noConfigChain;
                ManagedChainNode? prev = null;
                while (chain is not null)
                {
                    if (ReferenceEquals(chain, node))
                    {
                        chain = node.Next;
                        if (node.Next is { })
                        {
                            //node.Next.Prev = prev;
                        }
                        node.Next = null;
                        break;
                    }

                    prev = chain;
                    chain = ref chain.Next;
                }

                PrepareEndOfChain(detour.Factory);
                UpdateChain(detour.Factory, out var stealTrampoline);
                if (stealTrampoline)
                {
                    detourList.SyncInfo.StealTrampoline(detour.Factory, node);
                }
                node.Remove();
            }

            internal readonly DepGraph<ILHookEntry> ilhookGraph = new();
            internal readonly List<ILHookEntry> noConfigIlhooks = new();

            internal int ilhookVersion;
            public void AddILHook(SingleILHookState ilhook, bool takeLock = true)
            {
                ILHookEntry entry;
                var lockTaken = false;
                try
                {
                    if (takeLock)
                        detourLock.Enter(ref lockTaken);
                    if (ilhook.ManagerData is not null)
                        throw new InvalidOperationException("Trying to add an IL hook which was already added");

                    entry = new ILHookEntry(ilhook);
                    ilhookVersion++;
                    if (entry.Config is { } cfg)
                    {
                        var listNode = new DepListNode<ILHookEntry>(cfg, entry);
                        var graphNode = new DepGraphNode<ILHookEntry>(listNode);

                        ilhookGraph.Insert(graphNode);

                        ilhook.ManagerData = graphNode;
                    }
                    else
                    {
                        noConfigIlhooks.Add(entry);
                        ilhook.ManagerData = entry;
                    }

                    try
                    {
                        PrepareEndOfChain(ilhook.Factory);
                        UpdateEndOfChain();
                    }
                    catch
                    {
                        // the add failed, remove the node and re-update end of chain
                        switch (Interlocked.Exchange(ref ilhook.ManagerData, null))
                        {
                            case DepGraphNode<ILHookEntry> gn:
                                ilhookGraph.Remove(gn);
                                break;
                            case ILHookEntry cn:
                                noConfigIlhooks.Remove(cn);
                                break;
                            default:
                                throw new NotSupportedException("bad managerdata?");
                        }
                        UpdateEndOfChain();
                        throw;
                    }

                    UpdateChain(ilhook.Factory, out _);
                }
                finally
                {
                    if (lockTaken)
                        detourLock.Exit(true);
                }

                // TODO: make sure this ACTUALLY called outside of the lock
                InvokeILHookEvent(DetourManager.ILHookApplied, ILHookApplied, ilhook);
            }

            public void RemoveILHook(SingleILHookState ilhook, bool takeLock = true)
            {
                ILHookEntry entry;
                var lockTaken = false;
                try
                {
                    if (takeLock)
                        detourLock.Enter(ref lockTaken);
                    ilhookVersion++;
                    switch (Interlocked.Exchange(ref ilhook.ManagerData, null))
                    {
                        case null:
                            throw new InvalidOperationException("Trying to remove IL hook which wasn't added");

                        case DepGraphNode<ILHookEntry> gn:
                            RemoveGraphILHook(ilhook, gn);
                            entry = gn.ListNode.ChainNode;
                            break;

                        case ILHookEntry cn:
                            RemoveNoConfigILHook(ilhook, cn);
                            entry = cn;
                            break;

                        default:
                            throw new InvalidOperationException("Trying to remove IL hook with unknown manager data");
                    }
                }
                finally
                {
                    if (lockTaken)
                        detourLock.Exit(true);
                }

                // TODO: make sure this ACTUALLY called outside of the lock
                InvokeILHookEvent(DetourManager.ILHookUndone, ILHookUndone, ilhook);
            }

            private void RemoveGraphILHook(SingleILHookState ilhook, DepGraphNode<ILHookEntry> node)
            {
                ilhookGraph.Remove(node);
                PrepareEndOfChain(ilhook.Factory);
                UpdateEndOfChain();
                UpdateChain(ilhook.Factory, out _);
                CleanILContexts();
                node.ListNode.ChainNode.Remove();
            }

            private void RemoveNoConfigILHook(SingleILHookState ilhook, ILHookEntry node)
            {
                noConfigIlhooks.Remove(node);
                PrepareEndOfChain(ilhook.Factory);
                UpdateEndOfChain();
                UpdateChain(ilhook.Factory, out _);
                CleanILContexts();
                node.Remove();
            }

            private void PrepareEndOfChain(IDetourFactory factory)
            {
                detourList.PrepareDetour(factory, out SourceClone, out SourceCloneIl);
                if (EndOfChain is null)
                {
                    EndOfChain = SourceClone;
                    EndOfChainDelegate.Invoke = null;
                    EndOfChainDelegate = new();
                    EndOfChainDelegate.Invoke = EndOfChain.CreateDelegate(Builder.Orig);
                }
            }

            private void UpdateEndOfChain()
            {
                //Helpers.Assert(SourceClone is not null);

                if (noConfigIlhooks.Count == 0 && ilhookGraph.ListHead is null)
                {
                    detourList.HasILHook = false;
                    EndOfChain = SourceClone;

                    EndOfChainDelegate.Invoke = null;
                    EndOfChainDelegate = new();
                    EndOfChainDelegate.Invoke = EndOfChain?.CreateDelegate(Builder.Orig);
                    return;
                }
                if (SourceCloneIl is null)
                {
                    throw new InvalidOperationException("Target method cannot be ILHooked");
                }

                detourList.HasILHook = true;

                using var dmd = new DynamicMethodDefinition(SourceCloneIl);

                var def = dmd.Definition!;
                var cur = ilhookGraph.ListHead;
                while (cur is not null)
                {
                    InvokeManipulator(cur.ChainNode, def);
                    cur = cur.Next;
                }

                foreach (var node in noConfigIlhooks)
                {
                    InvokeManipulator(node, def);
                }

                var eoc = dmd.Generate();

                // compile the method in-band to throw for invalid code here
                PlatformTriple.Current.Compile(eoc);

                // don't set EndOfChain until after the method successfully compiles, to ensure some semblance of consistenfy
                Thread.MemoryBarrier();
                EndOfChain = eoc;

                EndOfChainDelegate.Invoke = null;
                EndOfChainDelegate = new();
                EndOfChainDelegate.Invoke = EndOfChain.CreateDelegate(Builder.Orig);
                //PatchInfo.SetILHook(dmd.Generate().CreateDelegate(PatchInfo.BridgeType));
            }

            private static void InvokeManipulator(ILHookEntry entry, MethodDefinition def)
            {
                //entry.LastContext?.Dispose(); // we can't safely clean up the old context until after we've updated the chain to point at the new method
                entry.IsApplied = true;
                var il = new ILContext(def);
                entry.CurrentContext = il;
                il.Invoke(entry.Manip);
                if (il.IsReadOnly)
                {
                    il.Dispose();
                    return;
                }

                // Free the now useless MethodDefinition and ILProcessor references.
                // This also prevents clueless people from storing the ILContext elsewhere
                // and reusing it outside of the IL manipulation context.
                il.MakeReadOnly();
                return;
            }

            private void CleanILContexts()
            {
                var cur = ilhookGraph.ListHead;
                while (cur is not null)
                {
                    CleanContext(cur.ChainNode);
                    cur = cur.Next;
                }

                foreach (var node in noConfigIlhooks)
                {
                    CleanContext(node);
                }

                static void CleanContext(ILHookEntry entry)
                {
                    if (entry.CurrentContext == entry.LastContext)
                        return;
                    var old = entry.LastContext;
                    entry.LastContext = entry.CurrentContext;
                    old?.Dispose();
                }
            }

            private void UpdateChain(IDetourFactory updatingFactory, out bool stealTrampolines)
            {
                Helpers.Assert(SourceClone is not null);
                Helpers.Assert(EndOfChain is not null);

                var graphNode = detourGraph.ListHead;

                ManagedChainNode? chain = null;
                ref var next = ref chain;
                ManagedChainNode? prev = null;
                while (graphNode is not null)
                {
                    //graphNode.ChainNode.Prev = prev;
                    next = graphNode.ChainNode;
                    next = ref next.Next;
                    next = null; // clear it to be safe before continuing
                    prev = graphNode.ChainNode;
                    graphNode = graphNode.Next;
                }

                // after building the chain from the graph list, add the noConfigChain
                next = noConfigChain;

                // our chain is now fully built, with the head in chain
                detourList.Next = chain; // detourList is the head of the real chain, and represents the original method

                Volatile.Write(ref detourList.SyncInfo.UpdatingThread, EnvironmentEx.CurrentManagedThreadId);
                detourList.SyncInfo.WaitForNoActiveCalls(out stealTrampolines);
                try
                {
                    var eoc = Builder.MakeEoc(EndOfChainDelegate);
                    chain = detourList;
                    while (chain is not null)
                    {
                        // we want to use the factory for the next node first
                        var fac = (chain.Next as ManagedDetourChainNode)?.Factory;
                        // then, if that doesn't exist, the current factory
                        fac ??= (chain as ManagedDetourChainNode)?.Factory;
                        // and if that doesn't exist, then the updating factory
                        fac ??= updatingFactory;

                        chain.UpdateDetour(fac, eoc, Builder);

                        chain = chain.Next;
                    }
                }
                finally
                {
                    Volatile.Write(ref detourList.SyncInfo.UpdatingThread, -1);
                }
            }

            public event Action<DetourInfo>? DetourApplied;
            public event Action<DetourInfo>? DetourUndone;
            public event Action<ILHookInfo>? ILHookApplied;
            public event Action<ILHookInfo>? ILHookUndone;

            private void InvokeDetourEvent(Action<DetourInfo>? evt1, Action<DetourInfo>? evt2, SingleManagedDetourState node)
            {
                if (evt1 is not null || evt2 is not null)
                {
                    var info = Info.GetDetourInfo(node);
                    evt1?.Invoke(info);
                    evt2?.Invoke(info);
                }
            }

            private void InvokeILHookEvent(Action<ILHookInfo>? evt1, Action<ILHookInfo>? evt2, SingleILHookState entry)
            {
                if (evt1 is not null || evt2 is not null)
                {
                    var info = Info.GetILHookInfo(entry);
                    evt1?.Invoke(info);
                    evt2?.Invoke(info);
                }
            }
        }

        internal sealed class SingleManagedDetourState : SingleDetourStateBase
        {
            public readonly MethodInfo PublicTarget;
            public readonly MethodInfo InvokeTarget;
            public readonly IDetourTrampoline NextTrampoline;
            public readonly Delegate InvokeDelegate;

            public DetourInfo? DetourInfo;

            public SingleManagedDetourState(IDetour dt) : base(dt)
            {
                PublicTarget = dt.PublicTarget;
                InvokeTarget = dt.InvokeTarget;
                NextTrampoline = dt.NextTrampoline;
                InvokeDelegate = dt.InvokDelegate;
            }
        }

        internal sealed class SingleILHookState : SingleDetourStateBase
        {
            public readonly ILContext.Manipulator Manip;
            public ILHookInfo? HookInfo;

            public SingleILHookState(IILHook hk) : base(hk)
            {
                Manip = hk.Manip;
            }
        }

        private static readonly ConcurrentDictionary<MethodBase, ManagedDetourState> detourStates = new();

        internal static ManagedDetourState GetDetourState(MethodBase method)
        {
            method = PlatformTriple.Current.GetIdentifiable(method);
            return detourStates.GetOrAdd(method, static m => new(m));
        }

        /// <summary>
        /// Gets the <see cref="MethodDetourInfo"/> for the provided method.
        /// </summary>
        /// <param name="method">The <see cref="MethodBase"/> to get a <see cref="MethodDetourInfo"/> for.</param>
        /// <returns>The <see cref="MethodDetourInfo"/> for <paramref name="method"/>.</returns>
        public static MethodDetourInfo GetDetourInfo(MethodBase method)
            => GetDetourState(method).Info;

        /// <summary>
        /// An event which is invoked whenever a detour is applied.
        /// </summary>
        /// <remarks>
        /// <see cref="Hook"/> is the only kind of detour, at present.
        /// </remarks>
        public static event Action<DetourInfo>? DetourApplied;
        /// <summary>
        /// An event which is invoked whenever a detour is undone.
        /// </summary>
        /// <remarks>
        /// <see cref="Hook"/> is the only kind of detour, at present.
        /// </remarks>
        public static event Action<DetourInfo>? DetourUndone;
        /// <summary>
        /// An event which is invoked whenever an <see cref="ILHook"/> is applied.
        /// </summary>
        public static event Action<ILHookInfo>? ILHookApplied;
        /// <summary>
        /// An event which is invoked whenever an <see cref="ILHook"/> is undone.
        /// </summary>
        public static event Action<ILHookInfo>? ILHookUndone;
    }
}
