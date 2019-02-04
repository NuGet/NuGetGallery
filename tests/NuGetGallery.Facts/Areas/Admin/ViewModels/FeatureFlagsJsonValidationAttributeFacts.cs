// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGetGallery.Features;
using Xunit;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class FeatureFlagsJsonValidationAttributeFacts
    {
        private readonly FeatureFlagsJsonValidationAttribute _target = new FeatureFlagsJsonValidationAttribute();

        [Theory]
        [InlineData(true)]
        [InlineData(1)]
        [InlineData('a')]
        public void IfValueIsntAString_ReturnsFalse(object input)
        {
            Assert.False(_target.IsValid(input));
        }

        [Theory]
        [MemberData(nameof(ValidatesJsonData))]
        public void ValidatesJson(string input, bool valid)
        {
            Assert.Equal(valid, _target.IsValid(input));
        }

        public static IEnumerable<object[]> ValidatesJsonData()
        {
            foreach (var json in FeatureFlagJsonHelper.ValidJson)
            {
                yield return new object[] { json, true };
            }

            foreach (var json in FeatureFlagJsonHelper.InvalidJson)
            {
                yield return new object[] { json, false };
            }
        }
    }
}
