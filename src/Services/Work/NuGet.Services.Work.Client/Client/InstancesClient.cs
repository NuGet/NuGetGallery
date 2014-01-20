using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Client;
using NuGet.Services.Work.Models;

namespace NuGet.Services.Work.Client
{
    public class InstancesClient
    {
        private HttpClient _client;

        public InstancesClient(HttpClient client)
        {
            _client = client;
        }

        public Task<ServiceResponse<IEnumerable<InstanceStatistics>>> GetStatistics()
        {
            return _client.GetAsync("instances/stats").AsServiceResponse<IEnumerable<InstanceStatistics>>();
        }
    }
}
