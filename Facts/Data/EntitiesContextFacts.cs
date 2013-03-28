using System.Data;
using Xunit;

namespace NuGetGallery.Data
{
    class EntitiesContextFacts
    {
        [Fact]
        public void SaveChangesFailsInReadOnlyMode()
        {
            var ec = new EntitiesContext("", readOnly: true);
            Assert.Throws<ReadOnlyException>(() => ec.Users.Add(new User
            {
                Key = -1,
                Username = "FredFredFred",
            }));
        }
    }
}
