using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Authentication
{
    public class ExistingPackageScopeSubjectConverter : IScopeSubjectConverter<PackageRegistration>
    {
        public string ConvertToScopeSubject(PackageRegistration subject)
        {
            return subject.Id;
        }
    }
}