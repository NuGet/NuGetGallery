// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Services.Validation
{
    public class ValidationStatusFacts
    {
        /// <summary>
        /// This enum is persisted so the integer values must not change.
        /// </summary>
        [Theory]
        [InlineData(0, ValidationStatus.NotStarted)]
        [InlineData(1, ValidationStatus.Incomplete)]
        [InlineData(2, ValidationStatus.Succeeded)]
        [InlineData(3, ValidationStatus.Failed)]
        public void HasUnchangingValues(int expected, ValidationStatus input)
        {
            Assert.Equal(expected, (int)input);
            Assert.Equal(4, Enum.GetValues(typeof(ValidationStatus)).Length);
        }
    }
}
