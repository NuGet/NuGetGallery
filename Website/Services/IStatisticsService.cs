using Newtonsoft.Json.Linq;

namespace NuGetGallery
{
    public interface IStatisticsService
    {
        JArray LoadReport(string name);
    }
}