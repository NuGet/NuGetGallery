namespace NuGetGallery
{
    public interface IIndexingService
    {
        void UpdateIndex();
        void UpdatePackage(Package package);
    }
}