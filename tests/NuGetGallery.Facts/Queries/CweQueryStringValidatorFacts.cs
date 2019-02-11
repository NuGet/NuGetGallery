// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.Queries
{
    public class CweQueryStringValidatorFacts
    {
        public class TheValidateMethod
        {
            [Theory]
            [InlineData("CWE-001", "CWE-001", CweQueryMethod.ByCweId)]
            [InlineData("Cwe-001", "CWE-001", CweQueryMethod.ByCweId)]
            [InlineData("cwe-001", "CWE-001", CweQueryMethod.ByCweId)]
            [InlineData("01", "CWE-01", CweQueryMethod.ByCweId)]
            [InlineData("name", "name", CweQueryMethod.ByName)]
            public void DeterminesQueryMethod(string queryString, string expectedValidatedString, CweQueryMethod expectedQueryMethod)
            {
                var actualValidatedString = CweQueryStringValidator.Validate(queryString, out var actualQueryMethod);

                Assert.Equal(expectedQueryMethod, actualQueryMethod);
                Assert.Equal(expectedValidatedString, actualValidatedString);
            }
        }
    }
}