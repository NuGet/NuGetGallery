using System.Collections.Generic;

namespace NuGetGallery
{
    public class Windows8PackageCurator : TagBasedPackageCurator
    {
        protected override IEnumerable<string> RequiredTags
        {
            get
            {
                yield return "winrt";
                yield return "win8";
                yield return "windows8";
                yield return "winjs";
            }
        }

        protected override string CuratedFeedName
        {
            get { return "windows8-packages"; }
        }
    }
}