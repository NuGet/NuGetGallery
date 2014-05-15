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
        private static string runFunctionalTests;
        private static string readOnlyMode;
      
        #endregion PrivateFields
        #region Properties

        /// <summary>
        /// Option to enable or disable functional tests from the current run.
        /// </summary>
        public static string RunFunctionalTests
        {
            get
            {        
               if (string.IsNullOrEmpty(runFunctionalTests))
                {
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RunFunctionalTests")))
                        runFunctionalTests = "False";
                    else
                        runFunctionalTests = Environment.GetEnvironmentVariable("RunFunctionalTests");
                }
                return runFunctionalTests;
            }
        }

        /// <summary>
        /// Option to enable or disable functional tests from the current run.
        /// </summary>
        public static string ReadOnlyMode
        {
            get
            {
                if (string.IsNullOrEmpty(readOnlyMode))
                {
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ReadOnlyMode")))
                        readOnlyMode = "False";
                    else
                        readOnlyMode = Environment.GetEnvironmentVariable("ReadOnlyMode");
                }
                return readOnlyMode;
            }
        }

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
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GalleryUrl", EnvironmentVariableTarget.User)) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GalleryUrl", EnvironmentVariableTarget.Process)) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GalleryUrl", EnvironmentVariableTarget.Machine)))
                        _baseurl = "https://int.nugettest.org/";
                    else
                    {
                        //Check for the environment variable under all scopes. This is to make sure that the variables are acessed properly in teamcity where we cannot set process leve variables.
                        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GalleryUrl", EnvironmentVariableTarget.User)))
                           _baseurl = Environment.GetEnvironmentVariable("GalleryUrl", EnvironmentVariableTarget.User);
                        else if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GalleryUrl", EnvironmentVariableTarget.Process)))
                           _baseurl = Environment.GetEnvironmentVariable("GalleryUrl", EnvironmentVariableTarget.Process);
                        else
                            _baseurl = Environment.GetEnvironmentVariable("GalleryUrl", EnvironmentVariableTarget.Machine);
                    }
                }

                if (string.IsNullOrEmpty(_baseurl))
                {
                    _baseurl = "https://int.nugettest.org/";
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
