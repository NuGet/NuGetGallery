using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public static class UriUtils
    {
        private const string Filter = "$filter=";
        private const string NonhijackableExpression = "true";
        private const string And = " and ";

        private const string GetSpecificPackageFormatRegExp = 
            @"Packages\(Id='(?<Id>[^']*)',Version='(?<Version>[^']*)'\)";

        /// <summary>
        /// The format of a nonhijacked "Packages(Id='...',Version='...')" request.
        /// Note that we are using "normalized version" here.
        /// This is because a hijacked request does a comparison on the normalized version, but the nonhijacked request does not.
        /// </summary>
        private static string GetSpecificPackageNonhijackable = 
            $"Packages?{Filter}{NonhijackableExpression}{And}" + 
            "Id eq '{0}'" + $"{And}" + "NormalizedVersion eq '{1}'";

        private static IEnumerable<string> HijackableEndpoints = new List<string>
        {
            "/Packages",
            "/Search",
            "/FindPackagesById"
        };

        public static Uri GetNonhijackableUri(Uri originalUri)
        {
            var nonhijackableUri = originalUri;

            var originalUriString = originalUri.ToString();
            string nonhijackableUriString = null;

            // Modify the request uri so that it will not be hijacked by the search service.
            // The main goal here is that we add an expression to the filter of every request.
            if (originalUriString.Contains(Filter))
            {
                // If there is a filter on the request, simply add the expression to the front of the filter.
                nonhijackableUriString = originalUriString.Replace(Filter, Filter + NonhijackableExpression + And);
            }
            else
            {
                // If this is a Packages(Id='...',Version='...') request, rewrite it as a request to Packages() with a filter and add the expression.
                // Note that Packages() returns a feed and Packages(Id='...',Version='...') returns an entry, but this is fine because the client reads both the same.
                var getSpecificPackageMatch = Regex.Match(originalUriString, GetSpecificPackageFormatRegExp);
                if (getSpecificPackageMatch.Success)
                {
                    nonhijackableUriString =
                        originalUriString.Substring(0, getSpecificPackageMatch.Index) +
                        string.Format(
                            GetSpecificPackageNonhijackable, 
                            getSpecificPackageMatch.Groups["Id"].Value, 
                            NuGetVersion.Parse(getSpecificPackageMatch.Groups["Version"].Value).ToNormalizedString());
                }
                else
                {
                    // If this is a request to a hijackable endpoint without a filter, add the filter expression to the end of the request.
                    if (HijackableEndpoints.Any(endpoint => originalUriString.Contains(endpoint)))
                    {
                        var bridgingCharacter = "&";

                        if (!originalUriString.Contains("?"))
                        {
                            bridgingCharacter = "?";
                        }

                        nonhijackableUriString = $"{originalUri}{bridgingCharacter}{Filter}{NonhijackableExpression}";
                    }
                }
            }

            if (nonhijackableUriString != null)
            {
                nonhijackableUri = new Uri(nonhijackableUriString);
            }

            return nonhijackableUri;
        }
    }
}
