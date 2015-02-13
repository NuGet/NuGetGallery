
using Newtonsoft.Json.Linq;

namespace PublishTestDriverWebSite.Models
{
    public class PackagesModel
    {
        public JArray Registrations { get; set; }

        public string Raw { get; set; }
    }
}