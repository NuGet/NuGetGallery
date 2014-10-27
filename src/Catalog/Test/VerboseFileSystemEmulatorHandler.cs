using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Test
{
    public class VerboseFileSystemEmulatorHandler : FileSystemEmulatorHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Console.WriteLine(request.RequestUri);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
