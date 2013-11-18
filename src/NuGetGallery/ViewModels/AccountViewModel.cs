using System.Collections.Generic;

namespace NuGetGallery
{
    public class AccountViewModel
    {
        public bool IsConfirmed { get; set; }
        public IEnumerable<string> CuratedFeeds { get; set; }
    }
}