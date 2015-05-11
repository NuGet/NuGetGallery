// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Web.DynamicData;
using System.Web.UI;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public partial class UrlField : FieldTemplateUserControl
    {
        public override Control DataControl
        {
            get { return HyperLinkUrl; }
        }

        protected override void OnDataBinding(EventArgs e)
        {
            HyperLinkUrl.NavigateUrl = ProcessUrl(FieldValueString);
        }

        private static string ProcessUrl(string url)
        {
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            return "http://" + url;
        }
    }
}