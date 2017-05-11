// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;
using System.Web.Mvc;

namespace NuGetGallery.Helpers
{
    public class AccordionHelper
    {
        private readonly bool _expanded;

        public string Name { get; }
        public string FormModelStatePrefix { get; }
        public WebViewPage Page { get; }
        public string ItemId { get { return Name + "-item"; } }
        public string ContentDropDownId { get { return Name + "-content"; } }
        public string CollapseButtonId { get { return Name + "-collapse"; } }
        public string ContentHiddenClass { get { return Expanded ? null : "s-hidden"; } }

        public bool Expanded
        {
            get
            {
                return _expanded
                    || (FormModelStatePrefix != null && !Page.ViewData.ModelState.IsValidField(FormModelStatePrefix));
            }
        }

        public AccordionHelper(string name, string formModelStatePrefix, bool expanded, WebViewPage page)
        {
            _expanded = expanded;

            Name = name;
            FormModelStatePrefix = formModelStatePrefix;
            Page = page;
        }

        public HtmlString ExpandButton(string closedTitle, string expandedTitle)
        {
            return new HtmlString(
                "<button class=\"accordion-expand-button btn btn-inline s-expand\" data-target=\"#" +
                ContentDropDownId +
                "\" data-toggletext=\"" +
                (Expanded ? closedTitle : expandedTitle) +
                "\" id=\"" +
                CollapseButtonId +
                "\" aria-expanded=\"" +
                (Expanded ? "true" : "false") +"\">" +
                (Expanded ? expandedTitle : closedTitle) +
                "</button>");
        }

        public HtmlString ExpandLink(string closedTitle, string expandedTitle)
        {
            return new HtmlString(
                "<button class=\"accordion-expand-link s-expand\" data-target=\"#" +
                ContentDropDownId +
                "\" data-toggletext=\"" +
                (Expanded ? closedTitle : expandedTitle) +
                "\" id=\"" +
                CollapseButtonId +
                "\" aria-expanded=\"" +
                (Expanded ? "true" : "false") + "\">" +
                (Expanded ? expandedTitle : closedTitle) +
                "</button>");
        }
    }
}