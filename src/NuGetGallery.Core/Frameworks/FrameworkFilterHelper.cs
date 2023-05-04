using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.Frameworks
{
    public static class FrameworkFilterHelper
    {

        /// <summary>
        /// Each Framework Filter Group represents one of the four Framework generations
        /// represented in the Search Filters.
        /// </summary>
        public class FrameworkFilterGroup
        {
            public FrameworkFilterGroup(
                string shortName,
                string displayName,
                List<string> tfms) {
                ShortName = shortName;
                DisplayName = displayName;
                Tfms = tfms;
            }

            public string ShortName { get; set; }
            public string DisplayName { get; set; }
            public List<string> Tfms { get; set; }
        }

        public static readonly Dictionary<string, FrameworkFilterGroup> FrameworkFilters = new Dictionary<string, FrameworkFilterGroup>()
        {
            { 
                AssetFrameworkHelper.FrameworkGenerationIdentifiers.Net,
                new FrameworkFilterGroup(
                    AssetFrameworkHelper.FrameworkGenerationIdentifiers.Net,
                    AssetFrameworkHelper.FrameworkGenerationDisplayNames.Net,
                    SupportedFrameworks.TfmFilters.NetTfms
                        .Select(f => f.GetShortFolderName())
                        .ToList()
                )
            },
            {   
                AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetCoreApp,
                new FrameworkFilterGroup(
                    AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetCoreApp,
                    AssetFrameworkHelper.FrameworkGenerationDisplayNames.NetCoreApp,
                    SupportedFrameworks.TfmFilters.NetCoreAppTfms
                        .Select(f => f.GetShortFolderName())
                        .ToList()
                )
            },
            { 
                AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetStandard,
                new FrameworkFilterGroup(
                    AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetStandard,
                    AssetFrameworkHelper.FrameworkGenerationDisplayNames.NetStandard,
                    SupportedFrameworks.TfmFilters.NetStandardTfms
                        .Select(f => f.GetShortFolderName())
                        .ToList()
                )
            },
            { 
                AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetFramework,
                new FrameworkFilterGroup(
                    AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetFramework,
                    AssetFrameworkHelper.FrameworkGenerationDisplayNames.NetFramework,
                    SupportedFrameworks.TfmFilters.NetFrameworkTfms
                        .Select(f => f.GetShortFolderName())
                        .ToList()
                )
            }
        };
    }
}
