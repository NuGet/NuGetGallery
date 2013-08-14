
using System.Threading.Tasks;
using NuGetGallery.Infrastructure;

namespace NuGetGallery
{
    public interface IReportService
    {
        Task<StatisticsReport> Load(string name);
    }
}