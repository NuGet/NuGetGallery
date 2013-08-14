
namespace NuGetGallery.FunctionalTests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.WebTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
    using System.Web.UI;
    using NuGetGallery.FunctionTests.Helpers;
    using NuGetGallery.FunctionalTests.TestBase;

    /// <summary>
    /// Tries to login with a POST request with the credentials retrieved from the data source. Validates that the response has the logged in user name.
    /// </summary>   
    public class InvalidLogonTest : WebTest
    {
        public InvalidLogonTest()
        {
            this.PreAuthenticate = true;
        }
        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            WebTestRequest registerPageRequest = AssertAndValidationHelper.GetHttpRequestForUrl(UrlHelper.RegisterPageUrl);
            yield return registerPageRequest;
            registerPageRequest = null;

            WebTestRequest registerPagePostRequest = new WebTestRequest(UrlHelper.RegisterPageUrl);
            registerPagePostRequest.Method = "POST";
            registerPagePostRequest.ExpectedResponseUrl = UrlHelper.RegistrationPendingPageUrl;
            //create a form and set the UserName, Email and password as form post parameters.
            //We just need to set some unique user name and Email.
            FormPostHttpBody registerNewUserFormPost = new FormPostHttpBody();
            registerNewUserFormPost.FormPostParameters.Add("__RequestVerificationToken", this.Context["$HIDDEN1.__RequestVerificationToken"].ToString());
            registerNewUserFormPost.FormPostParameters.Add(Constants.EmailAddressFormField, DateTime.Now.Ticks.ToString() + "@live.com"); //add a dummy mail account. This will be fixed once we incorporate the logic to delete user.
            registerNewUserFormPost.FormPostParameters.Add(Constants.UserNameFormField, DateTime.Now.Ticks.ToString() + "NewAccount");
            registerNewUserFormPost.FormPostParameters.Add(Constants.PasswordFormField, "xxxxxxx");
            registerNewUserFormPost.FormPostParameters.Add(Constants.ConfirmPasswordFormField, "xxxxxxx");
            registerNewUserFormPost.FormPostParameters.Add(Constants.AcceptTermsField, "true");
            registerPagePostRequest.Body = registerNewUserFormPost;
            //Validate the response to make sure that it has the pending confirmation text in it.           
            ValidationRuleFindText PendingConfirmationTextRule = AssertAndValidationHelper.GetValidationRuleForFindText(Constants.RegisterNewUserPendingConfirmationText);
            registerPagePostRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(PendingConfirmationTextRule.Validate);
            yield return registerPagePostRequest;
            registerPagePostRequest = null;
        }
    }
}
