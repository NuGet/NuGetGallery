using System.Collections.Generic;

namespace NuGet.Jobs.Common
{
    public class CuratedPackages
    {
        private readonly Dictionary<string, List<string>> _packageToFeeds = new Dictionary<string, List<string>>();

        public List<string> this[string packageId]
        {
            get
            {
                List<string> feeds;

                if (_packageToFeeds.TryGetValue(packageId, out feeds))
                {
                    return feeds;
                }

                feeds = new List<string>();
                _packageToFeeds.Add(packageId, feeds);

                return feeds;
            }
        }

        public int Count { get { return _packageToFeeds.Count; } }

        public IEnumerable<string> Keys { get { return _packageToFeeds.Keys; } }
    }
}
