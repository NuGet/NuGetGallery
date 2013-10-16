
using System.Threading.Tasks;
using NuGetGallery.Infrastructure;

namespace NuGetGallery
{
    public interface IReportService
    {
        Task<StatisticsReport> Load(string name);
    }

    public class NullReportService : IReportService
    {
        public static readonly NullReportService Instance = new NullReportService();

        private NullReportService() { }

        public Task<StatisticsReport> Load(string name)
        {
            return Task.FromResult<StatisticsReport>(null);
        }
    }
}