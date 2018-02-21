using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation
{
    public interface IPackageDownloader
    {
        Task<Stream> DownloadAsync(Uri packageUri, CancellationToken cancellationToken);
    }
}