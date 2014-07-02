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
            public static readonly string NuGetSchema = "http://schema.nuget.org/schema#";
            public static readonly string Catalog = "http://schema.nuget.org/catalog#";
            public static readonly string Gallery = "http://schema.nuget.org/gallery#";

            public static readonly string Xsd = "http://www.w3.org/2001/XMLSchema#";
            public static readonly string Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        }

        public static class DataTypes
        {
            public static readonly Uri Package = new Uri(Prefixes.NuGetSchema + "Package");
            public static readonly Uri DeletePackage = new Uri(Prefixes.NuGetSchema + "DeletePackage");
            public static readonly Uri DeleteRegistration = new Uri(Prefixes.NuGetSchema + "DeleteRegistration");
            public static readonly Uri CatalogRoot = new Uri(Prefixes.Catalog + "Root");
            public static readonly Uri CatalogPage = new Uri(Prefixes.Catalog + "Page");
            public static readonly Uri Resolver = new Uri(Prefixes.Catalog + "Resolver");
            public static readonly Uri Integer = new Uri(Prefixes.Xsd + "integer");
            public static readonly Uri DateTime = new Uri(Prefixes.Xsd + "dateTime");
        }

        public static class Predicates
        {
            public static readonly Uri Type = new Uri(Prefixes.Rdf + "type");

            public static readonly Uri CatalogCommitId = new Uri(Prefixes.Catalog + "commitId");
            public static readonly Uri CatalogTimestamp = new Uri(Prefixes.Catalog + "timeStamp");
            public static readonly Uri CatalogCommitUserData = new Uri(Prefixes.Catalog + "commitUserData");
            public static readonly Uri CatalogItem = new Uri(Prefixes.Catalog + "item");
            public static readonly Uri CatalogParent = new Uri(Prefixes.Catalog + "parent");
            public static readonly Uri CatalogCount = new Uri(Prefixes.Catalog + "count");
            public static readonly string CatalogPropertyPrefix = Prefixes.Catalog + "property$";

            public static readonly Uri GalleryKey = new Uri(Prefixes.Gallery + "key");
            public static readonly Uri GalleryChecksum = new Uri(Prefixes.Gallery + "checksum");

            public static readonly Uri PackageId = new Uri(Prefixes.NuGetSchema + "id");
            public static readonly Uri PackageVersion = new Uri(Prefixes.NuGetSchema + "version");
        }
    }
}
