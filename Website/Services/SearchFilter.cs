
namespace NuGetGallery
{
    public class SearchFilter
    {
        public string SearchTerm { get; set; }

        public int Skip { get; set; }

        public int Take { get; set; }

        public bool IncludePrerelease { get; set; }

        public SortProperty SortProperty { get; set; }

        public SortDirection SortDirection { get; set; }
    }

    public enum SortProperty
    {
        Relevance,
        DownloadCount,
        DisplayName,
        Recent,
    }

    public enum SortDirection
    {
        Descending,
        Ascending,
    }
}