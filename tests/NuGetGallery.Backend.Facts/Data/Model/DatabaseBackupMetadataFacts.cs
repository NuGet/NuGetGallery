using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Backend.Models;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery.Backend.Data.Model
{
    public class DatabaseBackupMetadataFacts
    {
        [Theory]
        [InlineData("burkurp")] // No timestamp
        [InlineData("backup_nov122013@1505")] // Wrong timestamp format
        [InlineData("backup_November12_2013_4_43_pm_PST")] // Wrong timestamp format
        [InlineData("Backup_12Nov2013_2043")] // Wrong timestamp format (missing trailing "Z")
        public void ReturnsNullForNonMatchingDatabaseName(string name)
        {
            Assert.Null(DatabaseBackup.Create(new Database() { name = name }));
        }

        [Theory]
        [InlineData("Backup_2013Nov12_2043Z", "Backup", "2013-11-12T2043")]
        [InlineData("Backup_2013nOv12_2043Z", "Backup", "2013-11-12T2043")]
        [InlineData("Backup_1924Dec12_0042z", "Backup", "1924-12-12T0042")]
        [InlineData("Burkurp_1924deC12_0042Z", "Burkurp", "1924-12-12T0042")]
        [InlineData("WarehouseBackup_1924Dec12_0042Z", "WarehouseBackup_", "1924-12-12T0042")]
        public void ParsesMatchingNameCorrectly(string name, string prefix, string expectedTimestamp)
        {
            var parsed = DatabaseBackup.Create(new Database() { name = name });
            Assert.NotNull(parsed);
            Assert.Equal(prefix, parsed.Prefix);
            Assert.Equal(expectedTimestamp, parsed.Timestamp.ToString("s"));
        }
    }
}
