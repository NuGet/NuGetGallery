// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Services.Validation
{
    public class ValidationErrorCodeFacts
    {
        /// <summary>
        /// This enum is persisted so the integer values must not change.
        /// </summary>
        [Theory]
        [InlineData(0, ValidationIssueCode.Unknown)]
        [InlineData(1, ValidationIssueCode.PackageIsSigned)]
#pragma warning disable 618
        [InlineData(9999, ValidationIssueCode.ObsoleteTesting)]
#pragma warning restore 618
        public void HasUnchangingValues(int expected, ValidationStatus input)
        {
            Assert.Equal(expected, (int)input);
            Assert.Equal(3, Enum.GetValues(typeof(ValidationIssueCode)).Length);
        }
    }
}
