using NuGet.Services.Search.Models;
namespace NuGetGallery
{
    public class SearchFilter
    {
        public static readonly string UITypeaheadContext = "UI.Typeahead";
        public static readonly string UISearchContext = "UI.Search";
        public static readonly string ODataInterceptContext = "OData.Intercept";
        public static readonly string ODataSearchContext = "OData.Search";

        public string Context { get; private set; }

        public string SearchTerm { get; set; }

        public int Skip { get; set; }

        public int Take { get; set; }

        public bool IncludePrerelease { get; set; }

        public CuratedFeed CuratedFeed { get; set; }

        public SortOrder SortOrder { get; set; }

        public string SupportedFramework { get; set; }

        /// <summary>
        ///     Determines if only this is a count only query and does not process the source queryable.
        /// </summary>
        public bool CountOnly { get; set; }

        public bool IncludeAllVersions { get; set; }

        /// <summary>
        /// Constructs a new search filter
        /// </summary>
        /// <param name="context">The context in which the search is being executed. See the Constants attached to this class for examples</param>
        public SearchFilter(string context)
        {
            Context = context;
        }
    }
}