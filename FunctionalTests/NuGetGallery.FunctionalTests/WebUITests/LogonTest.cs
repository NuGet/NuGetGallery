
namespace NuGetGallery.FunctionalTests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.WebTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
    using System.Web.UI;
    using NuGetGallery.FunctionTests.Helpers;

    /// <summary>
    /// Tries to login with a POST request with the credentials retrieved from the data source. Validates that the response has the logged in user name.
    /// </summary>
   // [DeploymentItem("nugetgallerybvts\\DataSource\\LogonCredentials.csv")]
    [DataSource("LogOnCredentials", "Microsoft.VisualStudio.TestTools.DataSource.CSV", "|DataDirectory|\\LogonCredentials.csv", Microsoft.VisualStudio.TestTools.WebTesting.DataBindingAccessMethod.Sequential, Microsoft.VisualStudio.TestTools.WebTesting.DataBindingSelectColumns.SelectOnlyBoundColumns, "LogonCredentials#csv")]
    [DataBinding("LogOnCredentials", "LogonCredentials#csv", "ï»¿UserNameOrEmail", "LogOnCredentials.LogonCredentials#csv.ï»¿UserNameOrEmail")]
    [DataBinding("LogOnCredentials", "LogonCredentials#csv", "Password", "LogOnCredentials.LogonCredentials#csv.Password")]
    public class LogonTest : WebTest
    {

        public LogonTest()
        {
            this.PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
          
            WebTestRequest logonRequest = new WebTestRequest(UrlHelper.LogonPageUrl);
            logonRequest.ExpectedResponseUrl = UrlHelper.LogonPageUrl;
            ExtractHiddenFields extractionRule1 = ValidationRuleHelper.GetDefaultExtractHiddenFields();
            logonRequest.ExtractValues += new EventHandler<ExtractionEventArgs>(extractionRule1.Extract);
            yield return logonRequest;
            logonRequest = null;

            //Send a post request with the appropriate form parameters for crednetials.
            WebTestRequest logonPostRequest = new WebTestRequest(UrlHelper.LogonPageUrl);
            logonPostRequest.Method = "POST";
            logonPostRequest.ExpectedResponseUrl = UrlHelper.BaseUrl;
            FormPostHttpBody logonRequestFormPostBody = new FormPostHttpBody();
            logonRequestFormPostBody.FormPostParameters.Add("__RequestVerificationToken", this.Context["$HIDDEN1.__RequestVerificationToken"].ToString());
            logonRequestFormPostBody.FormPostParameters.Add(Constants.UserNameOrEmailFormField, this.Context["LogOnCredentials.LogonCredentials#csv.ï»¿UserNameOrEmail"].ToString());
            logonRequestFormPostBody.FormPostParameters.Add(Constants.PasswordFormField, this.Context["LogOnCredentials.LogonCredentials#csv.Password"].ToString());
            logonPostRequest.Body = logonRequestFormPostBody;
            WebTestRequest dependentRequest = new WebTestRequest(UrlHelper.StatsPageUrl);
            logonPostRequest.DependentRequests.Add(dependentRequest);
            if ((this.Context.ValidationLevel >= Microsoft.VisualStudio.TestTools.WebTesting.ValidationLevel.High))
            {
                //After logon, the user name should appear as a hyperlink the reponse URL
                ValidateHtmlTagInnerText loggedOnUserNameValidationRule = ValidationRuleHelper.GetValidationRuleForHtmlTagInnerText(HtmlTextWriterTag.A.ToString(), HtmlTextWriterAttribute.Href.ToString(), "/account", "NugetTestAccount");             
                logonPostRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(loggedOnUserNameValidationRule.Validate);
            }
            yield return logonPostRequest;
            logonPostRequest = null;

          
        }
    }
}
