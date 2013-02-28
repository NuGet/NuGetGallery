using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.WebTesting;
using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
using NuGet;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.UI;

namespace NuGetGallery.FunctionalTests.TestBase
{
    /// <summary>
    /// Base class for all the test classes. Has the common functions which individual test classes would use.
    /// </summary>
    [TestClass]   
    public class GalleryTestBase : WebTest
    {
        public GalleryTestBase()
        {
            this.PreAuthenticate = true;
            //take the user name and password from the environment variable.
            this.UserName = EnvironmentSettings.TestAccountName;
            this.Password = EnvironmentSettings.TestAccountPassword;
        }

        #region InitializeMethods

        [AssemblyInitialize()]
        public static void ClassInit(TestContext context)
        {
            //update Nuget.exe in class init so that the latest one is being used always.            
            CmdLineHelper.UpdateNugetExe();           
        }    

        [TestInitialize()]
        public void TestInit()
        {           
            //Clear the machine cache during the start of every test to make sure that we always hit the gallery         .
            ClientSDKHelper.ClearMachineCache();
        }


        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            return null;
        }
        #endregion InitializeMethods

        #region BaseMethods

        /// <summary>
        /// Creates a package with the specified Id and Version and uploads it and checks if the upload has suceeded.
        /// This will be used by test classes which tests scenarios on top of upload.
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="version"></param>
        public void UploadNewPackageAndVerify(string packageId,string version="1.0.0")
        {            
            if (string.IsNullOrEmpty(packageId))
            {
                packageId = DateTime.Now.Ticks.ToString();
            }
            string packageFullPath = CmdLineHelper.CreatePackage(packageId,version);
            int exitCode = CmdLineHelper.UploadPackage(packageFullPath, UrlHelper.V2FeedPushSourceUrl);
            Assert.IsTrue((exitCode == 0), "The package upload via Nuget.exe didnt suceed properly. Check the logs to see the process error and output stream");
            Assert.IsTrue(ClientSDKHelper.CheckIfPackageVersionExistsInSource(packageId, version, UrlHelper.V2FeedRootUrl), "Package {0} is not found in the site {1} after uploading.", packageId, UrlHelper.V2FeedRootUrl);
            
            //Delete package from local disk so once it gets uploaded
            if (File.Exists(packageFullPath))
            {
                File.Delete(packageFullPath);
                Directory.Delete(Path.GetFullPath(Path.GetDirectoryName(packageFullPath)), true);
            }
        }

        /// <summary>
        /// Downloads a package to local folder and see if the download is successful. Used to individual tests which extend the download scenarios.
        /// </summary>
        /// <param name="packageId"></param>
        public void DownloadPackageAndVerify(string packageId)
        {
            ClientSDKHelper.ClearMachineCache();
            ClientSDKHelper.ClearLocalPackageFolder(packageId);
            new PackageManager(PackageRepositoryFactory.Default.CreateRepository(UrlHelper.V2FeedRootUrl), Environment.CurrentDirectory).InstallPackage(packageId);
            Assert.IsTrue(ClientSDKHelper.CheckIfPackageInstalled(packageId), "Package install failed. Either the file is not present on disk or it is corrupted. Check logs for details");
        }

        #endregion BaseMethods

        #region WebRequestBaseMethods

        /// <summary>
        /// Returns a WebRequest for the given Url. 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public WebTestRequest GetHttpRequestForUrl(string url)
        {
            WebTestRequest getRequest = new WebTestRequest(url);          
            ExtractHiddenFields extractionRule1 = ValidationRuleHelper.GetDefaultExtractHiddenFields();
            getRequest.ExtractValues += new EventHandler<ExtractionEventArgs>(extractionRule1.Extract);
            return getRequest;
        }

        /// <summary>
        /// Returns the GET WebRequest for logon.
        /// </summary>
        /// <returns></returns>
        public WebTestRequest GetLogonGetRequest()
        {
            return GetHttpRequestForUrl(UrlHelper.LogonPageUrl);   
        }

        /// <summary>
        /// Returns the POST WebRequest for logon with appropriate form parameters set.
        /// Individual WebTests can use this.
        /// </summary>
        /// <returns></returns>
        public WebTestRequest GetLogonPostRequest()
        {         
            WebTestRequest logonPostRequest = new WebTestRequest(UrlHelper.LogonPageUrl);
            logonPostRequest.Method = "POST";
            logonPostRequest.ExpectedResponseUrl = UrlHelper.BaseUrl;
            FormPostHttpBody logonRequestFormPostBody = new FormPostHttpBody();
            logonRequestFormPostBody.FormPostParameters.Add("__RequestVerificationToken", this.Context["$HIDDEN1.__RequestVerificationToken"].ToString());
            logonRequestFormPostBody.FormPostParameters.Add(Constants.UserNameOrEmailFormField, this.UserName);
            logonRequestFormPostBody.FormPostParameters.Add(Constants.PasswordFormField, this.Password);
            logonPostRequest.Body = logonRequestFormPostBody;          
            return logonPostRequest;           
        }

        #endregion WebRequestBaseMethods


    }
}
