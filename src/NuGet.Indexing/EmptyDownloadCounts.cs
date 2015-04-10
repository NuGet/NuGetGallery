using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Indexing
{
    class EmptyDownloadCounts : DownloadCounts
    {
        public override string Path
        {
            get { return "empty"; }
        }

        protected override JObject LoadJson()
        {
            return null;
        }
    }
}
