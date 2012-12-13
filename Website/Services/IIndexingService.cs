namespace NuGetGallery
{
    public interface IIndexingService
    {
        void UpdateIndex();
        void UpdateIndex(Package package);
    }
}