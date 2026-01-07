// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using NuGetGallery.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class ApiScopeEvaluationResultFacts
    {
        public class TheIsSuccessfulMethod
        {
            public static IEnumerable<object[]> AllPossible_Data
            {
                get
                {
                    foreach (var result in Enum.GetValues(typeof(PermissionsCheckResult)).Cast<PermissionsCheckResult>())
                    {
                        foreach (var user in new[] { null, new User("testUnconfirmed") { Key = 1 }, new User("testConfirmed") { Key = 2, EmailAddress = "something@confirmed.com" } })
                        {
                            foreach (var scopesAreValid in new[] { false, true })
                            {
                                yield return MemberDataHelper.AsData(
                                    scopesAreValid, 
                                    result, 
                                    user, 
                                    scopesAreValid && result == PermissionsCheckResult.Allowed && (user?.Confirmed ?? false));
                            }
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(AllPossible_Data))]
            public void ReturnsExpected(bool scopesAreValid, PermissionsCheckResult result, User owner, bool expectedIsSuccessful)
            {
                var apiScopeEvaluationResult = new ApiScopeEvaluationResult(owner, result, scopesAreValid);

                Assert.Equal(expectedIsSuccessful, apiScopeEvaluationResult.IsSuccessful());
            }
        }
    }
}
