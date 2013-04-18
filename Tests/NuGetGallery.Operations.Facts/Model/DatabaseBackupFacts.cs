using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Operations.Model
{
    public class DatabaseBackupFacts
    {
        public class TheConstructor
        {
            [Fact]
            public void GivenAnOldStyleTimeStamp_ItCorrectlyParsesIt()
            {
                // Arrange
                var expected = new DateTimeOffset(new DateTime(2013, 04, 13, 15, 10, 00), TimeSpan.Zero);

                // Act
                var backup = new DatabaseBackup("server", "Backup_20130413151000");

                // Assert
                Assert.Equal(expected, backup.Timestamp);
            }

            [Fact]
            public void GivenANewStyleTimeStamp_ItCorrectlyParsesIt()
            {
                // Arrange
                var expected = new DateTimeOffset(new DateTime(2013, 04, 13, 15, 10, 00), TimeSpan.Zero);

                // Act
                var backup = new DatabaseBackup("server", "Backup_2013Apr13_1510_UTC");

                // Assert
                Assert.Equal(expected, backup.Timestamp);
            }
        }
    }
}
