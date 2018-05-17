// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web.UI;
using Microsoft.VisualStudio.TestTools.WebTesting;
using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.AccountManagement
{
    /// <summary>
    /// Tries to login with a POST request with the credentials retrieved from the data source. Validates that the response has the logged in user name.
    /// priority : p0
    /// </summary>
    public class MSALogonTest
        : WebTest
    {
        public MSALogonTest()
        {
            PreAuthenticate = true;
        }

        private void PostRequestHandler(object sender, PostRequestEventArgs args)
        {
            if (args != null && args.Response != null)
            {
                string locationHeaderText = args.Response.Headers["Location"];
                args.WebTest.AddCommentToResult("Looking at header: " + locationHeaderText);
                if (!string.IsNullOrEmpty(locationHeaderText))
                {
                    args.WebTest.Context.Add("RedirectUrl", locationHeaderText);
                }
            }
            else
            {
                args.WebTest.AddCommentToResult("Response is null, headers missing?");
            }
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            WebTestRequest logonGet = new WebTestRequest(UrlHelper.MSASignInPageUrl);            // AssertAndValidationHelper.GetMSALogonGetRequest();
            logonGet.FollowRedirects = false;
            logonGet.PostRequest += new System.EventHandler<PostRequestEventArgs>(PostRequestHandler);
            yield return logonGet;
            logonGet = null;

            WebTestRequest logonGetWithUsername = new WebTestRequest(this.Context["RedirectUrl"].ToString());
            logonGetWithUsername.FollowRedirects = false;
            logonGetWithUsername.Method = "GET";
            logonGetWithUsername.QueryStringParameters.Add("username", EnvironmentSettings.MSATestAccountEmail);
            logonGetWithUsername.PostRequest += new System.EventHandler<PostRequestEventArgs>(PostRequestHandler);
            yield return logonGetWithUsername;
            logonGetWithUsername = null;

            var authenticateUrl = new Uri(this.Context["RedirectUrl"].ToString());
            //ppsecure/post.srf
            var builder = new UriBuilder();
            builder.Scheme = authenticateUrl.Scheme;
            builder.Host = authenticateUrl.Host;
            builder.Query = authenticateUrl.Query;
            builder.Path = "/ppsecure/post.srf";
            var newUrl = builder.Uri;

            //WebTestRequest logonGetPasswordPage = new WebTestRequest(newUrl.ToString());
            //logonGetPasswordPage.FollowRedirects = true;
            //logonGetPasswordPage.Method = "GET";
            //ExtractAttributeValue actionExtractionRule = new ExtractAttributeValue();
            //actionExtractionRule.TagName = "f1";
            //actionExtractionRule.AttributeName = "action";
            //actionExtractionRule.ContextParameterName = "PostActionUrl";
            ////actionExtractionRule.HtmlDecode = true;
            //actionExtractionRule.Index = 0;
            //logonGetPasswordPage.ExtractValues += new System.EventHandler<ExtractionEventArgs>(actionExtractionRule.Extract);
            //yield return logonGetPasswordPage;
            //logonGetPasswordPage = null;

            WebTestRequest logonRequest = new WebTestRequest(newUrl);
            logonRequest.FollowRedirects = true;
            logonRequest.Method = "POST";
            var formPostData = new FormPostHttpBody();
            formPostData.FormPostParameters.Add("login", EnvironmentSettings.MSATestAccountEmail);
            formPostData.FormPostParameters.Add("loginfmt", EnvironmentSettings.MSATestAccountEmail);
            formPostData.FormPostParameters.Add("passwd", EnvironmentSettings.MSATestAccountPassword);
            logonRequest.Body = formPostData;
            yield return logonRequest;
            logonRequest = null;

            //var logonRequestFormPostBody = new FormPostHttpBody();
            //logonRequestFormPostBody.FormPostParameters.Add("loginfmt", EnvironmentSettings.MSATestAccountEmail);
            //logonRequestFormPostBody.FormPostParameters.Add("passwd", EnvironmentSettings.MSATestAccountPassword);
            //logonPostRequest.Body = logonRequestFormPostBody;

            //var loggedOnUserNameValidationRule = AssertAndValidationHelper.GetValidationRuleForHtmlTagInnerText(
            //    HtmlTextWriterTag.Span.ToString(),
            //    HtmlTextWriterAttribute.Class.ToString(),
            //    "dropdown-username",
            //    "NugetMSATestAccount");

            //setMSAUsernameRequest.ValidateResponse += loggedOnUserNameValidationRule.Validate;
            //yield return setMSAUsernameRequest;
        }
    }
}

