// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                Assert.Equal("SHA1024", p.HashAlgorithm);
                Assert.Equal("NewHash", p.Hash);
                Assert.Equal(1, p.PackageFileSize);

                Assert.Equal(1, p.PackageEdits.Count); // It has to be deleted from the ObjectContext anyway so no point trying to delete it as part of ApplyEdit.
            }
        }
    }
}
