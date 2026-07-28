// Build-time-only stand-in for the slice of NUnit that Assets/Tests uses, so the
// editor test suite is compile-checked by CI too. Same rules as UnityStub.cs:
// never place under Assets/, and keep the signatures honest.

using System;
using System.Collections;
using System.Collections.Generic;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TestFixtureAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SetUpAttribute : Attribute { }

    public static class Assert
    {
        public static void AreEqual(object expected, object actual) { }
        public static void AreEqual(object expected, object actual, string message) { }
        public static void AreNotEqual(object expected, object actual) { }
        public static void AreNotEqual(object expected, object actual, string message) { }
        public static void IsTrue(bool condition) { }
        public static void IsTrue(bool condition, string message) { }
        public static void IsFalse(bool condition) { }
        public static void IsFalse(bool condition, string message) { }
        public static void IsNull(object value) { }
        public static void IsNull(object value, string message) { }
        public static void IsNotNull(object value) { }
        public static void IsNotNull(object value, string message) { }
        public static void IsEmpty(IEnumerable collection) { }
        public static void IsEmpty(IEnumerable collection, string message) { }
        public static void IsNotEmpty(IEnumerable collection) { }
        public static void IsNotEmpty(IEnumerable collection, string message) { }
        public static void Greater(IComparable a, IComparable b) { }
        public static void Greater(IComparable a, IComparable b, string message) { }
        public static void GreaterOrEqual(IComparable a, IComparable b) { }
        public static void GreaterOrEqual(IComparable a, IComparable b, string message) { }
        public static void Less(IComparable a, IComparable b) { }
        public static void Less(IComparable a, IComparable b, string message) { }
        public static void LessOrEqual(IComparable a, IComparable b) { }
        public static void LessOrEqual(IComparable a, IComparable b, string message) { }
        public static T Throws<T>(TestDelegate code) where T : Exception => null;
    }

    public delegate void TestDelegate();

    public static class CollectionAssert
    {
        public static void AreEqual(IEnumerable expected, IEnumerable actual) { }
        public static void AreEqual(IEnumerable expected, IEnumerable actual, string message) { }
        public static void DoesNotContain(IEnumerable collection, object item) { }
        public static void DoesNotContain(IEnumerable collection, object item, string message) { }
        public static void Contains(IEnumerable collection, object item) { }
    }
}
