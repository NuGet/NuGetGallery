// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class PackageDeleteBackupServiceFacts
    {
        [Fact]
        public async Task RoundTripsAndOverwritesDeletionBackup()
        {
            var fixture = new Fixture();
            var backup = CreateBackup();
            backup.ExternalReadmeMarkdown = "# README\r\n雪 😀 \"quoted\" \\path\n[link](https://example.test)";
            backup.Symbols.Add(new SymbolPackageDeleteBackup
            {
                SymbolPackageKey = 31, PackageStatusKey = PackageStatus.Available,
                PublicBackupHash = Convert.ToBase64String(Enumerable.Repeat((byte)2, 64).ToArray()),
            });
            backup.IncomingDeprecationKeys.Add(50);

            await fixture.Service.SaveAsync(backup);
            var actual = await fixture.Service.GetAsync(10, 20);

            Assert.Equal(backup.PackageStatusKey, actual.PackageStatusKey);
            Assert.Equal(backup.Listed, actual.Listed);
            Assert.Equal(backup.DeletedOnUtc, actual.DeletedOnUtc);
            Assert.Equal(DateTimeKind.Utc, actual.DeletedOnUtc.Kind);
            Assert.Equal(backup.PackageId, actual.PackageId);
            Assert.Equal(backup.NormalizedVersion, actual.NormalizedVersion);
            Assert.Equal(backup.ExternalReadmeMarkdown, actual.ExternalReadmeMarkdown);
            Assert.Equal(backup.PublicBackupHash, actual.PublicBackupHash);
            Assert.Equal(backup.ValidationBackupHash, actual.ValidationBackupHash);
            var symbol = Assert.Single(actual.Symbols);
            Assert.Equal(31, symbol.SymbolPackageKey);
            Assert.Equal(PackageStatus.Available, symbol.PackageStatusKey);
            Assert.Equal(backup.Symbols[0].PublicBackupHash, symbol.PublicBackupHash);
            Assert.Null(symbol.ValidationBackupHash);
            Assert.Equal(50, Assert.Single(actual.IncomingDeprecationKeys));
            Assert.Equal(CoreConstants.JsonContentType, fixture.ContentType);
            Assert.Equal("package-delete-backups/10/20/package-delete-backup.json", Assert.Single(fixture.Files).Key);

            backup.Listed = false;

            await fixture.Service.SaveAsync(backup);

            actual = await fixture.Service.GetAsync(10, 20);
            Assert.False(actual.Listed);
            Assert.Equal(backup.ExternalReadmeMarkdown, actual.ExternalReadmeMarkdown);
            var bytes = Assert.Single(fixture.Files).Value.ToArray();
            await fixture.Service.GetAsync(10, 20);
            Assert.Equal(bytes, Assert.Single(fixture.Files).Value);

            backup.Symbols[0].PackageStatusKey = PackageStatus.Deleted;
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Service.SaveAsync(backup));
            Assert.Equal(bytes, Assert.Single(fixture.Files).Value);
        }

        private static PackageDeleteBackup CreateBackup()
        {
            return new PackageDeleteBackup
            {
                PackageDeleteKey = 10, PackageKey = 20, PackageId = "Example.Package", NormalizedVersion = "1.0.0",
                DeletedOnUtc = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc),
                PackageStatusKey = PackageStatus.Available, Listed = true,
                PublicBackupHash = Convert.ToBase64String(new byte[64]),
                ValidationBackupHash = Convert.ToBase64String(Enumerable.Repeat((byte)1, 64).ToArray()),
            };
        }

        private class Fixture
        {
            public Mock<ICoreFileStorageService> Storage { get; } = new Mock<ICoreFileStorageService>(MockBehavior.Strict);
            public Dictionary<string, byte[]> Files { get; } = new Dictionary<string, byte[]>();
            public string ContentType { get; private set; }
            public PackageDeleteBackupService Service { get; }
            public Fixture()
            {
                Storage.Setup(s => s.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), true))
                    .Returns<string, string, string, Stream, bool>((folder, name, contentType, stream, overwrite) =>
                    {
                        var key = folder + "/" + name;

                        using (var copy = new MemoryStream())
                        {
                            stream.CopyTo(copy);
                            Files[key] = copy.ToArray();
                        }

                        ContentType = contentType;
                        return Task.CompletedTask;
                    });
                Storage.Setup(s => s.GetFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns<string, string>((folder, name) => Task.FromResult<Stream>(Files.TryGetValue(folder + "/" + name, out var bytes)
                        ? new MemoryStream(bytes) : null));
                Service = new PackageDeleteBackupService(Storage.Object);
            }
        }
    }
}