// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using Xunit;

namespace NuGetGallery.Helpers
{
    public class EnumHelperFacts
    {
        public class TheGetDescriptionMethod
        {
            [Theory]
            [InlineData(TestEnum.Foo, "Foo")]
            [InlineData(TestEnum.Bar, "Bar")]
            [InlineData(TestEnum.Baz, "Qux")]
            public void GetsCorrectDescription(TestEnum value, string description)
            {
                Assert.Equal(description, EnumHelper.GetDescription(value));
            }
        }

        public enum TestEnum
        {
            Foo,
            Bar,

            [Description("Qux")]
            Baz
        }
    }
}
