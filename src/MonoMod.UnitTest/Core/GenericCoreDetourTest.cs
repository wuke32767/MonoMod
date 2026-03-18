using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using MonoMod.Core;
using MonoMod.Utils;
using Xunit;
using Xunit.Abstractions;

namespace MonoMod.UnitTest.Core
{
    public class TieredCompilationTest(ITestOutputHelper helper) : TestBase(helper)
    {
        static Type self = typeof(TieredCompilationTest);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public int TestFrom<T>() => typeof(T).GetHashCode() | 1; //never returns 0

        static MethodInfo testFrom = self.GetMethod(nameof(TestFrom)).MakeGenericMethod([typeof(string)]);

        public int Rejected() => 0;
        static MethodInfo rejected = self.GetMethod(nameof(Rejected));

        [Fact]
        public void TestTieredCompilation()
        {
            Assert.NotEqual(0, TestFrom<object>());
            using var _ = DetourFactory.Default.CreateDetour(new(testFrom, rejected));
            for (var i = 0; i < 40; i++)
            {
                Assert.Equal(0, TestFrom<object>());
            }

            Thread.Sleep(200);
            for (var i = 0; i < 40; i++)
            {
                Assert.Equal(0, TestFrom<List<object>>());
            }
        }
    }

#pragma warning disable CA1000
#pragma warning disable CA1002
#pragma warning disable CA1034
#pragma warning disable CA1051
#pragma warning disable CA1715
#pragma warning disable CA1823
#pragma warning disable CA1815
#pragma warning disable IDE1060
#pragma warning disable xUnit1013
    public struct LargeStruct
    {
        public long a;
        readonly long _;
        readonly long __;
        readonly long ___;
    }

