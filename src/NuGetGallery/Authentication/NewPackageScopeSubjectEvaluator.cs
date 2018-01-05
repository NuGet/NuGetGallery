using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Authentication
{
    public class NewPackageScopeSubjectConverter : IScopeSubjectConverter<ActionOnNewPackageContext>
    {
        public string ConvertToScopeSubject(ActionOnNewPackageContext subject)
        {
            return subject.PackageId;
        }
    }
}