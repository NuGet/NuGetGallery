// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace NuGet.Services.Configuration.Tests
{
    public class DictionaryExtensionsFacts
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

        public static IEnumerable<object[]> NullEmptyData => new[]
        {
            new object[] {""},
            new object[] {null}
        };

        [Theory]
        [MemberData(nameof(ValueData))]
        public void GetOrDefaultReturnsValueInDictionaryConverted<T>(T value)
        {
            // Arrange
            const string key = "key";
            IDictionary<string, string> dictionary = new Dictionary<string, string>
            {
                {key, value.ToString()}
            };

            // Act
            var valueFromDictionary = dictionary.GetOrDefault<T>(key);

            // Assert
            Assert.Equal(value, valueFromDictionary);
        }

        [Theory]
        [MemberData(nameof(ValueData))]
        public void GetOrDefaultReturnsDefaultIfKeyNotFound<T>(T value)
        {
            // Arrange
            const string notKey = "notAKey";
            IDictionary<string, string> dictionary = new Dictionary<string, string>();

            // Act
            var notFoundFromDictionary = dictionary.GetOrDefault<T>(notKey);
            var notFoundFromDictionaryWithDefault = dictionary.GetOrDefault(notKey, value);

            // Assert
            Assert.Equal(default(T), notFoundFromDictionary);
            Assert.Equal(value, notFoundFromDictionaryWithDefault);
        }

        private class NoConversionFromStringToThisClass
        {
            public bool Value { get; }

            public NoConversionFromStringToThisClass(bool value)
            {
                Value = value;
            }
        }

        [Fact]
        public void GetOrDefaultReturnsDefaultIfConversionUnsupported()
        {
            // Arrange
            const string key = "key";
            const string notKey = "notAKey";
            IDictionary<string, string> dictionary = new Dictionary<string, string>
            {
                {key, "i am a string"}
            };

            // Act
            var unsupportedFromDictionary = dictionary.GetOrDefault<NoConversionFromStringToThisClass>(notKey);

            // default(NoConversionFromStringToThisClass) has value = false because default(bool) is false
            // Therefore, create a NoConversionFromStringToThisClass with a true value so it is different than the default.
            var defaultNoConversion = new NoConversionFromStringToThisClass(true);
            var unsupportedFromDictionaryWithDefault = dictionary.GetOrDefault(notKey, defaultNoConversion);

            // Assert
            Assert.Throws<NotSupportedException>(() => dictionary.GetOrDefault<NoConversionFromStringToThisClass>(key));
            Assert.Equal(default(NoConversionFromStringToThisClass), unsupportedFromDictionary);
            Assert.Equal(defaultNoConversion, unsupportedFromDictionaryWithDefault);
            // Safety check to prevent the test from passing if defaultNoConversion is equal to default(NoConversionFromStringToThisClass)
            Assert.NotEqual(default(NoConversionFromStringToThisClass), defaultNoConversion);
        }

        [Theory]
        [MemberData(nameof(ValueData))]
        public void GetOrThrowThrowsIfKeyNotFound<T>(T value)
        {
            // Arrange
            const string notKey = "notKey";

            IDictionary<string, string> dictionary = new Dictionary<string, string>
            {
                { "otherKey", value.ToString() }
            };

            // Assert
            Assert.Throws<KeyNotFoundException>(() => dictionary.GetOrThrow<T>(notKey));
        }

        [Theory]
        [MemberData(nameof(ValueData))]
        public void GetOrThrowThrowsIfConversionUnsupported<T>(T value)
        {
            // Arrange
            const string key = "key";
            IDictionary<string, string> dictionary = new Dictionary<string, string>
            {
                {key, value.ToString()}
            };

            // Act
            var valueFromDictionary = dictionary.GetOrThrow<T>(key);

            // Assert
            Assert.Equal(value, valueFromDictionary);
            Assert.Throws<NotSupportedException>(() => dictionary.GetOrThrow<NoConversionFromStringToThisClass>(key));
        }

        [Theory]
        [MemberData(nameof(NullEmptyData))]
        public void GetOrThrowThrowsIfNullOrEmptyValueForKey(string value)
        {
            // Arrange
            const string key = "key";
            IDictionary<string, string> dictionary = new Dictionary<string, string>
            {
                {key, value}
            };

            // Assert
            Assert.Throws<ArgumentException>(() => dictionary.GetOrThrow<string>(key));
        }
    }
}
