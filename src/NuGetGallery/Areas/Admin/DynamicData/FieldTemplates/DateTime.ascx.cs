// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Web.DynamicData;
using System.Web.UI;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public partial class DateTimeField : FieldTemplateUserControl
    {
        public override Control DataControl
        {
            get { return Literal1; }
        }
    }
}