// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery
{
    public class NullReportService : IReportService
    {
        public static readonly NullReportService Instance = new NullReportService();

        private NullReportService() { }

        public Task<StatisticsReport> Load(string reportName)
        {
            return Task.FromResult<StatisticsReport>(null);
        }
    }
}