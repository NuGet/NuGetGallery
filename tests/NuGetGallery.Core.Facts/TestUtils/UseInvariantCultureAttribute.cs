// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Xunit.Sdk;

namespace NuGetGallery.TestUtils
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class UseInvariantCultureAttribute : BeforeAfterTestAttribute
    {
        private CultureInfo originalUICulture;

        public CultureInfo OriginalUICulture { get; set; }

        public override void Before(MethodInfo methodUnderTest)
        {
            originalUICulture = Thread.CurrentThread.CurrentUICulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        public override void After(MethodInfo methodUnderTest)
        {
            Thread.CurrentThread.CurrentUICulture = this.originalUICulture;
        }
    }
}
