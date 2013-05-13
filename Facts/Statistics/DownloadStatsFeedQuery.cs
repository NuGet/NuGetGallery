using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NuGetGallery.Commands;

namespace NuGetGallery.Statistics
{
    public class DownloadStatsFeedQuery : Query<JArray>
    {
        public int? Count { get; private set; }

        public DownloadStatsFeedQuery()
        {
            Count = null;
        }

        public DownloadStatsFeedQuery(int count)
        {
            Count = count;
        }

        public override JArray Execute()
        {
            throw new NotImplementedException();
        }

        public override bool Equals(object obj)
        {
            DownloadStatsFeedQuery other = obj as DownloadStatsFeedQuery;
            return other != null &&
                Equals(other.Count, Count);
        }

        public override int GetHashCode()
        {
            return Count.GetHashCode();
        }
    }
}
