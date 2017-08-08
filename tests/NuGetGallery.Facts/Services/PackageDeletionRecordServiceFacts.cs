// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace NuGetGallery
{
    public class PackageDeletionRecordServiceFacts
    {
        [Fact]
        public Task SaveDeletionRecord_ThrowsIfPackageNull()
        {
            var fileStorageService = new Mock<IFileStorageService>();
            var service = CreateService(fileStorageService);

            return Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveDeletionRecord(null));
        }

        [Fact]
        public async Task SaveDeletionRecord_SavesFile()
        {
            var fileStorageService = new Mock<IFileStorageService>();
            var service = CreateService(fileStorageService);
            var package = new Package
            {
                PackageRegistration = new PackageRegistration { Id = "TestPackage" },
                NormalizedVersion = "1.0.0"
            };

            var filenamePrefix = $"{package.PackageRegistration.Id.ToLowerInvariant()}/{package.NormalizedVersion.ToLowerInvariant()}/";
            var now = DateTime.UtcNow;

            Expression<Func<IFileStorageService, Task>> saveFileExpression = 
                x => x.SaveFileAsync(
                    It.Is<string>(s => s == Constants.PackageDeletesFolderName),
                    It.Is<string>(s => s.StartsWith(filenamePrefix)),
                    It.IsAny<Stream>(),
                    It.Is<bool>(b => b == false));

            fileStorageService.Setup(saveFileExpression)
                .Returns(Task.CompletedTask)
                .Callback<string, string, Stream, bool>((folder, filename, stream, overwrite) => 
                {
                    // We must verify the stream's correctness in this callback because it is too complex for an Expression.
                    var reader = new StreamReader(stream);
                    var content = reader.ReadToEnd();
                    var deletionRecord = JsonConvert.DeserializeObject<PackageDeletionRecord>(content);

                    Assert.Equal(package.PackageRegistration.Id, deletionRecord.Id);
                    Assert.Equal(package.NormalizedVersion, deletionRecord.NormalizedVersion);
                    Assert.True(now <= deletionRecord.DeletedTimestamp, "The deletion record's timestamp for a package must be set when the record is created!");
                });

            await service.SaveDeletionRecord(package);

            fileStorageService.Verify(saveFileExpression);
        }

        private static PackageDeletionRecordService CreateService(Mock<IFileStorageService> fileStorageService = null)
        {
            fileStorageService = fileStorageService ?? new Mock<IFileStorageService>();

            return new PackageDeletionRecordService(fileStorageService.Object);
        }
    }
}
