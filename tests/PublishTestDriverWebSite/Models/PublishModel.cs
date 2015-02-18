
using System.Collections.Generic;

namespace PublishTestDriverWebSite.Models
{
    public class PublishModel
    {
        public PublishModel()
        {
            Domains = new List<string>();
        }
        public IList<string> Domains { get; private set;  }
        public string Message { get; set; }
    }
}