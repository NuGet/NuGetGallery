using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;

namespace NuGetGallery.Services
{
    public class SearchTMConfiguration : ISearchTMConfiguration
    {
        public bool IsSearchTMEnabled { get; }
        public string SearchGalleryQueryServiceType { get; }
        public string SearchGalleryAutocompleteServiceType { get; }

        public SearchTMConfiguration()
            : this(
                isSearchTMEnabled: false,
                searchGalleryQueryServiceType: string.Empty,
                searchGalleryAutocompleteServiceType: string.Empty)
        {
        }

        [JsonConstructor]
        public SearchTMConfiguration(
            bool isSearchTMEnabled,
            string searchGalleryQueryServiceType,
            string searchGalleryAutocompleteServiceType)
        {
            if (searchGalleryQueryServiceType == null)
            {
                throw new ArgumentNullException(nameof(searchGalleryQueryServiceType));
            }

            if (searchGalleryAutocompleteServiceType == null)
            {
                throw new ArgumentNullException(nameof(searchGalleryAutocompleteServiceType));
            }

            IsSearchTMEnabled = isSearchTMEnabled;
            SearchGalleryQueryServiceType = searchGalleryQueryServiceType ?? throw new ArgumentNullException(nameof(searchGalleryQueryServiceType));
            SearchGalleryAutocompleteServiceType = searchGalleryAutocompleteServiceType ?? throw new ArgumentNullException(nameof(searchGalleryAutocompleteServiceType));
        }
    }
}