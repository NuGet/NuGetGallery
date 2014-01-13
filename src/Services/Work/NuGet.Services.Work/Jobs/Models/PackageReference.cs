using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Work.Jobs.Models
{
    public class PackageReference
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Hash { get; set; }
    }
}
