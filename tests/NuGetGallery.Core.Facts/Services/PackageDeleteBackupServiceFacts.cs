// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
                SymbolPackageKey = 31, SymbolPackageStatusKey = PackageStatus.Available,
                PublicBackupHash = Convert.ToBase64String(Enumerable.Repeat((byte)2, 64).ToArray()),
            });
            backup.IncomingDeprecationKeys.Add(50);

            await fixture.Service.SaveAsync(backup);
            var actual = await fixture.Service.GetAsync("Example.Package", "1.0.0", 10, 20);

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
            Assert.Equal(PackageStatus.Available, symbol.SymbolPackageStatusKey);
            Assert.Equal(backup.Symbols[0].PublicBackupHash, symbol.PublicBackupHash);
            Assert.Null(symbol.ValidationBackupHash);
            Assert.Equal(50, Assert.Single(actual.IncomingDeprecationKeys));
            Assert.Equal(CoreConstants.JsonContentType, fixture.ContentType);
            Assert.Equal("package-backups/example.package/1.0.0/package-delete-backup-10-20.json", Assert.Single(fixture.Files).Key);
            var json = JObject.Parse(Encoding.UTF8.GetString(Assert.Single(fixture.Files).Value));
            Assert.Equal("Available", (string)json["PackageStatusKey"]);
            Assert.Equal("Available", (string)json["Symbols"][0]["SymbolPackageStatusKey"]);
            Assert.Null(json["Symbols"][0]["PackageStatusKey"]);

            backup.Listed = false;

            await fixture.Service.SaveAsync(backup);

            actual = await fixture.Service.GetAsync("Example.Package", "1.0.0", 10, 20);
            Assert.False(actual.Listed);
            Assert.Equal(backup.ExternalReadmeMarkdown, actual.ExternalReadmeMarkdown);
            var bytes = Assert.Single(fixture.Files).Value.ToArray();
            await fixture.Service.GetAsync("Example.Package", "1.0.0", 10, 20);
            Assert.Equal(bytes, Assert.Single(fixture.Files).Value);

            backup.Symbols[0].SymbolPackageStatusKey = PackageStatus.Deleted;
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Service.SaveAsync(backup));
            Assert.Equal(bytes, Assert.Single(fixture.Files).Value);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RejectsSymbolBackupWithoutRequiredStatus(bool usesLegacyStatusName)
        {
            var fixture = new Fixture();
            var backup = CreateBackup();
            backup.Symbols.Add(new SymbolPackageDeleteBackup
            {
                SymbolPackageKey = 31, SymbolPackageStatusKey = PackageStatus.Available,
                PublicBackupHash = Convert.ToBase64String(new byte[64]),
            });
            await fixture.Service.SaveAsync(backup);
            var file = Assert.Single(fixture.Files);
            var json = JObject.Parse(Encoding.UTF8.GetString(file.Value));
            var symbol = (JObject)json["Symbols"][0];
            symbol.Remove("SymbolPackageStatusKey");
            if (usesLegacyStatusName)
            {
                symbol["PackageStatusKey"] = "Available";
            }
            fixture.Files[file.Key] = Encoding.UTF8.GetBytes(json.ToString());

            await Assert.ThrowsAsync<JsonSerializationException>(() => fixture.Service.GetAsync("Example.Package", "1.0.0", 10, 20));
        }

        [Fact]
        public async Task RemovesOnlyTheFailedAttemptJsonAndAllowsRepeatedCleanup()
        {
            var fixture = new Fixture();
            var failed = CreateBackup();
            var other = CreateBackup();
            other.PackageDeleteKey = 11;
            var archiveName = "package-backups/example.package/1.0.0/archive.nupkg";
            var archiveBytes = new byte[] { 1, 2, 3 };
            fixture.Files.Add(archiveName, archiveBytes);
            await fixture.Service.SaveAsync(failed);
            await fixture.Service.SaveAsync(other);

            await fixture.Service.DeleteAsync("Example.Package", "1.0.0", 10, 20);
            await fixture.Service.DeleteAsync("Example.Package", "1.0.0", 10, 20);

            Assert.Null(await fixture.Service.GetAsync("Example.Package", "1.0.0", 10, 20));
            Assert.NotNull(await fixture.Service.GetAsync("Example.Package", "1.0.0", 11, 20));
            Assert.Equal(archiveBytes, fixture.Files[archiveName]);
            Assert.Equal(2, fixture.Files.Count);
            Assert.Contains("package-backups/example.package/1.0.0/package-delete-backup-11-20.json", fixture.Files.Keys);
        }

        [Theory]
        [InlineData(PackageStatus.Available, true)]
        [InlineData(PackageStatus.Available, false)]
        [InlineData(PackageStatus.Validating, true)]
        [InlineData(PackageStatus.FailedValidation, false)]
        public async Task ColocatesSnapshotsWithoutChangingArchivesOrEarlierDeletions(PackageStatus status, bool listed)
        {
            var fixture = new Fixture();
            var archiveName = "package-backups/example.package/1.0.0/archive.nupkg";
            var archiveBytes = new byte[] { 1, 2, 3 };
            fixture.Files.Add(archiveName, archiveBytes);
            var earlier = CreateBackup();
            await fixture.Service.SaveAsync(earlier);
            var earlierJson = fixture.Files["package-backups/example.package/1.0.0/package-delete-backup-10-20.json"].ToArray();
            var later = CreateBackup();
            later.PackageDeleteKey = 11;
            later.PackageStatusKey = status;
            later.Listed = listed;
            later.ExternalReadmeMarkdown = "A later deletion snapshot";

            await fixture.Service.SaveAsync(later);

            Assert.Equal(3, fixture.Files.Count);
            Assert.Equal(archiveBytes, fixture.Files[archiveName]);
            Assert.Equal(earlierJson, fixture.Files["package-backups/example.package/1.0.0/package-delete-backup-10-20.json"]);
            var actual = await fixture.Service.GetAsync("EXAMPLE.PACKAGE", "1.0.0", 11, 20);
            Assert.Equal(status, actual.PackageStatusKey);
            Assert.Equal(listed, actual.Listed);
            Assert.Equal(later.ExternalReadmeMarkdown, actual.ExternalReadmeMarkdown);
            Assert.Equal(earlier.PublicBackupHash, actual.PublicBackupHash);
            Assert.Null(await fixture.Service.GetAsync("Example.Package", "1.0.0", 12, 20));
            Assert.Equal(3, fixture.Files.Count);
            Assert.Equal(CoreConstants.JsonContentType, fixture.ContentType);
        }

        [Fact]
        public async Task UsesLowercasePackageAndPrereleaseVersionPrefix()
        {
            var fixture = new Fixture();
            var backup = CreateBackup();
            backup.NormalizedVersion = "1.0.0-BETA";

            await fixture.Service.SaveAsync(backup);

            Assert.Equal("package-backups/example.package/1.0.0-beta/package-delete-backup-10-20.json", Assert.Single(fixture.Files).Key);
            Assert.NotNull(await fixture.Service.GetAsync("example.package", "1.0.0-beta", 10, 20));
        }

        [Fact]
        public async Task SeparatesPackagesAndVersionsEvenWhenDeletionKeysMatch()
        {
            var fixture = new Fixture();
            var first = CreateBackup();
            var otherPackage = CreateBackup();
            otherPackage.PackageId = "Other.Package";
            var otherVersion = CreateBackup();
            otherVersion.NormalizedVersion = "2.0.0";
            await fixture.Service.SaveAsync(first);
            await fixture.Service.SaveAsync(otherPackage);
            await fixture.Service.SaveAsync(otherVersion);

            await fixture.Service.DeleteAsync(first.PackageId, first.NormalizedVersion, 10, 20);

            Assert.Equal(2, fixture.Files.Count);
            Assert.NotNull(await fixture.Service.GetAsync("Other.Package", "1.0.0", 10, 20));
            Assert.NotNull(await fixture.Service.GetAsync("Example.Package", "2.0.0", 10, 20));
        }

        [Theory]
        [InlineData("PackageId", "Other.Package")]
        [InlineData("NormalizedVersion", "2.0.0")]
        [InlineData("PackageDeleteKey", 11)]
        [InlineData("PackageKey", 21)]
        public async Task RejectsJsonWhoseIdentityDoesNotMatchItsBlobName(string property, object value)
        {
            var fixture = new Fixture();
            await fixture.Service.SaveAsync(CreateBackup());
            var file = Assert.Single(fixture.Files);
            var json = JObject.Parse(Encoding.UTF8.GetString(file.Value));
            json[property] = JToken.FromObject(value);
            fixture.Files[file.Key] = Encoding.UTF8.GetBytes(json.ToString());

            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Service.GetAsync("Example.Package", "1.0.0", 10, 20));
        }

        [Theory]
        [InlineData(null, "1.0.0", "packageId")]
        [InlineData("", "1.0.0", "packageId")]
        [InlineData("../packages", "1.0.0", "packageId")]
        [InlineData("Example/Package", "1.0.0", "packageId")]
        [InlineData("Example\\Package", "1.0.0", "packageId")]
        [InlineData("Example.Package", null, "normalizedVersion")]
        [InlineData("Example.Package", "1.0", "normalizedVersion")]
        [InlineData("Example.Package", "../1.0.0", "normalizedVersion")]
        public async Task RejectsInvalidStoragePrefixesBeforeAccessingStorage(string id, string version, string parameter)
        {
            var fixture = new Fixture();
            var backup = CreateBackup();
            backup.PackageId = id;
            backup.NormalizedVersion = version;

            var save = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.SaveAsync(backup));
            var get = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.GetAsync(id, version, 10, 20));
            var delete = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.DeleteAsync(id, version, 10, 20));

            Assert.Equal(parameter, save.ParamName);
            Assert.Equal(parameter, get.ParamName);
            Assert.Equal(parameter, delete.ParamName);
            fixture.Storage.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(0, 20, "packageDeleteKey")]
        [InlineData(10, 0, "packageKey")]
        public async Task RejectsNonPositiveKeysBeforeAccessingStorage(int deletionKey, int packageKey, string parameter)
        {
            var fixture = new Fixture();

            var get = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Service.GetAsync("Example.Package", "1.0.0", deletionKey, packageKey));
            var delete = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Service.DeleteAsync("Example.Package", "1.0.0", deletionKey, packageKey));

            Assert.Equal(parameter, get.ParamName);
            Assert.Equal(parameter, delete.ParamName);
            fixture.Storage.VerifyNoOtherCalls();
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
                Storage.Setup(s => s.DeleteFileAsync(CoreConstants.Folders.PackageBackupsFolderName, It.IsAny<string>()))
                    .Returns<string, string>((folder, name) =>
                    {
                        Files.Remove(folder + "/" + name);
                        return Task.CompletedTask;
                    });
                Service = new PackageDeleteBackupService(Storage.Object);
            }
        }
    }
}