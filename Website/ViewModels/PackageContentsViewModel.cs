using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using NuGet;
using NuGetGallery.ViewModels.PackagePart;

namespace NuGetGallery
{
    public class PackageContentsViewModel
    {
        private readonly PackageItem _rootFolder;
        private readonly IPackageMetadata _packageMetadata;

        public PackageContentsViewModel(IPackageMetadata packageMetadata, ICollection<User> owners, PackageItem rootFolder)
        {
            _packageMetadata = packageMetadata;
            _rootFolder = rootFolder;

            FlattenedAuthors = String.Join(", ", packageMetadata.Authors);
            FlattenedOwners = String.Join(", ", owners.Select(o => o.Username));
            IconUrl = packageMetadata.IconUrl == null ? null : packageMetadata.IconUrl.AbsoluteUri;
            FrameworkAssemblies = packageMetadata.FrameworkAssemblies.Select(
                f => 
                    {
                        if (f.SupportedFrameworks.Any())
                        {
                            return String.Format(CultureInfo.InvariantCulture, "{0} ({1})", f.AssemblyName, String.Join("; ", f.SupportedFrameworks));
                        }
                        else
                        {
                            return f.AssemblyName;
                        }
                    }).ToList();
        }

        public IPackageMetadata PackageMetadata
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

        public string GetNpeProtocolUrl(UrlHelper urlHelper)
        {
            return GetNpeProtocolUrl(urlHelper, PackageMetadata.Id, PackageMetadata.Version.ToString());
        }

        public string GetNpeActivationArgument(UrlHelper urlHelper)
        {
            return PackageMetadata.Id + "|" +
                PackageMetadata.Version.ToString() + "|" +
                urlHelper.PackageDownload(2, PackageMetadata.Id, PackageMetadata.Version.ToString());
        }

        public static string GetNpeProtocolUrl(UrlHelper urlHelper, string id, string version)
        {
            return String.Format(
                CultureInfo.InvariantCulture,
                "npe://none?id={0}&version={1}&url={2}",
                id,
                version,
                urlHelper.PackageDownload(2, id, version));
        }
    }
}