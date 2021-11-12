// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGetGallery.Frameworks
{
    public class SupportedFrameworksFacts
    {
        [Fact]
        public void SupportedFrameworksContainsAllNuGetClientCommonFrameworks()
        {
            var fields = typeof(FrameworkConstants.CommonFrameworks)
                .GetFields()
                .Where(f => f.FieldType == typeof(NuGetFramework))
                .ToList();

            Assert.True(fields.Count > 0);

            var supportedFrameworks = new HashSet<NuGetFramework>(SupportedFrameworks.AllSupportedNuGetFrameworks);

            foreach (var field in fields)
            {
                var framework = (NuGetFramework)field.GetValue(null);

                Assert.True(supportedFrameworks.Contains(framework), $"SupportedFrameworks is missing {field.Name} constant from CommonFrameworks.");
            }
        }
    }
}
