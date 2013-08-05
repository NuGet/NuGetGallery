using System;
using Xunit;

namespace NuGetGallery
{
    public class UrlExtensionsFacts
    {
        public class TheEnsureTrailingSlashHelperMethod
        {
            [Fact]
            public void Works()
            {
                string fixedUrl = UrlExtensions.EnsureTrailingSlash("http://nuget.org/packages/FooPackage.CS");
                Assert.True(fixedUrl.EndsWith("/", StringComparison.Ordinal));
            }

            [Fact]
            public void PropagatesNull()
            {
                string fixedUrl = UrlExtensions.EnsureTrailingSlash(null);
                Assert.Null(fixedUrl);
            }
        }
    }
}
