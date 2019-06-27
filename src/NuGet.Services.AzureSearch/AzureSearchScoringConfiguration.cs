using System.Collections.Generic;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Configurations that control how package search results are scored for relevancy.
    /// The index must be rebuilt after changing these values!
    /// </summary>
    public class AzureSearchScoringConfiguration
    {
        /// <summary>
        /// Weights to increase the importance of matches on specific fields. The keys are
        /// the names of the field whose weights should be modified, listed in <see cref="SearchService.IndexFields"/>.
        /// The values are the weight of that field.
        /// </summary>
        public Dictionary<string, double> FieldWeights { get; set; }

        /// <summary>
        /// The <see cref="SearchDocument.Full.DownloadScore"/> magnitude boost.
        /// This boosts packages with many downloads.
        /// </summary>
        public double DownloadScoreBoost { get; set; }

        /// <summary>
        /// The <see cref="BaseMetadataDocument.Published"/> freshness boost.
        /// This boosts packages that were published recently.
        /// </summary>
        public double PublishedFreshnessBoost { get; set; }
    }
}
