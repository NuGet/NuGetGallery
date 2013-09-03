namespace NuGetGallery
{
    public interface IPreviousNextPager
    {
        bool HasNextPage { get; }
        bool HasPreviousPage { get; }
        string NextPageUrl { get; }
        string PreviousPageUrl { get; }
    }
}