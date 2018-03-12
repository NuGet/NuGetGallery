using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IPackage 
    {
        PackageRegistration PackageRegistration { get; set; }

        int PackageRegistrationKey { get; set; }

        //int DownloadCount { get; set; }

        //string Title { get; set; }
        
        string Version { get; set; }

        int Key { get; set; }

        string NormalizedVersion { get; set; }

        PackageStatus PackageStatusKey { get; set; }

        string HashAlgorithm { get; set; }

        
        string Hash { get; set; }

        DateTime Created { get; set; }
    }
}
