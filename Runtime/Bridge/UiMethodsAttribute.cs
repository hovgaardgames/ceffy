using System;

namespace Ceffy.Bridge
{
    /// <summary>
    /// Marks an interface as containing UI Methods -- methods implemented in JS/TS
    /// that can be called from the Unity/C# side via the Ceffy bridge.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
    public sealed class UiMethodsAttribute : Attribute { }
}
