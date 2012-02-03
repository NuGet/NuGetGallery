using System.Collections.Generic;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Infrastructure
{
    public class LuceneIndexingServiceFacts
    {
        [Theory]
        [InlineData(new object[] { "NHibernate", new string[0] })]
        [InlineData(new object[] { "NUnit", new string[0] })]
        [InlineData(new object[] { "SisoDb", new[] { "Siso", "Db" } })]
        [InlineData(new object[] { "EntityFramework", new[] { "Entity", "Framework" } })]
        [InlineData(new object[] { "Sys-netFX", new[] { "Sys-net", "FX" } })]
        [InlineData(new object[] { "Sys-netX", new[] { "Sys-net" } })]
        [InlineData(new object[] { "xUnit", new string[0] })]
        [InlineData(new object[] { "jQueryUI", new[] { "jQuery", "UI" } })]
        [InlineData(new object[] { "NuGetPowerTools", new[] { "NuGet", "Power", "Tools" } })]
        [InlineData(new object[] { "microsoft-web-helpers", new string[0] })]
        public void CamelCaseTokenizer(string term, IEnumerable<string> tokens)
        {
            // Act
            var result = LuceneIndexingService.CamelCaseTokenize(term);

            // Assert
            Assert.Equal(tokens, result);
        }
    }
}
