using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
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
                return (statistics != null);
            }
        }
    }
}