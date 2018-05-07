// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Xunit;
using NuGetGallery.Auditing.Obfuscation;
using System.Collections.Generic;
using Moq;

namespace NuGetGallery.Auditing
{
    public class ObfuscatorJsonConverterTests
    {
        public static IEnumerable<object[]> ObfuscationTypes
        {
            get
            {
                foreach (var obfuscationType in Enum.GetValues(typeof(ObfuscationType)))
                {
                    yield return new[] { obfuscationType };
                }
            }
        }

        [Theory]
        [MemberData(nameof(ObfuscationTypes))]
        public void ReturnsExpectedObfuscation(ObfuscationType obfuscationType)
        {
            // Arrange
            var obfuscatorJsonConverter = new ObfuscatorJsonConverter(obfuscationType);
            var jsonWriterMock = new Mock<JsonWriter>();
            var value = "127.0.0.1";

            // Act
            obfuscatorJsonConverter.WriteJson(jsonWriterMock.Object, value, new JsonSerializer());

            // Assert
            jsonWriterMock.Verify(x => x.WriteValue(ObfuscatorJsonConverter.Obfuscate(value, obfuscationType)));
        }
    }
}
