
using System.Diagnostics.CodeAnalysis;
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
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "Type is immutable")]
        public static readonly NullReportService Instance = new NullReportService();

        private NullReportService() { }

        public Task<StatisticsReport> Load(string name)
        {
            return Task.FromResult<StatisticsReport>(null);
        }
    }
}