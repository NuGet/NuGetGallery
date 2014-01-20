using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services
{
    public interface ICredentialProvider
    {
        bool ApplyLocalCacheCredentials(HttpRequestMessage request);
        Task<bool> ApplyCredentials(HttpResponseMessage unauthorizedResponse, HttpRequestMessage request);
    }
}
