using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using MonoMod.Core;
using Xunit;
using Xunit.Abstractions;

namespace MonoMod.UnitTest.Core
{
    public class TieredCompilationTest(ITestOutputHelper helper) : TestBase(helper)
    {
        static Type self = typeof(TieredCompilationTest);

        public int TestFrom<T>() => typeof(T).GetHashCode() | 1; //never returns 0
        static MethodInfo testFrom = self.GetMethod(nameof(TestFrom)).MakeGenericMethod([typeof(string)]);

        public int Rejected() => 0;
        static MethodInfo rejected = self.GetMethod(nameof(Rejected));

        [Fact]
        public void TestTieredCompilation()
        {
            Assert.NotEqual(0, TestFrom<object>());
            DetourFactory.Default.CreateDetour(new(testFrom, rejected));
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
    public class GeneralTest(ITestOutputHelper helper) : TestBase(helper)
    {
        public struct LargeStruct
        {
            public long a; long b; long c; long d;
        }

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
            Test(
                typeof(GeneralTest).GetMethod("InstanceByRef").MakeGenericMethod(typeof(object)),
                typeof(GeneralTest).GetMethod("InstanceByRefReal").MakeGenericMethod(typeof(object)),
                () => InstanceByRef([], "", ref Unsafe.NullRef<bool>()));
            Test(
                typeof(GeneralTest).GetMethod("StaticByRef", BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(object)),
                typeof(GeneralTest).GetMethod("StaticByRefReal", BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(object)),
                () => StaticByRef([], "", ref Unsafe.NullRef<bool>()));
            Test(
                typeof(Foo).GetMethod("StaticMethod", BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(object)),
                typeof(Foo).GetMethod("StaticMethodReal", BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(object)),
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
                typeof(Bar<object>).GetMethod("StaticMethod2", BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(object)),
                typeof(Bar<object>).GetMethod("StaticMethod2Real", BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(object)),
                () => Bar<object>.StaticMethod2("", ""));
            Test(
                typeof(Bar<object>).GetMethod("InstanceMethod2", BindingFlags.Instance | BindingFlags.Public).MakeGenericMethod(typeof(object)),
                typeof(Bar<object>).GetMethod("InstanceMethod2Real", BindingFlags.Instance | BindingFlags.Public).MakeGenericMethod(typeof(object)),
                () => new Bar<object>().InstanceMethod2("", ""));
            Test(
                typeof(Baz).GetMethod("StaticMethod", BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(object)),
                typeof(Baz).GetMethod("StaticMethodReal", BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(object)),
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
                typeof(Qux<object>).GetMethod("StaticMethod2", BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(object)),
                typeof(Qux<object>).GetMethod("StaticMethod2Real", BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(typeof(object)),
                () => Qux<object>.StaticMethod2("", ""));
            Test(
                typeof(Qux<object>).GetMethod("InstanceMethod2", BindingFlags.Instance | BindingFlags.Public).MakeGenericMethod(typeof(object)),
                typeof(Qux<object>).GetMethod("InstanceMethod2Real", BindingFlags.Instance | BindingFlags.Public).MakeGenericMethod(typeof(object)),
                () => new Qux<object>().InstanceMethod2("", ""));
            void Test(MethodInfo from, MethodInfo to, Action invoke)
            {
                DetourFactory.Default.CreateDetour(new(from, to));
                for (var i = 0; i < 40; i++)
                {
                    invoke();
                }
                Thread.Sleep(200);
                for (var i = 0; i < 40; i++)
                {
                    invoke();
                }
            }
        }
    }
}
