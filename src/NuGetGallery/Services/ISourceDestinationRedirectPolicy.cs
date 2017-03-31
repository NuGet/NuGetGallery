using System;

namespace NuGetGallery
{
    /// <summary>
    /// Represents the policy services might use to allow or not allow redirects 
    /// from and to certain URLs.
    /// </summary>
    public interface ISourceDestinationRedirectPolicy
    {
        /// <summary>
        /// Checks if redirect is allowed 
        /// </summary>
        /// <param name="sourceUrl">The URL being redirected.</param>
        /// <param name="destinationUrl">The destination URL.</param>
        /// <returns>true if redirection is allowed, false otherwise</returns>
        bool IsAllowed(Uri sourceUrl, Uri destinationUrl);
    }
}
