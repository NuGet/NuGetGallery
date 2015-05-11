// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections;
using System.Web.UI;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public partial class _Default : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            IList visibleTables = DynamicDataManager.DefaultModel.VisibleTables;
            if (visibleTables.Count == 0)
            {
                throw new InvalidOperationException(
                    "There are no accessible tables. Make sure that at least one data model is registered in Global.asax and scaffolding is enabled or implement custom pages.");
            }
            Menu1.DataSource = visibleTables;
            Menu1.DataBind();
        }
    }
}