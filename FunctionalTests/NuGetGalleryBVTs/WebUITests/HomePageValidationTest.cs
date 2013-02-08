namespace NuGetGalleryBVTs
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.WebTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
    using NugetClientSDKHelpers;

    /// <summary>
    /// Sends http request to gallery home page checks for the default home page text in the reponse.
    /// </summary>
    public class HomePageValidationTest : WebTest
    {
        public HomePageValidationTest()
        {
            this.PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //send a request to home page and check for default home page text.
            WebTestRequest homePageRequest = new WebTestRequest(Utilities.BaseUrl);           
            WebTestRequest request1Dependent1 = new WebTestRequest(Utilities.BaseUrl + Constants.StatsPageUrlSuffix);
            homePageRequest.DependentRequests.Add(request1Dependent1);
            if ((this.Context.ValidationLevel >= Microsoft.VisualStudio.TestTools.WebTesting.ValidationLevel.High))
            {
                ValidationRuleFindText homePageTextValidationRule = ValidationRuleHelper.GetValidationRuleForFindText(Constants.HomePageText);               
                homePageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(homePageTextValidationRule.Validate);
            }
            yield return homePageRequest;
            homePageRequest = null;          
        }
    }
}
