using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.PackageHash
{
    public interface IPackageHashProcessor
    {
        Task ExecuteAsync(int bucketNumber, int bucketCount, CancellationToken token);
    }
}