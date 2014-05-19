using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalog
{
    public static class Constants
    {
        static Constants()
        {
            Package = new Uri("http://nuget.org/schema#Package");
            DeletePackage = new Uri("http://nuget.org/schema#DeletePackage");
            DeleteRegistration = new Uri("http://nuget.org/schema#DeleteRegistration");
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
    }
}
