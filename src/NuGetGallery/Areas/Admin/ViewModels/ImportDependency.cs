namespace NuGetGallery.Areas.Admin.ViewModels
{
    /// <summary>
    /// Item for displaying dependencies on packages to be imported.
    /// </summary>
    public class ImportDependency
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>
        /// The id.
        /// </value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the version spec for the dependency.
        /// </summary>
        /// <value>
        /// The version spec.
        /// </value>
        public string VersionSpec { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDependency"/> class.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="versionSpec">The versionSpec.</param>
        public ImportDependency(string id, string versionSpec)
        {
            Id = id;
            VersionSpec = versionSpec;
        }
    }
}