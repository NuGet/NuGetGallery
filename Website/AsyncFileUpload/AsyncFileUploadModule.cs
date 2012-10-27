using System;
using System.Diagnostics;
using System.IO;
using System.Web;

namespace NuGetGallery.AsyncFileUpload
{
    public class AsyncFileUploadModule : IHttpModule
    {
        public void Dispose()
        {
        }

        public void Init(HttpApplication application)
        {
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
            AsyncFileUploadManager.SetProgressDetails(username, progress);

            if (request.ReadEntityBodyMode != ReadEntityBodyMode.None)
            {
                return;
            }

            Stream uploadStream = request.GetBufferedInputStream();
            Debug.Assert(uploadStream != null);

            ReadStream(uploadStream, progress, requestParser);
        }

        private void ReadStream(Stream stream, AsyncFileUploadProgress progress, AsyncFileUploadRequestParser parser)
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

#if DEBUG
                // for demo purpose only
                System.Threading.Thread.Sleep(500);
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

            if (contentType.IndexOf("boundary=") < 0)
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