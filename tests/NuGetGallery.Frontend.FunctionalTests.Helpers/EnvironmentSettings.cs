using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.FunctionTests.Helpers
{
    /// <summary>
    /// This class reads the various test run settings which are set through env variable.
    /// </summary>
    public class EnvironmentSettings
    {
        #region PrivateFields
        private static string _baseurl;
        private static string testAccountName;
        private static string testAccountPassword;
        private static string testEmailServerHost;

      
        #endregion PrivateFields
        #region Properties


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
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GalleryUrl")))
                        _baseurl = "https://preview.nuget.org/";
                    else
                        _baseurl = Environment.GetEnvironmentVariable("GalleryUrl");
                }

                return _baseurl;
            }
        }

        /// <summary>
        /// The test nuget account name to be used for functional tests.
        /// </summary>
        public static string TestAccountName
        {
            get
            {
                if (string.IsNullOrEmpty(testAccountName))
                {
                    testAccountName = Environment.GetEnvironmentVariable("TestAccountName");
                }
                return testAccountName;
            }
        }

        /// <summary>
        /// The password for the test account.
        /// </summary>
        public static string TestAccountPassword
        {
            get
            {
                if (string.IsNullOrEmpty(testAccountPassword))
                {
                    testAccountPassword = Environment.GetEnvironmentVariable("TestAccountPassword");
                }
                return testAccountPassword;
            }
        }

        public static string TestEmailServerHost
        {
            get
            {
                if (string.IsNullOrEmpty(testEmailServerHost))
                {
                    testEmailServerHost = Environment.GetEnvironmentVariable("TestEmailServerHost");
                }
                return testEmailServerHost;
            }
        }

        #endregion Properties
    }
}
