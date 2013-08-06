
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IReportService
    {
        Task<string> Load(string name);
    }
}