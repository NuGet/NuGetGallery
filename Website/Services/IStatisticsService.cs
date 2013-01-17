using Newtonsoft.Json.Linq;

namespace NuGetGallery
{
    public interface IStatisticsService
    {
        string LoadReport(string name);
    }
}