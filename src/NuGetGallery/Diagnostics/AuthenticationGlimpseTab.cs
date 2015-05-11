// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Glimpse.AspNet.Extensibility;
using Glimpse.Core.Extensibility;

namespace NuGetGallery.Diagnostics
{
    public class AuthenticationGlimpseTab : AspNetTab
    {
        public override object GetData(ITabContext context)
        {
            return context.GetRequestContext<HttpContextBase>().User;
        }

        public override string Name
        {
            get { return "Auth"; }
        }
    }
}