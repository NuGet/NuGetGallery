using System.Web.Mvc;
using NuGetGallery.Statistics;

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