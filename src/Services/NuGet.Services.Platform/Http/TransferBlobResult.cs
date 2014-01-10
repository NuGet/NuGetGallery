using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Services.Http
{
    public class TransferBlobResult : IHttpActionResult
    {
        private ICloudBlob _blob;

        public TransferBlobResult(ICloudBlob blob)
        {
            _blob = blob;
        }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var content = new PushStreamContent(async (strm, _, __) =>
            {
                using (strm)
                {
                    await _blob.DownloadToStreamAsync(strm);
                }
            });
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(_blob.Properties.ContentType);
            if (!String.IsNullOrEmpty(_blob.Properties.ContentEncoding))
            {
                content.Headers.ContentEncoding.Add(_blob.Properties.ContentEncoding);
            }
            if (!String.IsNullOrEmpty(_blob.Properties.ContentLanguage))
            {
                content.Headers.ContentLanguage.Add(_blob.Properties.ContentLanguage);
            }
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            };
            if (!String.IsNullOrEmpty(_blob.Properties.ETag))
            {
                response.Headers.ETag = EntityTagHeaderValue.Parse(_blob.Properties.ETag);
            }
            return Task.FromResult(response);
        }
    }
}
