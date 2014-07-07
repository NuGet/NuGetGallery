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
            CatalogRoot = new Uri("http://nuget.org/catalog#Root");
            CatalogPage = new Uri("http://nuget.org/catalog#Page");

            Package = new Uri("http://nuget.org/schema#Package");
            DeletePackage = new Uri("http://nuget.org/schema#DeletePackage");
            DeleteRegistration = new Uri("http://nuget.org/schema#DeleteRegistration");
            Resolver = new Uri("http://nuget.org/schema#Resolver");

            Range = new Uri("http://nuget.org/gallery#Range");

            RdfType = new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type");
            Integer = new Uri("http://www.w3.org/2001/XMLSchema#integer");
            DateTime = new Uri("http://www.w3.org/2001/XMLSchema#dateTime");
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
        public static Uri Resolver
        {
            get;
            private set;
        }
        public static Uri Range
        {
            get;
            private set;
        }

        public static Uri RdfType
        {
            get;
            private set;
        }
        public static Uri Integer
        {
            get;
            private set;
        }
        public static Uri DateTime
        {
            get;
            private set;
        }
    }
}
