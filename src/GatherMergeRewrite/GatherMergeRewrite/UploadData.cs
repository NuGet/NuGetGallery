using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GatherMergeRewrite
{
    public class UploadData
    {
        public string OwnerId { get; set; }
        public string RegistrationId { get; set; }
        public string Nupkg { get; set; }
        public DateTime Published { get; set; }
    }
}
