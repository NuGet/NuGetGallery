using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Http.Models
{
    public class ApiDescription
    {
        public Uri Self { get; private set; }
        public string Host { get; set; }
        public IDictionary<string, Uri> Services { get; private set; }

        public ApiDescription(Uri baseUrl, IEnumerable<NuGetHttpService> httpServices)
        {
            Self = baseUrl;
            Services = httpServices.ToDictionary(
                service => service.Name.Service.ToLowerInvariant(),
                service => new Uri(baseUrl, service.BasePath.ToUriComponent()),
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
