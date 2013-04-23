using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace NuGetGallery
{
    public interface IContentService
    {
        Task<HtmlString> GetContentItemAsync(string name, TimeSpan expiresIn);
    }
}
