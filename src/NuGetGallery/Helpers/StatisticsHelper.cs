// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Web.Mvc;

namespace NuGetGallery
{
    public static class StatisticsHelper
    {
        public static bool IsStatisticsPageAvailable
        {
            get
            {
                var statistics = DependencyResolver.Current.GetService<IStatisticsService>();
                return (statistics != NullStatisticsService.Instance);
            }
        }
    }
}