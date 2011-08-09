using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace NuGet.Server.Infrastructure {
    public class DerivedPackageData {
        public long PackageSize { get; set; }
        public string PackageHash { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        public DateTimeOffset Created { get; set; }
        public string Path { get; set; }
        public IEnumerable<FrameworkName> SupportedFrameworks { get; set; }
    }
}
