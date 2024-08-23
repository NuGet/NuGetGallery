// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Revalidate
{
    /// <summary>
    /// The configuration needed to query an Application Insights account using
    /// the REST endpoints.
    /// </summary>
    public class ApplicationInsightsConfiguration
    {
        /// <summary>
        /// The Application Insights account identifier.
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// The API Key used to access the Application Insights account.
        /// </summary>
        public string ApiKey { get; set; }
    }
}
