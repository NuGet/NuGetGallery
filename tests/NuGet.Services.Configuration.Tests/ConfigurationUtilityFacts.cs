// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace NuGet.Services.Configuration.Tests
{
    public class ConfigurationUtilityFacts
    {
        public static IEnumerable<object[]> ValueData => new[]
        {
            new object[] {true},
            new object[] {false},
            new object[] {"hello"},
            new object[] {"123456789"},
            new object[] {-1},
            new object[] {1259},
            new object[] {DateTime.MinValue},
            new object[] {DateTime.MinValue.AddYears(100).AddDays(50).AddHours(5).AddSeconds(62)}
        };

        [Theory]
        [MemberData(nameof(ValueData))]
        public void ConvertFromStringConvertsToType<T>(T value)
        {
            // Arrange
            var valueString = value.ToString();

            // Act
            var result = ConfigurationUtility.ConvertFromString<T>(valueString);

            // Assert
            Assert.Equal(value, result);
        }

        private class NoConversionFromStringToThisClass
        {
        }

        [Fact]
        public void ConvertFromStringThrowsNotSupportedException()
        {
            // Arrange
            const string key = "convert me!";

            // Assert
            Assert.Throws<NotSupportedException>(
                () => ConfigurationUtility.ConvertFromString<NoConversionFromStringToThisClass>(key));
        }
    }
}
