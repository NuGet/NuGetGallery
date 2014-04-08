using System.Threading.Tasks;
namespace NuGetGallery
{
    public interface IAggregateStatsService
    {
        Task<AggregateStats> GetAggregateStats();
    }
}