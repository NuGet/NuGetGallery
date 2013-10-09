
namespace NuGetGallery.FunctionalTests
{
    using Microsoft.VisualStudio.TestTools.WebTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
    using NuGetGallery.FunctionTests.Helpers;
    using NuGetGallery.FunctionalTests.TestBase;
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Attempts to register an invalid user and confirms that the process failed.
    /// </summary>
    public class RegisterInvalidUserTest : WebTest
    {
        public RegisterInvalidUserTest()
        {
            this.PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            WebTestRequest registerPageRequest = AssertAndValidationHelper.GetHttpRequestForUrl(UrlHelper.LogonPageUrl);
            yield return registerPageRequest;
            registerPageRequest = null;

            WebTestRequest registerPagePostRequest = new WebTestRequest(UrlHelper.RegisterPageUrl);
            //create a form and set the UserName, Email and password as form post parameters.  
            //We just need to set some unique user name and Email.  
            registerPagePostRequest.Method = "POST";
            registerPagePostRequest.ExpectedResponseUrl = UrlHelper.RegistrationPendingPageUrl;
            FormPostHttpBody registerNewUserFormPost = new FormPostHttpBody();
            registerNewUserFormPost.FormPostParameters.Add("__RequestVerificationToken", this.Context["$HIDDEN1.__RequestVerificationToken"].ToString());
            registerNewUserFormPost.FormPostParameters.Add(Constants.EmailAddressFormField, DateTime.Now.Ticks.ToString()+ "@live.com" ); 
            registerNewUserFormPost.FormPostParameters.Add(Constants.UserNameFormField, EnvironmentSettings.TestAccountName);  // This account already exists; we expect this to fail.
            registerNewUserFormPost.FormPostParameters.Add(Constants.PasswordFormField, "xxxxxxx");
            registerPagePostRequest.Body = registerNewUserFormPost;
            //Validate the response to make sure that it has the pending confirmation text in it.           
            ValidationRuleFindText PendingConfirmationTextRule = AssertAndValidationHelper.GetValidationRuleForFindText(Constants.RegisterNewUserPendingConfirmationText, false);
            registerPagePostRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(PendingConfirmationTextRule.Validate);           
            yield return registerPagePostRequest;
            registerPagePostRequest = null;

            registerPagePostRequest = new WebTestRequest(UrlHelper.RegisterPageUrl);
            registerPagePostRequest.Method = "POST";
            registerPagePostRequest.ExpectedResponseUrl = UrlHelper.RegistrationPendingPageUrl;
            registerNewUserFormPost = new FormPostHttpBody();
            registerNewUserFormPost.FormPostParameters.Add("__RequestVerificationToken", this.Context["$HIDDEN1.__RequestVerificationToken"].ToString());
            registerNewUserFormPost.FormPostParameters.Add(Constants.EmailAddressFormField, DateTime.Now.Ticks.ToString() + "@live.com");
            registerNewUserFormPost.FormPostParameters.Add(Constants.UserNameFormField, Convert.ToChar(4).ToString());  // This is an invalid username; we expect this to fail, too.
            registerNewUserFormPost.FormPostParameters.Add(Constants.PasswordFormField, "xxxxxxx");
            registerPagePostRequest.Body = registerNewUserFormPost;
            //Validate the response to make sure that it lacks the pending confirmation text.           
            PendingConfirmationTextRule = AssertAndValidationHelper.GetValidationRuleForFindText(Constants.RegisterNewUserPendingConfirmationText, false);
            registerPagePostRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(PendingConfirmationTextRule.Validate);
            //Validate the error is handled.  We should end up on the same page again.     
            PendingConfirmationTextRule = AssertAndValidationHelper.GetValidationRuleForFindText(Constants.RegisterNewUserPendingConfirmationText, false);
            registerPagePostRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(PendingConfirmationTextRule.Validate);
            yield return registerPagePostRequest;
            registerPagePostRequest = null;
        }
    }
}
