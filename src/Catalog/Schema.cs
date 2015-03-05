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
            public static readonly string Record = "http://schema.nuget.org/record#";
            public static readonly string Xsd = "http://www.w3.org/2001/XMLSchema#";
            public static readonly string Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        }

        public static class DataTypes
        {
            public static readonly Uri Package = new Uri(Prefixes.NuGet + "Package");
            public static readonly Uri PackageRegistration = new Uri(Prefixes.NuGet + "PackageRegistration");
            public static readonly Uri PackageDetails = new Uri(Prefixes.NuGet + "PackageDetails");
            public static readonly Uri PackageDependencyGroup = new Uri(Prefixes.NuGet + "PackageDependencyGroup");
            public static readonly Uri PackageDependency = new Uri(Prefixes.NuGet + "PackageDependency");
            public static readonly Uri NuGetClassicPackage = new Uri(Prefixes.NuGet + "NuGetClassicPackage");
            public static readonly Uri ApiAppPackage = new Uri(Prefixes.NuGet + "ApiAppPackage");
            public static readonly Uri PowerShellPackage = new Uri(Prefixes.NuGet + "PowerShellPackage");

            public static readonly Uri PackageEntry = new Uri(Prefixes.NuGet + "PackageEntry");

            public static readonly Uri CatalogRoot = new Uri(Prefixes.Catalog + "CatalogRoot");
            public static readonly Uri CatalogPage = new Uri(Prefixes.Catalog + "CatalogPage");
            public static readonly Uri Permalink = new Uri(Prefixes.Catalog + "Permalink");
            public static readonly Uri AppendOnlyCatalog = new Uri(Prefixes.Catalog + "AppendOnlyCatalog");

            public static readonly Uri Integer = new Uri(Prefixes.Xsd + "integer");
            public static readonly Uri DateTime = new Uri(Prefixes.Xsd + "dateTime");
            public static readonly Uri Boolean = new Uri(Prefixes.Xsd + "boolean");

            public static readonly Uri Icon = new Uri(Prefixes.NuGet + "Icon");
            public static readonly Uri Screenshot = new Uri(Prefixes.NuGet + "Screenshot");
            public static readonly Uri ZipArchive = new Uri(Prefixes.NuGet + "ZipArchive");
            public static readonly Uri HeroIcon = new Uri(Prefixes.NuGet + "HeroIcon");
            public static readonly Uri LargeIcon = new Uri(Prefixes.NuGet + "LargeIcon");
            public static readonly Uri MediumIcon = new Uri(Prefixes.NuGet + "MediumIcon");
            public static readonly Uri SmallIcon = new Uri(Prefixes.NuGet + "SmallIcon");
            public static readonly Uri WideIcon = new Uri(Prefixes.NuGet + "WideIcon");
            public static readonly Uri CsmTemplate = new Uri(Prefixes.NuGet + "CsmTemplate");

            public static readonly Uri Record = new Uri(Prefixes.Record + "Record");
            public static readonly Uri RecordRegistration = new Uri(Prefixes.Record + "Registration");
            public static readonly Uri RecordOwner = new Uri(Prefixes.Record + "Owner");
        }

        public static class Predicates
        {
            public static readonly Uri Type = new Uri(Prefixes.Rdf + "type");

            public static readonly Uri CatalogCommitId = new Uri(Prefixes.Catalog + "commitId");
            public static readonly Uri CatalogTimeStamp = new Uri(Prefixes.Catalog + "commitTimeStamp");
            public static readonly Uri CatalogItem = new Uri(Prefixes.Catalog + "item");
            public static readonly Uri CatalogCount = new Uri(Prefixes.Catalog + "count");
            public static readonly Uri CatalogParent = new Uri(Prefixes.Catalog + "parent");

            public static readonly Uri GalleryKey = new Uri(Prefixes.Catalog + "galleryKey");
            public static readonly Uri GalleryChecksum = new Uri(Prefixes.Catalog + "galleryChecksum");

            public static readonly Uri Prefix = new Uri(Prefixes.NuGet + "prefix");
            public static readonly Uri Id = new Uri(Prefixes.NuGet + "id");
            public static readonly Uri Version = new Uri(Prefixes.NuGet + "version");

            public static readonly Uri Upper = new Uri(Prefixes.NuGet + "upper");
            public static readonly Uri Lower = new Uri(Prefixes.NuGet + "lower");

            public static readonly Uri CatalogEntry = new Uri(Prefixes.NuGet + "catalogEntry");
            public static readonly Uri PackageContent = new Uri(Prefixes.NuGet + "packageContent");

            public static readonly Uri PackageEntry = new Uri(Prefixes.NuGet + "packageEntry");
            public static readonly Uri FullName = new Uri(Prefixes.NuGet + "fullName");
            public static readonly Uri Name = new Uri(Prefixes.NuGet + "name");
            public static readonly Uri Length = new Uri(Prefixes.NuGet + "length");
            public static readonly Uri CompressedLength = new Uri(Prefixes.NuGet + "compressedLength");

            public static readonly Uri FileName = new Uri(Prefixes.Catalog + "fileName");
            public static readonly Uri Details = new Uri(Prefixes.Catalog + "details");

            public static readonly Uri Domain = new Uri(Prefixes.Record + "domain");
            public static readonly Uri RecordDomain = new Uri(Prefixes.Record + "recordDomain");
            public static readonly Uri RecordRegistration = new Uri(Prefixes.Record + "recordRegistration");
            public static readonly Uri ObjectId = new Uri(Prefixes.Record + "objectId");

            public static readonly Uri Visibility = new Uri(Prefixes.NuGet + "visibility");
            public static readonly Uri Organization = new Uri(Prefixes.NuGet + "organization");
            public static readonly Uri Subscription = new Uri(Prefixes.NuGet + "subscription");

            // General-purpose fields
            
            public static readonly Uri Author = new Uri(Prefixes.NuGet + "author");
            public static readonly Uri Copyright = new Uri(Prefixes.NuGet + "copyright");
            public static readonly Uri Created = new Uri(Prefixes.NuGet + "created");
            public static readonly Uri Description = new Uri(Prefixes.NuGet + "description");
            public static readonly Uri IconUrl = new Uri(Prefixes.NuGet + "iconUrl");

            public static readonly Uri Package = new Uri(Prefixes.NuGet + "package");
            public static readonly Uri Registration = new Uri(Prefixes.NuGet + "registration");

            public static readonly Uri LastCreated = new Uri(Prefixes.NuGet + "lastCreated");
            public static readonly Uri LastEdited = new Uri(Prefixes.NuGet + "lastEdited");

            public static readonly Uri Language = new Uri(Prefixes.NuGet + "language");
            public static readonly Uri Published = new Uri(Prefixes.NuGet + "published");
            public static readonly Uri Publisher = new Uri(Prefixes.NuGet + "publisher");
            public static readonly Uri UserName = new Uri(Prefixes.NuGet + "userName");
            public static readonly Uri Tenant = new Uri(Prefixes.NuGet + "tenant");
            public static readonly Uri UserId = new Uri(Prefixes.NuGet + "userId");
            public static readonly Uri TenantId = new Uri(Prefixes.NuGet + "tenantId");
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
            public static readonly Uri SupportedFramework = new Uri(Prefixes.NuGet + "supportedFramework");
            public static readonly Uri Owner = new Uri(Prefixes.NuGet + "owner");
        }
    }
}
