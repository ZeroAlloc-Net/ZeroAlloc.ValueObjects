// Polyfill required for 'record' and 'init' in netstandard2.0
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
