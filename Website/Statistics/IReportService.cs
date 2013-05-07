
using System.Threading.Tasks;

namespace NuGetGallery.Statistics
{
    public interface IReportService
    {
        Task<string> Load(string name);
    }
}