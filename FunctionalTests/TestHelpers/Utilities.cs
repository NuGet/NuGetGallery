using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NugetClientSDKHelpers
{
    public class Utilities
    {
        private static string _baseurl;
        /// <summary>
        /// The environment against which the test has to be run. The value would be picked from env variable.
        /// If nothing is specified, preview is used as default.
        /// </summary>
        public static string BaseUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_baseurl))
                {
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("%NuGetGalleryTestUrl%")))
                        _baseurl = "https://preview.nuget.org/";
                    else
                        _baseurl = Environment.GetEnvironmentVariable("%NuGetGalleryTestUrl%");
                }
                
                    return _baseurl;
            }
        }

        public static string FeedUrl
        {
            get
            {
                return BaseUrl + "api/v2/";
            }
        }        
    }
}
