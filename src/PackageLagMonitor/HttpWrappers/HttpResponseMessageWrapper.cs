// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class HttpResponseMessageWrapper : IHttpResponseMessageWrapper
    {
        private HttpResponseMessage _responseMessage;

        public HttpResponseMessageWrapper(HttpResponseMessage message)
        {
            _responseMessage = message;
        }

        public IHttpContentWrapper Content {
            get
            {
                return new HttpContentWrapper(_responseMessage.Content);
            }
        }

        public bool IsSuccessStatusCode => _responseMessage.IsSuccessStatusCode;

        public string ReasonPhrase { get => _responseMessage.ReasonPhrase; set => _responseMessage.ReasonPhrase = value; }

        public HttpStatusCode StatusCode { get => _responseMessage.StatusCode; set => _responseMessage.StatusCode = value; }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _responseMessage.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
