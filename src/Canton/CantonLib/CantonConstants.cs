using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public static class CantonConstants
    {
        /// <summary>
        /// Contains the url of an uploaded nupkg, and the gallery details for it.
        /// </summary>
        public const string GalleryPagesQueue = "cantongallerypages";

        /// <summary>
        /// Contains the url of finished catalog pages that need to be added to the index.
        /// </summary>
        public const string CatalogPageQueue = "cantoncatalogpages";

        /// <summary>
        /// Contains finished commits that need registration updates.
        /// </summary>
        public const string CatalogCommitQueue = "cantoncatalogcommits";

        /// <summary>
        /// Contains batches ready for package regs.
        /// </summary>
        public const string RegBatchQueue = "cantonregbatches";

        /// <summary>
        /// Contains the master entry with info about the RegBatchQueue items.
        /// </summary>
        public const string RegMasterBatchQueue = "cantonregbatchesmaster";

        /// Finished registration pages waiting in temp.
        /// </summary>
        public const string RegBatchPagesQueue = "cantonregpages";

        /// <summary>
        /// Canton cursor table.
        /// </summary>
        public const string CursorTable = "cantoncursors";

        /// <summary>
        /// Canton schema base #
        /// </summary>
        public const string CantonSchema = "http://schema.nuget.org/canton#";

        public static readonly DateTime MinSupportedDateTime = DateTime.FromFileTimeUtc(0);
    }
}
