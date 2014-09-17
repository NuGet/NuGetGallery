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
            public static readonly Uri PackageRegistration = new Uri(Prefixes.NuGet + "PackageRegistration");
            public static readonly Uri Package = new Uri(Prefixes.NuGet + "Package");
            public static readonly Uri PackageOverview = new Uri(Prefixes.NuGet + "PackageOverview");
            public static readonly Uri PackageInfo = new Uri(Prefixes.NuGet + "PackageInfo");
            public static readonly Uri PackageDependencyGroup = new Uri(Prefixes.NuGet + "PackageDependencyGroup");
            public static readonly Uri PackageDependency = new Uri(Prefixes.NuGet + "PackageDependency");
            public static readonly Uri RangePackages = new Uri(Prefixes.NuGet + "RangePackages");

            public static readonly Uri DeletePackage = new Uri(Prefixes.NuGet + "PackageDeletion");
            public static readonly Uri DeleteRegistration = new Uri(Prefixes.NuGet + "PackageRegistrationDeletion");

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
            public static readonly Uri CatalogTimestamp = new Uri(Prefixes.Catalog + "commitTimestamp");
            public static readonly Uri CatalogCommitUserData = new Uri(Prefixes.Catalog + "commitUserData");
            public static readonly Uri CatalogItem = new Uri(Prefixes.Catalog + "item");
            public static readonly string CatalogPropertyPrefix = Prefixes.Catalog + "commitUserProperty$";
            public static readonly Uri GalleryKey = new Uri(Prefixes.Catalog + "galleryKey");
            public static readonly Uri GalleryChecksum = new Uri(Prefixes.Catalog + "galleryChecksum");
            public static readonly Uri Parent = new Uri(Prefixes.Catalog + "parent");
            public static readonly Uri CatalogCount = new Uri(Prefixes.Catalog + "count");

            public static readonly Uri Id = new Uri(Prefixes.NuGet + "id");
            public static readonly Uri Version = new Uri(Prefixes.NuGet + "version");

            public static readonly Uri Info = new Uri(Prefixes.NuGet + "info");
            public static readonly Uri Overview = new Uri(Prefixes.NuGet + "overview");
            public static readonly Uri Range = new Uri(Prefixes.NuGet + "range");
            public static readonly Uri Low = new Uri(Prefixes.NuGet + "low");
            public static readonly Uri High = new Uri(Prefixes.NuGet + "high");
            public static readonly Uri RangePackages = new Uri(Prefixes.NuGet + "rangePackages");
            public static readonly Uri PackageRange = new Uri(Prefixes.NuGet + "packageRange");

            // General-purpose fields
            
            public static readonly Uri Author = new Uri(Prefixes.NuGet + "author");
            public static readonly Uri Copyright = new Uri(Prefixes.NuGet + "copyright");
            public static readonly Uri Created = new Uri(Prefixes.NuGet + "created");
            public static readonly Uri Description = new Uri(Prefixes.NuGet + "description");
            public static readonly Uri IconUrl = new Uri(Prefixes.NuGet + "iconUrl");
            public static readonly Uri IsLatest = new Uri(Prefixes.NuGet + "isLatest");
            public static readonly Uri IsLatestStable = new Uri(Prefixes.NuGet + "isLatestStable");
            public static readonly Uri IsPrerelease = new Uri(Prefixes.NuGet + "isPrerelease");

		    public static readonly Uri IsLatestVersion = new Uri(Prefixes.NuGet + "isLatestVersion");
			public static readonly Uri IsAbsoluteLatestVersion = new Uri(Prefixes.NuGet + "isAbsoluteLatestVersion");

            public static readonly Uri Package = new Uri(Prefixes.NuGet + "package");
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
