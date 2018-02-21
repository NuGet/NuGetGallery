using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.PackageHash
{
    public interface IResultRecorder
    {
        Task RecordAsync(IReadOnlyList<InvalidPackageHash> results);
    }
}