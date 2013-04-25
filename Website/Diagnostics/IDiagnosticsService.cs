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
}
