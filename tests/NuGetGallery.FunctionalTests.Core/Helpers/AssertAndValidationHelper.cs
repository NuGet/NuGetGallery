// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.VisualStudio.TestTools.WebTesting;
using Microsoft.VisualStudio.TestTools.WebTesting.Rules;

namespace NuGetGallery.FunctionalTests.Helpers
{
    public static class AssertAndValidationHelper
    {
        public static ValidationRuleFindText GetValidationRuleForFindText(string findText, bool passIfTextFound = true)
        {
            var text = new ValidationRuleFindText();
            text.FindText = findText;
            text.IgnoreCase = true;
            text.UseRegularExpression = false;
            text.PassIfTextFound = passIfTextFound;
            return text;
        }

        public static ValidateHtmlTagInnerText GetValidationRuleForHtmlTagInnerText(string tagName, string attributeName, string attributeValue, string innerText)
        {
            var text = new ValidateHtmlTagInnerText();
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
            var extractionRule1 = new ExtractHiddenFields();
            extractionRule1.Required = true;
            extractionRule1.HtmlDecode = true;
            extractionRule1.ContextParameterName = "1";
            return extractionRule1;
        }

        /// <summary>
        /// Returns a WebRequest for the given Url.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static WebTestRequest GetHttpRequestForUrl(string url)
        {
            var getRequest = new WebTestRequest(url);
            var extractionRule = GetDefaultExtractHiddenFields();
            getRequest.ExtractValues += extractionRule.Extract;
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
        public static WebTestRequest GetLogonPostRequest(WebTest test)
        {
            var logonPostRequest = new WebTestRequest(UrlHelper.SignInPageUrl);
            logonPostRequest.Method = "POST";

            var logonRequestFormPostBody = new FormPostHttpBody();
            logonRequestFormPostBody.FormPostParameters.Add("__RequestVerificationToken", test.Context["$HIDDEN1.__RequestVerificationToken"].ToString());
            logonRequestFormPostBody.FormPostParameters.Add("ReturnUrl", "/");
            logonRequestFormPostBody.FormPostParameters.Add("LinkingAccount", "false");
            logonRequestFormPostBody.FormPostParameters.Add(Constants.UserNameOrEmailFormField, GalleryConfiguration.Instance.Account.Email);
            logonRequestFormPostBody.FormPostParameters.Add(Constants.PasswordFormField, GalleryConfiguration.Instance.Account.Password);
            logonPostRequest.Body = logonRequestFormPostBody;

            return logonPostRequest;
        }

        /// <summary>
        /// Returns the POST WebRequest for logon with appropriate form parameters set.
        /// Individual WebTests can use this.
        /// </summary>
        /// <returns></returns>
        public static WebTestRequest GetCancelUploadPostRequestForPackage(WebTest test)
        {
            var uploadPostRequest = new WebTestRequest(UrlHelper.CancelUpload);
            uploadPostRequest.Method = "POST";

            var uploadPostBody = new FormPostHttpBody();
            uploadPostBody.FormPostParameters.Add("__RequestVerificationToken", test.Context["$HIDDEN1.__RequestVerificationToken"].ToString());
            uploadPostRequest.Body = uploadPostBody;

            return uploadPostRequest;
        }

        /// <summary>
        /// Returns the POST WebRequest for logon with appropriate form parameters set.
        /// Individual WebTests can use this.
        /// </summary>
        /// <returns></returns>
        public static WebTestRequest GetUploadPostRequestForPackage(WebTest test, string packageFullPath)
        {
            var uploadPostRequest = new WebTestRequest(UrlHelper.UploadPageUrl);
            uploadPostRequest.Method = "POST";

            var uploadPostBody = new FormPostHttpBody();
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
        public static WebTestRequest GetVerifyPackagePostRequestForPackage(WebTest test, string packageId, string packageVersion, string expectedResponseUrl, string expectedText, string owner, int expectedResponseCode = 200)
        {
            var verifyUploadPostRequest = new WebTestRequest(UrlHelper.VerifyUploadPageUrl);
            verifyUploadPostRequest.Method = "POST";
            verifyUploadPostRequest.ExpectedHttpStatusCode = expectedResponseCode;
            verifyUploadPostRequest.ExpectedResponseUrl = expectedResponseUrl;

            var verifyUploadPostRequestBody = new FormPostHttpBody();
            verifyUploadPostRequestBody.FormPostParameters.Add("__RequestVerificationToken", test.Context["$HIDDEN1.__RequestVerificationToken"].ToString());
            verifyUploadPostRequestBody.FormPostParameters.Add("Id", packageId);
            verifyUploadPostRequestBody.FormPostParameters.Add("Version", packageVersion);
            verifyUploadPostRequestBody.FormPostParameters.Add("LicenseUrl", "");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.VersionTitle", "");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.Description", "Package description");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.Summary", "");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.IconUrl", "");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.ProjectUrl", "");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.Authors", "nugettest");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.CopyrightText", "Copyright 2013");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.Tags", " windows8 ");
            verifyUploadPostRequestBody.FormPostParameters.Add("Edit.ReleaseNotes", "");
            verifyUploadPostRequestBody.FormPostParameters.Add("Owner", owner);
            verifyUploadPostRequest.Body = verifyUploadPostRequestBody;

            var postUploadText = GetValidationRuleForFindText(expectedText);
            verifyUploadPostRequest.ValidateResponse += postUploadText.Validate;
            return verifyUploadPostRequest;
        }

        public static WebTestRequest GetCancelGetRequest()
        {
            return GetHttpRequestForUrl(UrlHelper.CancelUrl);
        }
    }
}

