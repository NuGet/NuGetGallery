
namespace NuGetGalleryBVTs
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.WebTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
    using NugetClientSDKHelpers;
    using System.Web.UI;

    /// <summary>
    /// Sends http request to individual package pages and checks the response for appropriate title and download count.
    /// </summary>
    [DeploymentItem("nugetgallerybvts\\DataSource\\Packages.csv")]
    [DataSource("Packages", "Microsoft.VisualStudio.TestTools.DataSource.CSV", "|DataDirectory|\\Packages.csv", Microsoft.VisualStudio.TestTools.WebTesting.DataBindingAccessMethod.Sequential, Microsoft.VisualStudio.TestTools.WebTesting.DataBindingSelectColumns.SelectOnlyBoundColumns, "Packages#csv")]
    [DataBinding("Packages", "Packages#csv", "ï»¿PackageId", "Packages.Packages#csv.ï»¿PackageId")]
    public class PackagesPageTest : WebTest
    {

        public PackagesPageTest()
        {
            this.PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            // take package names from the data source 
            string packageId = this.Context["Packages.Packages#csv.ï»¿PackageId"].ToString();
            WebTestRequest packagePageRequest = new WebTestRequest( Utilities.BaseUrl + @"Packages/" + packageId + "/");
       
            if ((this.Context.ValidationLevel >= Microsoft.VisualStudio.TestTools.WebTesting.ValidationLevel.High))
            {
                //Rule to check if the title contains the package id and the latest stable version of the package.
                ValidateHtmlTagInnerText packageTitleValidationRule = ValidationRuleHelper.GetValidationRuleForHtmlTagInnerText(HtmlTextWriterTag.Title.ToString(), string.Empty, string.Empty, "NuGet Gallery | "+packageId+" "+ ClientSDKHelper.GetLatestStableVersion(packageId));              
                packagePageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(packageTitleValidationRule.Validate);
            }
            if ((this.Context.ValidationLevel >= Microsoft.VisualStudio.TestTools.WebTesting.ValidationLevel.High))
            {
                //rule to check that the download count is present in the response.
                ValidationRuleFindText downloadCountValidationRule = ValidationRuleHelper.GetValidationRuleForFindText(ClientSDKHelper.GetFormattedDownLoadStatistics(packageId));                
                packagePageRequest.ValidateResponse += new EventHandler<ValidationEventArgs>(downloadCountValidationRule.Validate);
            }
            yield return packagePageRequest;
            packagePageRequest = null;

            
        }
    }
}
