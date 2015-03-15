namespace NuGetGallery.Areas.Admin.ViewModels
{
    /// <summary>
    /// View model for importing from the NuGet official feed.
    /// </summary>
    public class ImportDownloadViewModel
    {
        /// <summary>
        /// Gets or sets the package id.
        /// </summary>
        /// <value>
        /// The id.
        /// </value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the package version.
        /// </summary>
        /// <value>
        /// The version.
        /// </value>
        public string Version { get; set; }
    }
}