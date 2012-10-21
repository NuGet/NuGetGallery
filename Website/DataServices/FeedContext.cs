using System.Linq;

namespace NuGetGallery
{
    public class FeedContext<T>
    {
        public IQueryable<T> Packages { get; set; }
    }
}