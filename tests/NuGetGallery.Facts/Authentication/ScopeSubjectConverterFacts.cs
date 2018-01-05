using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class ScopeSubjectConverterFacts
    {
        public class TheConvertToScopeSubjectMethod
        {
            public class TheNewPackageScopeSubjectConverterClass
            {
                [Fact]
                public void ReturnsPackageId()
                {
                    var context = new ActionOnNewPackageContext("asdfasdf", new Mock<ReservedNamespaceService>().Object);
                    var scopeSubjectConverter = new NewPackageScopeSubjectConverter();

                    Assert.Equal(context.PackageId, scopeSubjectConverter.ConvertToScopeSubject(context));
                }
            }

            public class TheExistingPackageScopeSubjectConverterClass
            {
                [Fact]
                public void ReturnsId()
                {
                    var packageRegistration = new PackageRegistration { Id = "fdsafdsa" };
                    var scopeSubjectConverter = new ExistingPackageScopeSubjectConverter();

                    Assert.Equal(packageRegistration.Id, scopeSubjectConverter.ConvertToScopeSubject(packageRegistration));
                }
            }
        }
    }
}
