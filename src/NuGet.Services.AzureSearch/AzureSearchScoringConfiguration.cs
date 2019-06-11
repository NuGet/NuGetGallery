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
        /// Controls the log base of <see cref="SearchDocument.Full.LogOfDownloadCount"/>.
        /// Decreasing the base increases the range of values, thereby increasing the boost
        /// for packages with high download counts.
        /// </summary>
        public double DownloadCountLogBase { get; set; }

        /// <summary>
        /// Weights to increase the importance of matches on specific fields. The keys are
        /// the names of the field whose weights should be modified, listed in <see cref="SearchService.IndexFields"/>.
        /// The values are the weight of that field.
        /// </summary>
        public Dictionary<string, double> FieldWeights { get; set; }

        /// <summary>
        /// The <see cref="SearchDocument.Full.LogOfDownloadCount"/> magnitude boost.
        /// This boosts packages with many downloads.
        /// </summary>
        public double LogOfDownloadCountMagnitudeBoost { get; set; }

        /// <summary>
        /// The <see cref="BaseMetadataDocument.Published"/> freshness boost.
        /// This boosts packages that were published recently.
        /// </summary>
        public double PublishedFreshnessBoost { get; set; }
    }
}
