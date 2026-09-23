#if !NET5_0_OR_GREATER

// The trimming and AOT annotations live in the base class library from .NET 5 onwards. Routya.Core
// also targets netstandard2.0 and netstandard2.1, where they do not exist, so internal copies are
// declared here to keep one set of method signatures across every target framework.
//
// These copies carry no behaviour. The trimmer only reads the annotations from the assembly a
// consuming application actually references, and an application that trims is on .NET 5 or later,
// so it resolves the real attributes from the .NET build of this library rather than these.

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Indicates that the specified method requires the ability to access members that trimming
    /// cannot statically prove are needed.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class,
        Inherited = false)]
    internal sealed class RequiresUnreferencedCodeAttribute : Attribute
    {
        public RequiresUnreferencedCodeAttribute(string message)
        {
            Message = message;
        }

        public string Message { get; }

        public string Url { get; set; }
    }

    /// <summary>
    /// Specifies which members of a type are accessed dynamically, so that trimming preserves them.
    /// </summary>
    [Flags]
    internal enum DynamicallyAccessedMemberTypes
    {
        None = 0,
        PublicParameterlessConstructor = 0x0001,
        PublicConstructors = 0x0003,
        NonPublicConstructors = 0x0004,
        PublicMethods = 0x0008,
        PublicProperties = 0x0040,
        All = ~None
    }

    /// <summary>
    /// States which members of a type are accessed dynamically, so that trimming keeps them.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter
        | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Method
        | AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct,
        Inherited = false)]
    internal sealed class DynamicallyAccessedMembersAttribute : Attribute
    {
        public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes)
        {
            MemberTypes = memberTypes;
        }

        public DynamicallyAccessedMemberTypes MemberTypes { get; }
    }
}

#endif
