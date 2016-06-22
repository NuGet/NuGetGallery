// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;
using System.Web.Mvc;

namespace NuGetGallery.Helpers
{
    public class AccordeonHelper
    {
        private readonly bool _expanded;

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
                return _expanded
                    || (FormModelStatePrefix != null && !Page.ViewData.ModelState.IsValidField(FormModelStatePrefix));
            }
        }

        public AccordeonHelper(string name, string formModelStatePrefix, bool expanded, WebViewPage page)
        {
            _expanded = expanded;

            Name = name;
            FormModelStatePrefix = formModelStatePrefix;
            Page = page;
        }

        public HtmlString ExpandButton(string closedTitle, string expandedTitle)
        {
            return new HtmlString(
                "<a href=\"#\" class=\"accordeon-expand-button btn btn-inline s-expand\" data-target=\"#" +
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
                "<a href=\"#\" class=\"accordeon-expand-link s-expand\" data-target=\"#" +
                ContentDropDownId +
                "\" data-toggletext=\"" +
                (Expanded ? closedTitle : expandedTitle) +
                "\">" +
                (Expanded ? expandedTitle : closedTitle) +
                "</a>");
        }
    }
}