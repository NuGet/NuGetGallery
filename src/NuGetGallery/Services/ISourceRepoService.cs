using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace NuGetGallery
{
    public interface ISourceRepoService
    {
        Task<SourceRepositoryViewModel> Load(Package package);
    }
}