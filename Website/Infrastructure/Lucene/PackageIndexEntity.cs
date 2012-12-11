using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NuGetGallery
{
    public class PackageIndexEntity
    {
        public int Key { get; set; }

        public int PackageRegistrationKey { get; set; }

        public int PackageRegistrationDownloadCount { get; set; }

        public string IconUrl { get; set; }

        public string Id { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Tags { get; set; }

        public string Authors { get; set; }

        public int DownloadCount { get; set; }

        public bool IsLatest { get; set; }

        public bool IsLatestStable { get; set; }

        public IEnumerable<string> Owners { get; set; }

        public DateTime Published { get; set; }
    }
}