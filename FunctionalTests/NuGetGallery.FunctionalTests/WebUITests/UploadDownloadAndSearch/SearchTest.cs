namespace NuGetGallery.FunctionalTests
{
    using Microsoft.VisualStudio.TestTools.WebTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using NuGetGallery.FunctionalTests.TestBase;
    using NuGetGallery.FunctionTests.Helpers;
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class SearchTest : WebTest
    {
        /// <summary>
        /// Verifies that the aggregate stats in home page gets incremented appropriately after downloading a package.
        /// </summary>
        public SearchTest()
        {
            this.PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            WebTestRequest SearchRequest = new WebTestRequest(UrlHelper.PackagesPageUrl);
           // SearchRequest.ResponseTimeGoal = 1F;
            SearchRequest.QueryStringParameters.Add("q", "jQuery", false, false);
            if ((this.Context.ValidationLevel >= Microsoft.VisualStudio.TestTools.WebTesting.ValidationLevel.High))
            {
                ValidationRuleFindText findSearchTextRule = new ValidationRuleFindText();
                findSearchTextRule.FindText = "jQuery UI (Combined Library)";
                findSearchTextRule.IgnoreCase = false;
                findSearchTextRule.UseRegularExpression = false;
                findSearchTextRule.PassIfTextFound = true;
                SearchRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(findSearchTextRule.Validate);
            }
            //if ((this.Context.ValidationLevel >= Microsoft.VisualStudio.TestTools.WebTesting.ValidationLevel.High))
            //{
            //    ValidationRuleResponseTimeGoal toleranceRule = new ValidationRuleResponseTimeGoal();
            //    toleranceRule.Tolerance = 20D;
            //    SearchRequest.ValidateResponseOnPageComplete += new EventHandler<ValidationEventArgs>(toleranceRule.Validate);
            //}
            yield return SearchRequest;
            SearchRequest = null;

        }
    }
}

