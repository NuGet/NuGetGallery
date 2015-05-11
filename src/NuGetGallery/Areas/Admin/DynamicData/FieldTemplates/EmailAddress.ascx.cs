// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Web.DynamicData;
using System.Web.UI;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public partial class EmailAddressField : FieldTemplateUserControl
    {
        public override Control DataControl
        {
            get { return HyperLink1; }
        }

        protected override void OnDataBinding(EventArgs e)
        {
            string url = FieldValueString;
            if (!url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                url = "mailto:" + url;
            }
            HyperLink1.NavigateUrl = url;
        }
    }
}