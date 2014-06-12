using Microsoft.VisualStudio.TestTools.WebTesting;
using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionTests.Helpers;
using System;
using System.Collections.Generic;
using System.Web.UI;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// Sends a http request to the statistics page and tries to validate the default stats page text and the prescene of top package.
    /// Priority : p1
    /// </summary>
    public class StatisticsPageTest : WebTest
    {
        public StatisticsPageTest()
        {
            this.PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            WebTestRequest statsPageRequest = new WebTestRequest(UrlHelper.StatsPageUrl);
          
            //Checks for the prescene of a link to jqeury package. It is harded to Jquery for now as there is no API exposed for stats
            //and also Jquery is going to be one of the top 10 for now.
            ValidateHtmlTagInnerText jQueryPackageValidationRule = AssertAndValidationHelper.GetValidationRuleForHtmlTagInnerText(HtmlTextWriterTag.A.ToString(), HtmlTextWriterAttribute.Href.ToString(), "/packages/EntityFramework/", "EntityFramework");               
            statsPageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(jQueryPackageValidationRule.Validate);
            //validation rule to check for the default text in stats page.
            ValidationRuleFindText StatsPageDefaultTextValidationRule = AssertAndValidationHelper.GetValidationRuleForFindText(Constants.StatsPageDefaultText);
            statsPageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(StatsPageDefaultTextValidationRule.Validate);
          
            yield return statsPageRequest;
            statsPageRequest = null;
        }
    }
}
