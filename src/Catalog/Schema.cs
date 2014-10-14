using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public static class Schema
    {
        public static class Prefixes
        {
            public static readonly string NuGet = "http://schema.nuget.org/schema#";
            public static readonly string Catalog = "http://schema.nuget.org/catalog#";
            public static readonly string Xsd = "http://www.w3.org/2001/XMLSchema#";
            public static readonly string Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        }

        public static class DataTypes
        {
            public static readonly Uri Package = new Uri(Prefixes.NuGet + "Package");
            public static readonly Uri PackageDetails = new Uri(Prefixes.NuGet + "PackageDetails");
            public static readonly Uri PackageDependencyGroup = new Uri(Prefixes.NuGet + "PackageDependencyGroup");
            public static readonly Uri PackageDependency = new Uri(Prefixes.NuGet + "PackageDependency");

            public static readonly Uri CatalogRoot = new Uri(Prefixes.Catalog + "CatalogRoot");
            public static readonly Uri CatalogPage = new Uri(Prefixes.Catalog + "CatalogPage");

            public static readonly Uri Integer = new Uri(Prefixes.Xsd + "integer");
            public static readonly Uri DateTime = new Uri(Prefixes.Xsd + "dateTime");
            public static readonly Uri Boolean = new Uri(Prefixes.Xsd + "boolean");
        }

        public static class Predicates
        {
            public static readonly Uri Type = new Uri(Prefixes.Rdf + "type");

            public static readonly Uri CatalogCommitId = new Uri(Prefixes.Catalog + "commitId");
            public static readonly Uri CatalogTimeStamp = new Uri(Prefixes.Catalog + "commitTimeStamp");
            public static readonly Uri CatalogItem = new Uri(Prefixes.Catalog + "item");
            public static readonly Uri CatalogCount = new Uri(Prefixes.Catalog + "count");

            public static readonly Uri GalleryKey = new Uri(Prefixes.Catalog + "galleryKey");
            public static readonly Uri GalleryChecksum = new Uri(Prefixes.Catalog + "galleryChecksum");
            public static readonly Uri Parent = new Uri(Prefixes.Catalog + "parent");

            public static readonly Uri Id = new Uri(Prefixes.NuGet + "id");
            public static readonly Uri Version = new Uri(Prefixes.NuGet + "version");

            public static readonly Uri Upper = new Uri(Prefixes.NuGet + "upper");
            public static readonly Uri Lower = new Uri(Prefixes.NuGet + "lower");

            public static readonly Uri CatalogEntry = new Uri(Prefixes.NuGet + "catalogEntry");
            public static readonly Uri PackageContent = new Uri(Prefixes.NuGet + "packageContent");

            // General-purpose fields
            
            public static readonly Uri Author = new Uri(Prefixes.NuGet + "author");
            public static readonly Uri Copyright = new Uri(Prefixes.NuGet + "copyright");
            public static readonly Uri Created = new Uri(Prefixes.NuGet + "created");
            public static readonly Uri Description = new Uri(Prefixes.NuGet + "description");
            public static readonly Uri IconUrl = new Uri(Prefixes.NuGet + "iconUrl");

            public static readonly Uri Package = new Uri(Prefixes.NuGet + "package");
            public static readonly Uri Registration = new Uri(Prefixes.NuGet + "registration");

            public static readonly Uri Language = new Uri(Prefixes.NuGet + "language");
            public static readonly Uri Published = new Uri(Prefixes.NuGet + "published");
            public static readonly Uri LastEdited = new Uri(Prefixes.NuGet + "lastEdited");
            public static readonly Uri PackageHash = new Uri(Prefixes.NuGet + "packageHash");
            public static readonly Uri PackageHashAlgorithm = new Uri(Prefixes.NuGet + "packageHashAlgorithm");
            public static readonly Uri PackageSize = new Uri(Prefixes.NuGet + "packageSize");
            public static readonly Uri ProjectUrl = new Uri(Prefixes.NuGet + "projectUrl");
            public static readonly Uri GalleryDetailsUrl = new Uri(Prefixes.NuGet + "galleryDetailsUrl");
            public static readonly Uri ReleaseNotes = new Uri(Prefixes.NuGet + "releaseNotes");
            public static readonly Uri RequireLicenseAcceptance = new Uri(Prefixes.NuGet + "requireLicenseAcceptance");
            public static readonly Uri Summary = new Uri(Prefixes.NuGet + "summary");
            public static readonly Uri Title = new Uri(Prefixes.NuGet + "title");
            public static readonly Uri LicenseUrl = new Uri(Prefixes.NuGet + "licenseUrl");
            public static readonly Uri LicenseReportUrl = new Uri(Prefixes.NuGet + "licenseReportUrl");
            public static readonly Uri MinimumClientVersion = new Uri(Prefixes.NuGet + "minimumClientVersion");
            public static readonly Uri Tag = new Uri(Prefixes.NuGet + "tag");
            public static readonly Uri LicenseName = new Uri(Prefixes.NuGet + "licenseName");

        }
    }
}
