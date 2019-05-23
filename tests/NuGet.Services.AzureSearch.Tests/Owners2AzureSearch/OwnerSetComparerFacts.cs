// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.AzureSearch.Support;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Owners2AzureSearch
{
    public class OwnerSetComparerFacts
    {
        public class Compare : Facts
        {
            public Compare(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public void FindsAddedPackageIds()
            {
                var oldData = Data("NuGet.Core: NuGet, Microsoft");
                var newData = Data("NuGet.Core: NuGet, Microsoft",
                                   "NuGet.Versioning: NuGet, Microsoft");

                var changes = Target.Compare(oldData, newData);

                var pair = Assert.Single(changes);
                Assert.Equal("NuGet.Versioning", pair.Key);
                Assert.Equal(new[] { "Microsoft", "NuGet" }, pair.Value);
            }


            [Fact]
            public void FindsRemovedPackageIds()
            {
                var oldData = Data("NuGet.Core: NuGet, Microsoft",
                                   "NuGet.Versioning: NuGet, Microsoft");
                var newData = Data("NuGet.Core: NuGet, Microsoft");

                var changes = Target.Compare(oldData, newData);

                var pair = Assert.Single(changes);
                Assert.Equal("NuGet.Versioning", pair.Key);
                Assert.Empty(pair.Value);
            }

            [Fact]
            public void FindsAddedOwner()
            {
                var oldData = Data("NuGet.Core: NuGet");
                var newData = Data("NuGet.Core: NuGet, Microsoft");

                var changes = Target.Compare(oldData, newData);

                var pair = Assert.Single(changes);
                Assert.Equal("NuGet.Core", pair.Key);
                Assert.Equal(new[] { "Microsoft", "NuGet" }, pair.Value);
            }

            [Fact]
            public void FindsRemovedOwner()
            {
                var oldData = Data("NuGet.Core: NuGet, Microsoft");
                var newData = Data("NuGet.Core: NuGet");

                var changes = Target.Compare(oldData, newData);

                var pair = Assert.Single(changes);
                Assert.Equal("NuGet.Core", pair.Key);
                Assert.Equal(new[] { "NuGet" }, pair.Value);
            }

            [Fact]
            public void FindsOwnerWithChangedCase()
            {
                var oldData = Data("NuGet.Core: NuGet, Microsoft");
                var newData = Data("NuGet.Core: NuGet, microsoft");

                var changes = Target.Compare(oldData, newData);

                var pair = Assert.Single(changes);
                Assert.Equal("NuGet.Core", pair.Key);
                Assert.Equal(new[] { "microsoft", "NuGet" }, pair.Value);
            }

            [Fact]
            public void FindsManyChangesAtOnce()
            {
                var oldData = Data("NuGet.Core: NuGet, Microsoft",
                                   "NuGet.Frameworks: NuGet",
                                   "NuGet.Protocol: NuGet");
                var newData = Data("NuGet.Core: NuGet, microsoft",
                                   "NuGet.Versioning: NuGet",
                                   "NuGet.Protocol: NuGet");

                var changes = Target.Compare(oldData, newData);

                Assert.Equal(3, changes.Count);
                Assert.Equal(new[] { "NuGet.Core", "NuGet.Frameworks", "NuGet.Versioning" }, changes.Keys.ToArray());
                Assert.Equal(new[] { "microsoft", "NuGet" }, changes["NuGet.Core"]);
                Assert.Empty(changes["NuGet.Frameworks"]);
                Assert.Equal(new[] { "NuGet" }, changes["NuGet.Versioning"]);
            }

            [Fact]
            public void FindsNoChanges()
            {
                var oldData = Data("NuGet.Core: NuGet, Microsoft",
                                   "NuGet.Versioning: NuGet, Microsoft");
                var newData = Data("NuGet.Core: NuGet, Microsoft",
                                   "NuGet.Versioning: NuGet, Microsoft");

                var changes = Target.Compare(oldData, newData);

                Assert.Empty(changes);
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                Logger = output.GetLogger<OwnerSetComparer>();

                Target = new OwnerSetComparer(Logger);
            }

            public RecordingLogger<OwnerSetComparer> Logger { get; }
            public OwnerSetComparer Target { get; }

            /// <summary>
            /// A helper to turn lines formatted like this "PackageId: OwnerA, OwnerB" into package ID to owners
            /// dictionary.
            /// </summary>
            public SortedDictionary<string, SortedSet<string>> Data(params string[] lines)
            {
                var builder = new PackageIdToOwnersBuilder(Logger);
                foreach (var line in lines)
                {
                    var pieces = line.Split(new[] { ':' }, 2);
                    var id = pieces[0].Trim();
                    var usernames = pieces[1]
                        .Split(',')
                        .Select(x => x.Trim())
                        .Where(x => x.Length > 0)
                        .ToList();

                    builder.Add(id, usernames);
                }

                return builder.GetResult();
            }
        }
    }
}
