// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public class RelevancyTheoryAttribute : TheoryAttribute
    {
        public RelevancyTheoryAttribute()
        {
            if (!AzureSearchConfiguration.Create().TestSettings.RunAzureSearchRelevancyTests)
            {
                Skip = "Azure search Relevancy tests are disabled";
            }
        }
    }
}
