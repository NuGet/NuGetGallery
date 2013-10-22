using System;
namespace NuGetGallery.Diagnostics
{
    public interface IDiagnosticsService
    {
        /// <summary>
        /// Gets an <see cref="IDiagnosticsSource"/> by the specified name.
        /// </summary>
        /// <param name="name">The name of the source, it's recommended you use the unqualified type name (i.e. 'UserService')</param>
        /// <returns></returns>
        IDiagnosticsSource GetSource(string name);
    }

    public static class DiagnosticsServiceExtensions
    {
        public static IDiagnosticsSource SafeGetSource(this IDiagnosticsService self, string name)
        {
            // Hyper-defensive code to get a diagnostics source when self could be null AND self.GetSource(name) could return null.
            // Designed to support all kinds of mocking scenarios and basically just never fail :)
            try
            {
                return self == null ?
                    NullDiagnosticsSource.Instance :
                    (self.GetSource(name) ?? NullDiagnosticsSource.Instance);
            }
            catch(Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Error getting trace source: " + ex.ToString());
                return NullDiagnosticsSource.Instance;
            }
        }
    }
}
