using System.Collections.Generic;

namespace NuGetGallery
{
    public interface IPageableEnumerable<T>
    {
        IEnumerable<T> Items { get; }
        int PageIndex { get; }
        int PageSize { get; }
    }
}
