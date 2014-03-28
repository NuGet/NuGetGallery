using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GatherMergeRewrite
{
    public interface IPackageHandle
    {
        Task<PackageData> GetData();
    }
}
