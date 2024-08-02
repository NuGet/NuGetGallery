// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.Services.Validation;
using Xunit;

namespace NuGet.Services
{
    public class ContractsFacts
    {
        [Fact]
        public void ShouldOnlyHaveInterfacesAndEnums()
        {
            // Arrange
            var assembly = typeof(ValidationStatus).Assembly;
            var exclude = new[]
            {
                // included in the assembly by newer language versions
                "Microsoft.CodeAnalysis.EmbeddedAttribute",
                "System.Runtime.CompilerServices.RefSafetyRulesAttribute",
            };

            // Act
            var types = assembly.GetTypes();

            // Assert
            Assert.NotEmpty(types);
            foreach (var type in types)
            {
                if (exclude.Contains(type.FullName))
                {
                    continue;
                }
                Assert.True(type.IsEnum || type.IsInterface, $"{type.FullName} must either be an interface or an enum.");
            }
        }
    }
}
