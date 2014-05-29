using Microsoft.VisualStudio.TestTools.WebTesting;
using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Collections.Generic;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// Sends http POST request to register a new user in read-only mode and checks if read-only mode error is shown properly.
    /// </summary>
    public class RegisterNewUserInReadOnlyModeTest : WebTest
    {
        public RegisterNewUserInReadOnlyModeTest()
        {
            this.PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //run this test only if read-only mode is set. This is to avoid false failures while doing Run all tests locally.
            if (EnvironmentSettings.ReadOnlyMode.Equals("True", StringComparison.OrdinalIgnoreCase))
            {
                WebTestRequest registerPageRequest = AssertAndValidationHelper.GetHttpRequestForUrl(UrlHelper.LogonPageUrl);
                yield return registerPageRequest;
                registerPageRequest = null;

                WebTestRequest registerPagePostRequest = new WebTestRequest(UrlHelper.RegisterPageUrl);
                registerPagePostRequest.Method = "POST";
                registerPagePostRequest.ExpectedResponseUrl = UrlHelper.RegistrationPendingPageUrl;
                //create a form and set the UserName, Email and password as form post parameters.
                //We just need to set some unique user name and Email.
                FormPostHttpBody registerNewUserFormPost = new FormPostHttpBody();
                registerNewUserFormPost.FormPostParameters.Add("__RequestVerificationToken", this.Context["$HIDDEN1.__RequestVerificationToken"].ToString());
                registerNewUserFormPost.FormPostParameters.Add("LinkingAccount", "false");
                registerNewUserFormPost.FormPostParameters.Add(Constants.EmailAddressFormField, DateTime.Now.Ticks.ToString() + "@live.com"); //add a dummy mail account. This will be fixed once we incorporate the logic to delete user.
                registerNewUserFormPost.FormPostParameters.Add(Constants.UserNameFormField, DateTime.Now.Ticks.ToString() + "NewAccount");
                registerNewUserFormPost.FormPostParameters.Add(Constants.RegisterPasswordFormField, "xxxxxxxx");
                registerPagePostRequest.Body = registerNewUserFormPost;
                registerPagePostRequest.ExpectedHttpStatusCode = 503;
                //Validate the response to make sure that it shows the error message for read-only mode.     
                ValidationRuleFindText ReadOnlyModeTextRule = AssertAndValidationHelper.GetValidationRuleForFindText(Constants.ReadOnlyModeError);
                registerPagePostRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(ReadOnlyModeTextRule.Validate);
                yield return registerPagePostRequest;
                registerPagePostRequest = null;
            }
        }
    }
}


