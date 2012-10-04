using System;
using System.Collections.Generic;
using System.Linq;
using NuGet;
using NuGetGallery.ViewModels.PackagePart;

namespace NuGetGallery
{
    public class PackageContentsViewModel
    {
        private readonly PackageItem _rootFolder;
        private readonly IPackage _packageMetadata;

        public PackageContentsViewModel(IPackage packageMetadata, PackageItem rootFolder)
        {
            _packageMetadata = packageMetadata;
            _rootFolder = rootFolder;

            FlattenedAuthors = String.Join(", ", packageMetadata.Authors);
            FlattenedOwners = String.Join(", ", packageMetadata.Owners);
            IconUrl = packageMetadata.IconUrl == null ? null : packageMetadata.IconUrl.AbsoluteUri;
            FrameworkAssemblies = packageMetadata.FrameworkAssemblies.Select(
                f => 
                    {
                        if (f.SupportedFrameworks.Any())
                        {
                            return String.Format("{0} ({1})", f.AssemblyName, String.Join("; ", f.SupportedFrameworks));
                        }
                        else
                        {
                            return f.AssemblyName;
                        }
                    }).ToList();
        }

        public IPackage PackageMetadata
        {
            get { return _packageMetadata; }
        }

        public PackageItem RootFolder
        {
            get { return _rootFolder; }
        }

        public string FlattenedAuthors
        {
            get;
            private set;
        }

        public string FlattenedOwners
        {
            get;
            private set;
        }

        public ICollection<string> FrameworkAssemblies
        {
            get;
            private set;
        }

        public string IconUrl
        {
            get;
            private set;
        }
    }
}