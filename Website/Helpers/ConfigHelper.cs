using System.Web.Mvc;

namespace NuGetGallery
{
    /// <summary>
    /// Helper for reading the site configuration.
    /// </summary>
    public static class ConfigHelper
    {
        /// <summary>
        /// Gets a value indicating whether this Gallery should be run as a company intranet site.
        /// </summary>
        /// <value>
        ///   <c>true</c> if intranet site; otherwise, <c>false</c>.
        /// </value>
        public static bool IsIntranetSite
        {
            get
            {
                var config = DependencyResolver.Current.GetService<IConfiguration>();
                return config.IsIntranetSite;
            }
        }

        /// <summary>
        /// Gets the intranet company URL for the logo at the top of the layout.
        /// </summary>
        /// <value>
        /// The intranet company URL.
        /// </value>
        public static string IntranetCompanyUrl
        {
            get
            {
                var config = DependencyResolver.Current.GetService<IConfiguration>();
                return config.IntranetCompanyUrl;
            }
        }
    }
}