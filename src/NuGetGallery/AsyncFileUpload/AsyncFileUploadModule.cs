using System;
using System.Diagnostics;
using System.IO;
using System.Web;
using Ninject;

namespace NuGetGallery.AsyncFileUpload
{
    public class AsyncFileUploadModule : IHttpModule
    {
        private ICacheService _cacheService;

        public void Dispose()
        {
        }

        public void Init(HttpApplication application)
        {
            _cacheService = Container.Kernel.Get<ICacheService>();

            application.PostAuthenticateRequest += PostAuthorizeRequest;
        }

        private void PostAuthorizeRequest(object sender, EventArgs e)
        {
            HttpApplication app = sender as HttpApplication;

            if (!app.Context.User.Identity.IsAuthenticated)
            {
                return;
            }

            if (!IsAsyncUploadRequest(app.Context))
            {
                return;
            }

            var username = app.Context.User.Identity.Name;
            if (String.IsNullOrEmpty(username))
            {
                return;
            }

            HttpRequest request = app.Context.Request;
            string contentType = request.ContentType;
            int boundaryIndex = contentType.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase);
            string boundary = "--" + contentType.Substring(boundaryIndex + 9);
            var requestParser = new AsyncFileUploadRequestParser(boundary, request.ContentEncoding);

            var progress = new AsyncFileUploadProgress(request.ContentLength);
            _cacheService.SetProgress(username, progress);

            if (request.ReadEntityBodyMode != ReadEntityBodyMode.None)
            {
                return;
            }

            Stream uploadStream = request.GetBufferedInputStream();
            Debug.Assert(uploadStream != null);

            ReadStream(uploadStream, request, username, progress, requestParser);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "request", Justification="'request' parameter is used in debug build.")]
        private void ReadStream(Stream stream, HttpRequest request, string username, AsyncFileUploadProgress progress, AsyncFileUploadRequestParser parser)
        {
            const int bufferSize = 1024 * 4; // in bytes

            byte[] buffer = new byte[bufferSize];
            while (progress.BytesRemaining > 0)
            {
                int bytesRead = stream.Read(buffer, 0, Math.Min(progress.BytesRemaining, bufferSize));
                progress.TotalBytesRead = bytesRead == 0
                                          ? progress.ContentLength
                                          : (progress.TotalBytesRead + bytesRead);

                if (bytesRead > 0)
                {
                    parser.ParseNext(buffer, bytesRead);
                    progress.FileName = parser.CurrentFileName;
                }

                _cacheService.SetProgress(username, progress);

#if DEBUG
                if (request.IsLocal)
                {
                    // If the request is from local machine, the upload will be too fast to see the progress.
                    // Slow it down a bit.
                    System.Threading.Thread.Sleep(30);
                }
#endif
            }
        }

        private static bool IsAsyncUploadRequest(HttpContext context)
        {
            // not a POST request
            if (!context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // not a multipart content type
            string contentType = context.Request.ContentType;
            if (contentType == null || !contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (contentType.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            // Don't deal with transfer-encoding-chunked and less than 4KB
            if (context.Request.ContentLength < 4096)
            {
                return false;
            }

            return true;
        }
    }
}