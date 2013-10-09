using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.WebTesting;
using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;    
using System.Text;
using System.Threading.Tasks;
using NuGet;

namespace NuGetGallery.FunctionalTests
{
    public class AssertAndValidationHelper
    {
        #region ValidationRules
        public static ValidationRuleFindText GetValidationRuleForFindText(string findText, bool passIfTextFound = true)
        {
            ValidationRuleFindText text = new ValidationRuleFindText();
            text.FindText = findText;
            text.IgnoreCase = true;
            text.UseRegularExpression = false;
            text.PassIfTextFound = passIfTextFound;
            return text;
        }

        public static ValidateHtmlTagInnerText GetValidationRuleForHtmlTagInnerText(string tagName, string attributeName, string attributeValue, string innerText)
        {
            ValidateHtmlTagInnerText text = new ValidateHtmlTagInnerText();
            text.TagName = tagName;
            text.AttributeName = attributeName;
            text.AttributeValue = attributeValue;
            text.ExpectedInnerText = innerText;
            text.RemoveInnerTags = true;
            text.HasClosingTags = true;
            text.CollapseWhiteSpace = true;
            text.Index = -1;
            text.IgnoreCase = true;
            return text;
        }

        public static ExtractHiddenFields GetDefaultExtractHiddenFields()
        {
            ExtractHiddenFields extractionRule1 = new ExtractHiddenFields();
            extractionRule1.Required = true;
            extractionRule1.HtmlDecode = true;
            extractionRule1.ContextParameterName = "1";
            return extractionRule1;
        }

        #endregion ValidationRules


        #region WebRequestBaseMethods

        /// <summary>
        /// Returns a WebRequest for the given Url. 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static WebTestRequest GetHttpRequestForUrl(string url)
        {
            WebTestRequest getRequest = new WebTestRequest(url);
            ExtractHiddenFields extractionRule1 = AssertAndValidationHelper.GetDefaultExtractHiddenFields();
            getRequest.ExtractValues += new EventHandler<ExtractionEventArgs>(extractionRule1.Extract);
            return getRequest;
        }

        /// <summary>
        /// Returns the GET WebRequest for logon.
        /// </summary>
        /// <returns></returns>
        public static WebTestRequest GetLogonGetRequest()
        {
            return GetHttpRequestForUrl(UrlHelper.LogonPageUrl);
        }

        /// <summary>
        /// Returns the GET WebRequest for Log Off.
        /// </summary>
        /// <returns></returns>
        public static WebTestRequest GetLogOffGetRequest()
        {
            return new WebTestRequest(UrlHelper.LogOffPageUrl);
        }

        /// <summary>
        /// Returns the POST WebRequest for logon with appropriate form parameters set.
        /// Individual WebTests can use this.
        /// </summary>
        /// <returns></returns>
        public static WebTestRequest GetLogonPostRequest(WebTest test, string accountName = null, string password = null)
        {
            if (accountName == null)
            {
                accountName = EnvironmentSettings.TestAccountName;
            }
            if (password == null)
            {
                accountName = EnvironmentSettings.TestAccountPassword;
            }

            WebTestRequest logonPostRequest = new WebTestRequest(UrlHelper.SignInPageUrl);
            logonPostRequest.Method = "POST";
            logonPostRequest.ExpectedResponseUrl = UrlHelper.BaseUrl;
            FormPostHttpBody logonRequestFormPostBody = new FormPostHttpBody();
            logonRequestFormPostBody.FormPostParameters.Add("__RequestVerificationToken", test.Context["$HIDDEN1.__RequestVerificationToken"].ToString());
            logonRequestFormPostBody.FormPostParameters.Add("ReturnUrl", "/");
            logonRequestFormPostBody.FormPostParameters.Add(Constants.UserNameOrEmailFormField, EnvironmentSettings.TestAccountName);
            logonRequestFormPostBody.FormPostParameters.Add(Constants.PasswordFormField, EnvironmentSettings.TestAccountPassword);
            logonPostRequest.Body = logonRequestFormPostBody;
            return logonPostRequest;
        }

