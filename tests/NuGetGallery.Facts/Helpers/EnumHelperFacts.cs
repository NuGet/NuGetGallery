﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;

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
