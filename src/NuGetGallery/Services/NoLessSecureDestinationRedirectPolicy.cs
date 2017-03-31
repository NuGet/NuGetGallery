using System;

namespace NuGetGallery
{
    /// <summary>
    /// Redirect policy that only allows same or higher security level for destination URL 
    /// compared to source URL (i.e. HTTPS to HTTP redirects are not allowed).
    /// </summary>
    public class NoLessSecureDestinationRedirectPolicy : ISourceDestinationRedirectPolicy
    {
        public bool IsAllowed(Uri sourceUrl, Uri destinationUrl)
        {
            return sourceUrl.Scheme == Uri.UriSchemeHttp 
                || (sourceUrl.Scheme == Uri.UriSchemeHttps && destinationUrl.Scheme == Uri.UriSchemeHttps);
        }
    }
}