    public class GeneralTest(ITestOutputHelper helper) : TestBase(helper)
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void InstanceVoid<T>(List<T> list, T item, ref bool injected)
        {
            Assert.Fail("should be detoured");
            return;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public string InstanceRegister<T>(List<T> list, T item, ref bool injected)
        {
            Assert.Fail("should be detoured");
            return "";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public LargeStruct InstanceByRef<T>(List<T> list, T item, ref bool injected)
        {
            Assert.Fail("should be detoured");
            return default;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static LargeStruct StaticByRef<T>(List<T> list, T item, ref bool injected)
        {
            Assert.Fail("should be detoured");
            return default;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void InstanceVoidReal<T>(List<T> list, T item, ref bool injected)
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public string InstanceRegisterReal<T>(List<T> list, T item, ref bool injected)
        {
            return "";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public LargeStruct InstanceByRefReal<T>(List<T> list, T item, ref bool injected)
        {
            return default;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static LargeStruct StaticByRefReal<T>(List<T> list, T item, ref bool injected)
        {
            return default;
        }

        public class Foo
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void StaticMethod<T>(T t)
            {
                Assert.Fail("should be detoured");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InstanceMethod<T>(T t)
            {
                Assert.Fail("should be detoured");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void StaticMethodReal<T>(T t)
            {
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InstanceMethodReal<T>(T t)
            {
            }
        }

        public class Bar<T>
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void StaticMethod(T t)
            {
                Assert.Fail("should be detoured");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InstanceMethod(T t)
            {
                Assert.Fail("should be detoured");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void StaticMethod2<U>(T t, U u)
            {
                Assert.Fail("should be detoured");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InstanceMethod2<U>(T t, U u)
            {
                Assert.Fail("should be detoured");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void StaticMethodReal(T t)
            {
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InstanceMethodReal(T t)
            {
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void StaticMethod2Real<U>(T t, U u)
            {
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InstanceMethod2Real<U>(T t, U u)
            {
            }
        }

        public struct Baz
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void StaticMethod<T>(T t)
            {
                Assert.Fail("should be detoured");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InstanceMethod<T>(T t)
            {
                Assert.Fail("should be detoured");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void StaticMethodReal<T>(T t)
            {
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InstanceMethodReal<T>(T t)
            {
            }
        }

        public struct Qux<T>
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void StaticMethod(T t)
            {
                Assert.Fail("should be detoured");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InstanceMethod(T t)
            {
                Assert.Fail("should be detoured");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void StaticMethod2<U>(T t, U u)
            {
                Assert.Fail("should be detoured");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InstanceMethod2<U>(T t, U u)
            {
                Assert.Fail("should be detoured");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void StaticMethodReal(T t)
            {
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InstanceMethodReal(T t)
            {
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void StaticMethod2Real<U>(T t, U u)
            {
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InstanceMethod2Real<U>(T t, U u)
            {
            }
        }

        [Fact]
        public void TestTieredCompilation()
        {
            Test(
                typeof(GeneralTest).GetMethod("InstanceVoid").MakeGenericMethod(typeof(object)),
                typeof(GeneralTest).GetMethod("InstanceVoidReal").MakeGenericMethod(typeof(object)),
                () => InstanceVoid([], "", ref Unsafe.NullRef<bool>()));
            Test(
                typeof(GeneralTest).GetMethod("InstanceRegister").MakeGenericMethod(typeof(object)),
                typeof(GeneralTest).GetMethod("InstanceRegisterReal").MakeGenericMethod(typeof(object)),
                () => InstanceRegister([], "", ref Unsafe.NullRef<bool>()));
            // TODO: Wait for ABI fixup
            if (PlatformDetection.Architecture is not ArchitectureKind.x86)
            {
                Test(
                    typeof(GeneralTest).GetMethod("InstanceByRef").MakeGenericMethod(typeof(object)),
                    typeof(GeneralTest).GetMethod("InstanceByRefReal").MakeGenericMethod(typeof(object)),
                    () => InstanceByRef([], "", ref Unsafe.NullRef<bool>()));
                Test(
                    typeof(GeneralTest).GetMethod("StaticByRef", BindingFlags.Static | BindingFlags.Public)
                        .MakeGenericMethod(typeof(object)),
                    typeof(GeneralTest).GetMethod("StaticByRefReal", BindingFlags.Static | BindingFlags.Public)
                        .MakeGenericMethod(typeof(object)),
                    () => StaticByRef([], "", ref Unsafe.NullRef<bool>()));
            }

            Test(
                typeof(Foo).GetMethod("StaticMethod", BindingFlags.Static | BindingFlags.Public)
                    .MakeGenericMethod(typeof(object)),
                typeof(Foo).GetMethod("StaticMethodReal", BindingFlags.Static | BindingFlags.Public)
                    .MakeGenericMethod(typeof(object)),
                () => Foo.StaticMethod(""));
            Test(
                typeof(Foo).GetMethod("InstanceMethod").MakeGenericMethod(typeof(object)),
                typeof(Foo).GetMethod("InstanceMethodReal").MakeGenericMethod(typeof(object)),
                () => new Foo().InstanceMethod(""));
            Test(
                typeof(Bar<object>).GetMethod("StaticMethod", BindingFlags.Static | BindingFlags.Public),
                typeof(Bar<object>).GetMethod("StaticMethodReal", BindingFlags.Static | BindingFlags.Public),
                () => Bar<object>.StaticMethod(""));
            Test(
                typeof(Bar<object>).GetMethod("InstanceMethod", BindingFlags.Instance | BindingFlags.Public),
                typeof(Bar<object>).GetMethod("InstanceMethodReal", BindingFlags.Instance | BindingFlags.Public),
                () => new Bar<object>().InstanceMethod(""));
            Test(
                typeof(Bar<object>).GetMethod("StaticMethod2", BindingFlags.Static | BindingFlags.Public)
                    .MakeGenericMethod(typeof(object)),
                typeof(Bar<object>).GetMethod("StaticMethod2Real", BindingFlags.Static | BindingFlags.Public)
                    .MakeGenericMethod(typeof(object)),
                () => Bar<object>.StaticMethod2("", ""));
            Test(
                typeof(Bar<object>).GetMethod("InstanceMethod2", BindingFlags.Instance | BindingFlags.Public)
                    .MakeGenericMethod(typeof(object)),
                typeof(Bar<object>).GetMethod("InstanceMethod2Real", BindingFlags.Instance | BindingFlags.Public)
                    .MakeGenericMethod(typeof(object)),
                () => new Bar<object>().InstanceMethod2("", ""));
            Test(
                typeof(Baz).GetMethod("StaticMethod", BindingFlags.Static | BindingFlags.Public)
                    .MakeGenericMethod(typeof(object)),
                typeof(Baz).GetMethod("StaticMethodReal", BindingFlags.Static | BindingFlags.Public)
                    .MakeGenericMethod(typeof(object)),
                () => Baz.StaticMethod(""));
            Test(
                typeof(Baz).GetMethod("InstanceMethod").MakeGenericMethod(typeof(object)),
                typeof(Baz).GetMethod("InstanceMethodReal").MakeGenericMethod(typeof(object)),
                () => new Baz().InstanceMethod(""));
            Test(
                typeof(Qux<object>).GetMethod("StaticMethod", BindingFlags.Static | BindingFlags.Public),
                typeof(Qux<object>).GetMethod("StaticMethodReal", BindingFlags.Static | BindingFlags.Public),
                () => Qux<object>.StaticMethod(""));
            Test(
                typeof(Qux<object>).GetMethod("InstanceMethod", BindingFlags.Instance | BindingFlags.Public),
                typeof(Qux<object>).GetMethod("InstanceMethodReal", BindingFlags.Instance | BindingFlags.Public),
                () => new Qux<object>().InstanceMethod(""));
            Test(
                typeof(Qux<object>).GetMethod("StaticMethod2", BindingFlags.Static | BindingFlags.Public)
                    .MakeGenericMethod(typeof(object)),
                typeof(Qux<object>).GetMethod("StaticMethod2Real", BindingFlags.Static | BindingFlags.Public)
                    .MakeGenericMethod(typeof(object)),
                () => Qux<object>.StaticMethod2("", ""));
            Test(
                typeof(Qux<object>).GetMethod("InstanceMethod2", BindingFlags.Instance | BindingFlags.Public)
                    .MakeGenericMethod(typeof(object)),
                typeof(Qux<object>).GetMethod("InstanceMethod2Real", BindingFlags.Instance | BindingFlags.Public)
                    .MakeGenericMethod(typeof(object)),
                () => new Qux<object>().InstanceMethod2("", ""));

            void Test(MethodInfo from, MethodInfo to, Action invoke)
            {
                using var _ = DetourFactory.Default.CreateDetour(new(from, to));
                invoke();
            }
        }
    }

    public class WalkThroughTest(ITestOutputHelper helper) : TestBase(helper)
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int TestFrom() => TestFromCore();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public int TestFromCore() => 0;

        public int Reject() => 1919810;

        [Fact]
        public void TestWalkThrough()
        {
            var self = typeof(WalkThroughTest);
            var from = self.GetMethod(nameof(TestFrom));
            var to = self.GetMethod(nameof(Reject));

            Assert.Equal(0, TestFrom());
            Assert.Equal(0, TestFromCore());
            using var _ = DetourFactory.Default.CreateDetour(new(from, to));
            Assert.Equal(1919810, TestFrom());
            Assert.Equal(0, TestFromCore());
        }
    }

    public class CountlessTest(ITestOutputHelper helper) : TestBase(helper)
    {
        public int Method0<T>() => 0;
        public int Method1<T>(T a0) => 1;
        public int Method2<T>(T a0, T a1) => 2;
        public int Method3<T>(T a0, T a1, T a2) => 3;

        public int Method4<T>(T a0, T a1, T a2, T a3) => 4;
        public int Method5<T>(T a0, T a1, T a2, T a3, T a4) => 5;
        public int Method6<T>(T a0, T a1, T a2, T a3, T a4, T a5) => 6;
        public int Method7<T>(T a0, T a1, T a2, T a3, T a4, T a5, T a6) => 7;
        public int Method8<T>(T a0, T a1, T a2, T a3, T a4, T a5, T a6, T a7) => 8;
        public int Method0Real() => GetHashCode() + GetHashCode();
        public int Method1Real(object a0) => GetHashCode() + a0.GetHashCode();
        public int Method2Real(object a0, object a1) => GetHashCode() + a1.GetHashCode();
        public int Method3Real(object a0, object a1, object a2) => GetHashCode() + a2.GetHashCode();

        public int Method4Real(object a0, object a1, object a2, object a3) => GetHashCode() + a3.GetHashCode();
        public int Method5Real(object a0, object a1, object a2, object a3, object a4) => GetHashCode() + a4.GetHashCode();
        public int Method6Real(object a0, object a1, object a2, object a3, object a4, object a5) => GetHashCode() + a5.GetHashCode();
        public int Method7Real(object a0, object a1, object a2, object a3, object a4, object a5, object a6) => GetHashCode() + a6.GetHashCode();
        public int Method8Real(object a0, object a1, object a2, object a3, object a4, object a5, object a6, object a7) => GetHashCode() + a7.GetHashCode();

        [Fact]
        public void TestCountless()
        {
            var self = typeof(CountlessTest);
            for (var i = 0; i < 9; i++)
            {
                var sth = Enumerable.Repeat(this, i).ToArray();
                var from = self.GetMethod($"Method{i}").MakeGenericMethod([typeof(object)]);
                var to = self.GetMethod($"Method{i}Real");

                Assert.Equal(i, from.Invoke(this, sth));
                using var _ = DetourFactory.Default.CreateDetour(new(from, to));
                Assert.Equal(GetHashCode() * 2, (int)from.Invoke(this, sth));
            }
        }
    }

    public class ThisIsAbiTest(ITestOutputHelper helper) : TestBase(helper)
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int Method0<T>() => 0;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public int Method1<T>(T a0) => 1;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Method2<T>(T a0, T a1) => 2;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Method3<T>(T a0, T a1, T a2) => 3;

        public int Method0Real() => 114514;
        public static int Method1Real(object self, object a0) => 114514;
        public int Method2Real(object a1) => 114514;
        public static int Method3Real(object a0, object a1, object a2) => 114514;

        [Fact]
        public void TestOurAbi()
        {
            var self = typeof(ThisIsAbiTest);
            for (var i = 0; i < 4; i++)
            {
                var from = self.GetMethod($"Method{i}").MakeGenericMethod([typeof(object)]);
                var to = self.GetMethod($"Method{i}Real");

                var sth = Enumerable.Repeat(this, from.GetParameters().Length).ToArray();
                var th = from.IsStatic ? null : this;

                Assert.Equal(i, from.Invoke(th, sth));
                using var _ = DetourFactory.Default.CreateDetour(new(from, to));
                Assert.Equal(114514, from.Invoke(th, sth));
            }
        }

        static int OrderedHash(object self, object a0)
        {
            var b = self.GetHashCode() * 5 + a0.GetHashCode();
            return b;
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        public int Method4<T>(T a0) => Throw<int>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public int Method5<T>(T a0) => Throw<int>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Method6<T>(object self, T a0) => Throw<int>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Method7<T>(object self, T a0) => Throw<int>();

        public int Method4Real(object a0) => OrderedHash(this, a0);
        public static int Method5Real(object self, object a0) => OrderedHash(self, a0);
        public int Method6Real(object a0) => OrderedHash(this, a0);
        public static int Method7Real(object self, object a0) => OrderedHash(self, a0);

        [Fact]
        public void TestAbiParams()
        {
            var self = typeof(ThisIsAbiTest);
            var hash = OrderedHash(this, self);
            object[] arr1 = [self];
            object[] arr2 = [this, self];
            for (var i = 4; i < 8; i++)
            {
                var from = self.GetMethod($"Method{i}").MakeGenericMethod([typeof(object)]);
                var to = self.GetMethod($"Method{i}Real");

                var sth = from.IsStatic ? arr2 : arr1;
                var th = from.IsStatic ? null : this;

                using var _ = DetourFactory.Default.CreateDetour(new(from, to));
                Assert.Equal(hash, (int)from.Invoke(th, sth));
            }
        }
        static LargeStruct OrderedHashLarge(object self, object a0)
        {
            var b = self.GetHashCode() * 5 + a0.GetHashCode();
            return new() { a = b };
        }

        static T Throw<T>()
        {
            Assert.Fail("should be detoured");
            return default;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public LargeStruct Method8<T>(T a0) => Throw<LargeStruct>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public LargeStruct Method9<T>(T a0) => Throw<LargeStruct>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static LargeStruct Method10<T>(object self, T a0) => Throw<LargeStruct>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static LargeStruct Method11<T>(object self, T a0) => Throw<LargeStruct>();

        public LargeStruct Method8Real(object a0) => OrderedHashLarge(this, a0);
        public static LargeStruct Method9Real(object self, object a0) => OrderedHashLarge(self, a0);
        public LargeStruct Method10Real(object a0) => OrderedHashLarge(this, a0);
        public static LargeStruct Method11Real(object self, object a0) => OrderedHashLarge(self, a0);

        [Fact]
        public void TestAbiParamsOnLargeObject()
        {
            var self = typeof(ThisIsAbiTest);
            var hash = OrderedHashLarge(this, self);
            object[] arr1 = [self];
            object[] arr2 = [this, self];
            for (var i = 8; i < 12; i++)
            {
                var from = self.GetMethod($"Method{i}").MakeGenericMethod([typeof(object)]);
                var to = self.GetMethod($"Method{i}Real");

                var sth = from.IsStatic ? arr2 : arr1;
                var th = from.IsStatic ? null : this;

                using var _ = DetourFactory.Default.CreateDetour(new(from, to));
                Assert.Equal(hash, (LargeStruct)from.Invoke(th, sth));
            }
        }
    }
}