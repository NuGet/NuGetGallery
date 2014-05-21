using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public static class Constants
    {
        static Constants()
        {
            Package = new Uri("http://nuget.org/schema#Package");
            DeletePackage = new Uri("http://nuget.org/schema#DeletePackage");
            DeleteRegistration = new Uri("http://nuget.org/schema#DeleteRegistration");
            CatalogRoot = new Uri("http://nuget.org/catalog#Root");
            CatalogPage = new Uri("http://nuget.org/catalog#Page");
        }

        public static Uri Package
        {
            get;
            private set;
        }
        public static Uri DeletePackage
        {
            get;
            private set;
        }
        public static Uri DeleteRegistration
        {
            get;
            private set;
        }
        public static Uri CatalogRoot
        {
            get;
            private set;
        }
        public static Uri CatalogPage
        {
            get;
            private set;
        }
    }
}
