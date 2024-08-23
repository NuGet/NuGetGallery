// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Jobs.Monitoring.PackageLag
{
    public class HttpContentWrapper : IHttpContentWrapper
    {
        private HttpContent _content;

        public HttpContentWrapper(HttpContent content)
        {
            _content = content;
        }

        public async Task<string> ReadAsStringAsync()
        {
            var output = await _content.ReadAsStringAsync();
            return output;
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _content.Dispose();
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
