using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public class FileSystemEmulatorHandler : DelegatingHandler
    {
        private Uri _rootUrl;
        private string _rootNormalized;

        public FileSystemEmulatorHandler() : base() { }
        public FileSystemEmulatorHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

        public string RootFolder { get; set; }
        public Uri BaseAddress
        {
            get
            { return _rootUrl; }
            set
            {
                _rootUrl = value;
                _rootNormalized = GetNormalizedUrl(_rootUrl);
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Compare the URL
            string requestNormalized = GetNormalizedUrl(request.RequestUri);
            if (requestNormalized.StartsWith(_rootNormalized))
            {
                var relative = requestNormalized.Substring(_rootNormalized.Length);
                return Intercept(relative, request, cancellationToken);
            }
            else
            {
                return base.SendAsync(request, cancellationToken);
            }
        }

        private Task<HttpResponseMessage> Intercept(string relative, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = Path.Combine(
                RootFolder,
                relative.Trim('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                // The framework will dispose of the original stream after "transferring" it
                var content = new StreamContent(
                    new FileStream(path, FileMode.Open, FileAccess.Read));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = content
                });
            }
            else
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }

        private static string GetNormalizedUrl(Uri url)
        {
            return url.GetComponents(
                UriComponents.HostAndPort |
                UriComponents.UserInfo |
                UriComponents.Path,
                UriFormat.SafeUnescaped);
        }
    }
}
