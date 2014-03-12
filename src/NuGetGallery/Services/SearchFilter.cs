using NuGet.Services.Search.Models;
namespace NuGetGallery
{
    public class SearchFilter
    {
        public string SearchTerm { get; set; }

        public int Skip { get; set; }

        public int Take { get; set; }

        public bool IncludePrerelease { get; set; }

        public CuratedFeed CuratedFeed { get; set; }

        public SortOrder SortOrder { get; set; }

        /// <summary>
        ///     Determines if only this is a count only query and does not process the source queryable.
        /// </summary>
        public bool CountOnly { get; set; }
    }
}