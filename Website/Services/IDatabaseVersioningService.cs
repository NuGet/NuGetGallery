using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Services
{
    public interface IDatabaseVersioningService
    {
        ISet<string> AppliedVersions { get; }
        ISet<string> PendingVersions { get; }
        ISet<string> AvailableVersions { get; } 
    }

    public static class DatabaseVersioningServiceExtensions
    {
        public static bool HasVersion(this IDatabaseVersioningService self, string versionName)
        {
            return self.AppliedVersions.Contains(versionName);
        }
    }
}
