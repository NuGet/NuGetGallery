﻿
namespace NuGetGallery.FunctionalTests
{
    using Microsoft.VisualStudio.TestTools.WebTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
    using NuGetGallery.FunctionalTests.TestBase;
    using NuGetGallery.FunctionTests.Helpers;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Web.UI;

    /// <summary>
    /// Sends a http request to the statistics page and tries to validate the default stats page text and the prescene of top package.
    /// </summary>
    public class StatisticsPageTest : GalleryTestBase
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
            ValidateHtmlTagInnerText jQueryPackageValidationRule = ValidationRuleHelper.GetValidationRuleForHtmlTagInnerText(HtmlTextWriterTag.A.ToString(), HtmlTextWriterAttribute.Href.ToString(), "/packages/jQuery/", "jQuery");               
            statsPageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(jQueryPackageValidationRule.Validate);
            //validation rule to check for the default text in stats page.
            ValidationRuleFindText StatsPageDefaultTextValidationRule = ValidationRuleHelper.GetValidationRuleForFindText(Constants.StatsPageDefaultText);
            statsPageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(StatsPageDefaultTextValidationRule.Validate);
          
            yield return statsPageRequest;
            statsPageRequest = null;
        }
    }
}
