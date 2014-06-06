using System;
using System.Threading.Tasks;
using System.Web;

namespace NuGetGallery
{
    public interface IContentService
    {
        Task<IHtmlString> GetContentItemAsync(string name, TimeSpan expiresIn);
        void ClearCache();
    }
}
