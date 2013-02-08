using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.WebTesting;
using Microsoft.VisualStudio.TestTools.WebTesting.Rules;

namespace NuGetGalleryBVTs
{
    public class ValidationRuleHelper
    {
        public static ValidationRuleFindText GetValidationRuleForFindText(string findText)
        {
            ValidationRuleFindText text = new ValidationRuleFindText();
            text.FindText = findText;
            text.IgnoreCase = true;
            text.UseRegularExpression = false;
            text.PassIfTextFound = true;
            return text;
        }

        public static ValidateHtmlTagInnerText GetValidationRuleForHtmlTagInnerText(string tagName, string attributeName, string attributeValue, string innerText)
        {
            ValidateHtmlTagInnerText text = new ValidateHtmlTagInnerText();
            text.TagName = tagName;
            text.AttributeName = attributeName;
            text.AttributeValue = attributeValue;
            text.ExpectedInnerText = innerText;
            text.RemoveInnerTags = true;
            text.HasClosingTags = true;
            text.CollapseWhiteSpace = true;
            text.Index = -1;
            text.IgnoreCase = true;
            return text;
        }

        public static ExtractHiddenFields GetDefaultExtractHiddenFields()
        {
            ExtractHiddenFields extractionRule1 = new ExtractHiddenFields();
            extractionRule1.Required = true;
            extractionRule1.HtmlDecode = true;
            extractionRule1.ContextParameterName = "1";
            return extractionRule1;
        }
    

 


    }
}
