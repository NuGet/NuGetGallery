using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery.Helpers
{
    public class AccordianHelper
    {
        public string Name { get; private set; }
        public string FormModelStatePrefix { get; private set; }
        public WebViewPage Page { get; private set; }
        public string ItemId { get { return Name + "-item"; } }
        public string ContentDropDownId { get { return Name + "-content"; } }
        public string ContentHiddenClass { get { return Expanded ? null : "s-hidden"; } }

        public bool Expanded
        {
            get
            {
                return FormModelStatePrefix != null &&
                    !Page.ViewData.ModelState.IsValidField(FormModelStatePrefix);
            }
        }

        public AccordianHelper(string name, string formModelStatePrefix, WebViewPage page)
        {
            Name = name;
            FormModelStatePrefix = formModelStatePrefix;
            Page = page;
        }

        public HtmlString ExpandButton(string closedTitle, string expandedTitle)
        {
            return new HtmlString(
                "<a href=\"#\" class=\"accordian-expand-button btn btn-inline s-expand\" data-target=\"#" +
                ContentDropDownId +
                "\" data-toggletext=\"" +
                (Expanded ? closedTitle : expandedTitle) +
                "\">" +
                (Expanded ? expandedTitle : closedTitle) +
                "</a>");
        }

        public HtmlString ExpandLink(string closedTitle, string expandedTitle)
        {
            return new HtmlString(
                "<a href=\"#\" class=\"accordian-expand-link s-expand\" data-target=\"#" +
                ContentDropDownId +
                "\" data-toggletext=\"" +
                (Expanded ? closedTitle : expandedTitle) +
                "\">" +
                (Expanded ? expandedTitle : closedTitle) +
                "</a>");
        }
    }
}