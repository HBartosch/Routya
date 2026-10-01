using System.Reflection;
using System.Text;

namespace Routya.SourceGenerators.Emitters
{
    /// <summary>
    /// The attributes every generated type carries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <c>// &lt;auto-generated/&gt;</c> header tells Roslyn analyzers to skip the file, but tooling
    /// outside the compiler does not read it. Coverage tools in particular key off attributes, so
    /// without these the generated dispatcher counts as untested code the consumer never wrote, and
    /// drags their coverage figure down.
    /// </para>
    /// <para>
    /// <c>GeneratedCode</c> is the standard marker, naming the tool and its version.
    /// <c>ExcludeFromCodeCoverage</c> keeps generated dispatch out of the consumer's numbers.
    /// Both are fully qualified so a user type cannot shadow them.
    /// </para>
    /// </remarks>
    internal static class GeneratedTypeAttributes
    {
        private static readonly string ToolVersion =
            typeof(GeneratedTypeAttributes).GetTypeInfo().Assembly.GetName().Version?.ToString() ?? "1.0.0.0";

        /// <summary>
        /// Appends the attributes for a generated type that contains executable code.
        /// </summary>
        public static void AppendForType(StringBuilder sb, string indent)
        {
            AppendGeneratedCode(sb, indent);
            sb.AppendLine(indent + "[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]");
        }

        /// <summary>
        /// Appends the attributes for a generated interface, which has no code to exclude from
        /// coverage.
        /// </summary>
        public static void AppendForInterface(StringBuilder sb, string indent)
        {
            AppendGeneratedCode(sb, indent);
        }

        private static void AppendGeneratedCode(StringBuilder sb, string indent)
        {
            sb.AppendLine(
                indent + "[global::System.CodeDom.Compiler.GeneratedCode(\"Routya.SourceGenerators\", \""
                + ToolVersion + "\")]");
        }
    }
}
