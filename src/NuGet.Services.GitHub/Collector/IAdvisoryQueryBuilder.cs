// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.GitHub.GraphQL;

namespace NuGet.Services.GitHub.Collector
{
    public interface IAdvisoryQueryBuilder
    {
        int GetMaximumResultsPerRequest();
        string CreateSecurityAdvisoriesQuery(DateTimeOffset? updatedSince = null, string afterCursor = null);
        string CreateSecurityAdvisoryQuery(SecurityAdvisory advisory);
    }
}