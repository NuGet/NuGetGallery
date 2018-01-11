﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Xunit;

namespace NuGetGallery
{
    public class PackageFacts
    {
        public class PositiveScenarios
        {
            [Fact]
            public void ApplyPackageEditUpdatesPackage()
            {
                var p = new Package
                {
                    Title = "OldTitle",
                    Description = "OldDescription",
                    Summary = "OldSummary",
                    Tags = "OldTags",
                    FlattenedAuthors = "OldAuthors",
                    Copyright = "OldCopyright",
                    ReleaseNotes = "OldReleaseNotes",
                    RepositoryUrl = "OldRepositoryUrl",
                    Hash = "OldHash",
                    HashAlgorithm = "SHA512",
                    PackageFileSize = 0,
                };

                var pe = new PackageEdit
                {
                    Title = "NewTitle",
                    Description = "NewDescription",
                    Summary = "NewSummary",
                    Tags = "NewTags",
                    Authors = "NewAuthors",
                    Copyright = "NewCopyright",
                    ReleaseNotes = "NewReleaseNotes",
                    RepositoryUrl = "NewRepositoryUrl",
                };

                p.PackageEdits.Add(pe);
                p.ApplyEdit(pe, "SHA1024", "NewHash", 1);

                Assert.Equal(1, p.PackageHistories.Count);
                var h = p.PackageHistories.ElementAt(0);
                Assert.Equal("OldTitle", h.Title);
                Assert.Equal("OldDescription", h.Description);
                Assert.Equal("OldSummary", h.Summary);
                Assert.Equal("OldTags", h.Tags);
                Assert.Equal("OldAuthors", h.Authors);
                Assert.Equal("OldCopyright", h.Copyright);
                Assert.Equal("OldReleaseNotes", h.ReleaseNotes);
                Assert.Equal("OldRepositoryUrl", h.RepositoryUrl);
                Assert.Equal("SHA512", h.HashAlgorithm);
                Assert.Equal("OldHash", h.Hash);
                Assert.Equal(0, h.PackageFileSize);

                Assert.Equal("NewTitle", p.Title);
                Assert.Equal("NewDescription", p.Description);
                Assert.Equal("NewSummary", p.Summary);
                Assert.Equal("NewTags", p.Tags);
                Assert.Equal("NewAuthors", p.FlattenedAuthors);
                Assert.Equal("NewCopyright", p.Copyright);
                Assert.Equal("NewReleaseNotes", p.ReleaseNotes);
                Assert.Equal("NewRepositoryUrl", p.RepositoryUrl);
                Assert.Equal("SHA1024", p.HashAlgorithm);
                Assert.Equal("NewHash", p.Hash);
                Assert.Equal(1, p.PackageFileSize);

                Assert.Equal(1, p.PackageEdits.Count); // It has to be deleted from the ObjectContext anyway so no point trying to delete it as part of ApplyEdit.
            }
        }

        [Fact]
        public void HasReadMe_InternalColumnMapped()
        {
            // Arrange & Act
            var attributes = typeof(Package).GetProperty("HasReadMeInternal").GetCustomAttributes(typeof(ColumnAttribute), true) as ColumnAttribute[];

            // Assert
            Assert.Equal(1, attributes.Length);
            Assert.Equal("HasReadMe", attributes[0].Name);
        }

        [Fact]
        public void HasReadMe_WhenInternalIsNullReturnsFalse()
        {
            // Arrange & Act
            var package = new Package { HasReadMeInternal = null };
            
            // Assert
            Assert.False(package.HasReadMe);
        }

        [Fact]
        public void HasReadMe_WhenInternalIsTrueReturnsTrue()
        {
            // Arrange & Act
            var package = new Package { HasReadMeInternal = true };

            // Assert
            Assert.True(package.HasReadMe);
        }

        [Fact]
        public void HasReadMe_WhenSetToFalseSavesInternalAsNull()
        {
            // Arrange & Act
            var package = new Package { HasReadMeInternal = true };
            package.HasReadMe = false;

            // Assert
            Assert.Null(package.HasReadMeInternal);
        }

        [Fact]
        public void HasReadMe_WhenSetToTrueSavesInternalAsTrue()
        {
            // Arrange & Act
            var package = new Package { HasReadMeInternal = null };
            package.HasReadMe = true;

            // Assert
            Assert.True(package.HasReadMeInternal);
        }
    }
}
