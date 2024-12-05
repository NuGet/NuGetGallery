// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery.Helpers
{
    public class CSPInlineStypeHelper : TagHelper
    {
       public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (output)
            {
                
            }
            output.Attributes.RemoveAll("style");
        }
    }
 
}
