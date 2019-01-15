using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Services
{
    public interface ISearchTMConfiguration
    {
        bool IsSearchTMEnabled { get; }
        string SearchGalleryQueryServiceType { get; }
        string SearchGalleryAutocompleteServiceType { get; }
    }
}
