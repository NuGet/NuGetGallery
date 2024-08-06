// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Configuration;
using System.Collections.Generic;
using WebBackgrounder;

namespace NuGetGallery.Infrastructure.Lucene
{
    public interface IIndexingJobFactory
    {
        void RegisterBackgroundJobs(IList<IJob> jobs, IAppConfiguration configuration);
    }
}
