using System;
using Xunit;

namespace NuGetGallery {
    public class CryptographyServiceFacts {
        public class TheConvertToBase64UrlStringMethod {
            [Fact]
            public void ReturnsBase64ForUrlVariantEncodedRandomString() {
                var crypto = new CryptographyService();
                var bytes = Convert.FromBase64String("P+c/");

                string encoded = crypto.ConvertToBase64UrlString(bytes);

                Assert.Equal("P-c_", encoded);
            }
        }
    }
}
