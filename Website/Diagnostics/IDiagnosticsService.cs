namespace NuGetGallery.Diagnostics
{
    public interface IDiagnosticsService
    {
        IDiagnosticsSource GetSource(string name);
    }
}
