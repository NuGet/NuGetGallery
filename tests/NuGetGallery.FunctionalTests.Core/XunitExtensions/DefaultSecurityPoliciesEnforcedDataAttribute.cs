// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Reflection;
using Xunit.Sdk;

namespace NuGetGallery.FunctionalTests.XunitExtensions
{
    public class DefaultSecurityPoliciesEnforcedDataAttribute : DataAttribute
    {
        private readonly object[] _data;		
 		
         public DefaultSecurityPoliciesEnforcedDataAttribute(params object[] data)
         {		
             _data = data;

            if (!GalleryConfiguration.Instance.DefaultSecurityPoliciesEnforced)
            {
                Skip = "Default security policies are not configured on the server";
            }
        }		
 		
         public override IEnumerable<object[]> GetData(MethodInfo testMethod)
         {		
             return new[] { _data };		
         }
    }
}
