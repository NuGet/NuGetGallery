// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public class AnalysisFactAttribute : FactAttribute
    {
        public AnalysisFactAttribute()
        {
            if (!AzureSearchConfiguration.Create().TestSettings.RunAzureSearchAnalysisTests)
            {
                Skip = "Azure Search Analyzer tests are disabled";
            }
        }
    }
}
