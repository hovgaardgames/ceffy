using System;

namespace Ceffy.Bridge
{
    /// <summary>
    /// Marks an interface as containing Unity Methods -- methods implemented in C#
    /// that can be called from the web/JS side via the Ceffy bridge.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
    public sealed class UnityMethodsAttribute : Attribute { }
}
