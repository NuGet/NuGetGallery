// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using WebBackgrounder;

namespace NuGetGallery
{
    public class CloudDownloadCountServiceRefreshJob : Job
    {
        private readonly CloudDownloadCountService _downloadCountService;

        public CloudDownloadCountServiceRefreshJob(TimeSpan interval, CloudDownloadCountService downloadCountService)
            : base("", interval)
        {
            _downloadCountService = downloadCountService;
        }

        public override Task Execute()
        {
            return _downloadCountService.Refresh();
        }
    }
}