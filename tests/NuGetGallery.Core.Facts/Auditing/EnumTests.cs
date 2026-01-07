// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Xunit;

namespace NuGetGallery.Auditing
{
    public abstract class EnumTests
    {
        protected void Verify(Type enumType, string[] expectedNames)
        {
            var actualNames = Enum.GetNames(enumType);

            Assert.Equal(expectedNames.Length, actualNames.Length);

            var actualNotInExpected = actualNames.Except(expectedNames);
            var expectedNotInActual = expectedNames.Except(actualNames);

            var commonMessage = $"The {enumType.Name} enum definition has changed.  Please evaluate this change against all {nameof(AuditingService)} implementations.";

            Assert.False(actualNotInExpected.Any(), $"{commonMessage}  Unexpected members found:  {string.Join(", ", actualNotInExpected)}");
            Assert.False(expectedNotInActual.Any(), $"{commonMessage}  Expected members not found:  {string.Join(", ", expectedNotInActual)}");
        }
    }
}