        /// <summary>
        /// Returns the POST WebRequest for logon with appropriate form parameters set.
        /// Individual WebTests can use this.
        /// </summary>
        /// <returns></returns>
        public static WebTestRequest GetUploadPostRequestForPackage(WebTest test,string packageFullPath)
        {
            WebTestRequest uploadPostRequest = new WebTestRequest(UrlHelper.UploadPageUrl);
            uploadPostRequest.Method = "POST";
            uploadPostRequest.ExpectedResponseUrl = UrlHelper.VerifyUploadPageUrl;
            FormPostHttpBody uploadPostBody = new FormPostHttpBody();
            uploadPostBody.FormPostParameters.Add("__RequestVerificationToken", test.Context["$HIDDEN1.__RequestVerificationToken"].ToString());
            uploadPostBody.FormPostParameters.Add(new FileUploadParameter("UploadFile", packageFullPath, "application/x-zip-compressed", true));
            uploadPostRequest.Body = uploadPostBody;
            return uploadPostRequest;
        }

        /// <summary>
        /// Returns the POST WebRequest for logon with appropriate form parameters set.
        /// Individual WebTests can use this.
        /// </summary>
        /// <returns></returns>
        public static WebTestRequest GetVerifyPackagePostRequestForPackage(WebTest test, string packageId, string packageVersion)
        {
            WebTestRequest verifyUploadPostRequest = new WebTestRequest(UrlHelper.VerifyUploadPageUrl);
            verifyUploadPostRequest.Method = "POST";
            verifyUploadPostRequest.ExpectedResponseUrl = UrlHelper.GetPackagePageUrl(packageId) + "/" + packageVersion;
            FormPostHttpBody verifyUploadPostRequestBody = new FormPostHttpBody();
            verifyUploadPostRequestBody.FormPostParameters.Add("__RequestVerificationToken", test.Context["$HIDDEN1.__RequestVerificationToken"].ToString());
            verifyUploadPostRequestBody.FormPostParameters.Add("Id", packageId);
            verifyUploadPostRequestBody.FormPostParameters.Add("Version", packageVersion);
            verifyUploadPostRequestBody.FormPostParameters.Add("LicenseUrl", "");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.VersionTitle", "");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.Description", "Package description");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.Summary", "");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.IconUrl", "");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.ProjectUrl", "");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.Authors", "bhuvak");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.CopyrightText", "Copyright 2013");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.Tags", " windows8 ");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.ReleaseNotes", "");
            verifyUploadPostRequest.Body = verifyUploadPostRequestBody;
            return verifyUploadPostRequest;
        }

        #endregion WebRequestBaseMethods

        #region AssertMethods

        /// <summary>
        /// Creates a package with the specified Id and Version and uploads it and checks if the upload has suceeded.
        /// This will be used by test classes which tests scenarios on top of upload.
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="version"></param>
        public static void UploadNewPackageAndVerify(string packageId, string version = "1.0.0")
        {
            if (string.IsNullOrEmpty(packageId))
            {
                packageId = DateTime.Now.Ticks.ToString();
            }
            string packageFullPath = PackageCreationHelper.CreatePackage(packageId, version);
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            int exitCode = CmdLineHelper.UploadPackage(packageFullPath, UrlHelper.V2FeedPushSourceUrl, out standardOutput, out standardError);
            Assert.IsTrue((exitCode == 0), "The package upload via Nuget.exe didnt suceed properly. Check the logs to see the process error and output stream.  Exit Code: " + exitCode + ". Error message: \"" + standardError + "\"");
            Console.WriteLine(standardOutput);
            Console.WriteLine(standardError);
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
        public static void DownloadPackageAndVerify(string packageId,string version="1.0.0")
        {
            ClientSDKHelper.ClearMachineCache();
            ClientSDKHelper.ClearLocalPackageFolder(packageId);
            new PackageManager(PackageRepositoryFactory.Default.CreateRepository(UrlHelper.V2FeedRootUrl), Environment.CurrentDirectory).InstallPackage(packageId,new SemanticVersion(version));
            Assert.IsTrue(ClientSDKHelper.CheckIfPackageVersionInstalled(packageId,version), "Package install failed. Either the file is not present on disk or it is corrupted. Check logs for details");
        }
        
        #endregion AssertMethods



    }
}
