// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using CatalogPageCommit = NuGet.Services.Metadata.Catalog.CatalogCommit;

namespace NuGet.Jobs.Catalog2Registration
{
    /// <summary>
    /// A dedicated reader for ID-level (package registration scoped) catalog leaves, such as
    /// <c>PackageSponsorshipDetails</c>. These leaves are version-less, so they are intentionally excluded from the
    /// shared version-level pipeline (which requires a version to build a <see cref="CatalogCommitItem"/>). This
    /// collector walks the same catalog but reads the raw JSON directly, never building a
    /// <see cref="CatalogCommitItem"/>, and applies the ID-level attributes to the registration index via
    /// <see cref="IRegistrationLevelUpdater"/>. It has its own cursor so it can progress independently of the
    /// version-level collector.
    /// </summary>
    public class RegistrationLevelCollector : CollectorBase, IRegistrationLevelCollector
    {
        private readonly IRegistrationLevelUpdater _updater;

        public RegistrationLevelCollector(
            IRegistrationLevelUpdater updater,
            ITelemetryService telemetryService,
            Func<HttpMessageHandler> handlerFunc,
            IOptionsSnapshot<Catalog2RegistrationConfiguration> options)
            : base(
                  new Uri(options.Value.Source),
                  telemetryService,
                  handlerFunc,
                  options.Value.HttpClientTimeout)
        {
            _updater = updater ?? throw new ArgumentNullException(nameof(updater));
        }

        protected override async Task<bool> FetchAsync(
            CollectorHttpClient client,
            ReadWriteCursor front,
            ReadCursor back,
            CancellationToken cancellationToken)
        {
            var root = await client.GetJObjectAsync(Index, cancellationToken);

            var pages = root["items"]
                .Select(item => CatalogPageCommit.Create((JObject)item));
            var pagesInRange = CommitCollector
                .GetCommitsInRange(pages, front.Value, back.Value)
                .OrderBy(page => page.CommitTimeStamp)
                .ToList();

            foreach (var pageCommit in pagesInRange)
            {
                var page = await client.GetJObjectAsync(pageCommit.Uri, cancellationToken);

                page.TryGetValue("@context", out var context);

                // Select only the ID-level (version-less) items that fall within the cursor window. These items only
                // carry a minimal @type + nuget:id on the page; the actual attribute values live in the leaf blob.
                var items = page["items"]
                    .Cast<JObject>()
                    .Where(item => CatalogCommitItem.IsRegistrationLevelItem((JObject)context, item))
                    .Select(item => new RegistrationLevelPageItem(
                        commitTimeStamp: item.Value<DateTimeOffset>("commitTimeStamp"),
                        leafUrl: item.Value<string>("@id"),
                        id: item.Value<string>("nuget:id")))
                    .Where(item => item.CommitTimeStamp.UtcDateTime > front.Value
                        && item.CommitTimeStamp.UtcDateTime <= back.Value)
                    .ToList();

                // Process in commit order and advance the cursor per distinct commit timestamp so the job is resumable.
                // Because later commits are processed last, the newest ID-level state wins for a given package.
                foreach (var commitGroup in items
                    .GroupBy(item => item.CommitTimeStamp.UtcDateTime)
                    .OrderBy(group => group.Key))
                {
                    // Within a single commit, the last item for a given package ID wins.
                    var latestPerId = commitGroup
                        .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.Last());

                    foreach (var pageItem in latestPerId)
                    {
                        var leaf = await client.GetJObjectAsync(new Uri(pageItem.LeafUrl), cancellationToken);
                        var sponsorshipUrls = ReadSponsorshipUrls(leaf);
                        var id = leaf.Value<string>("id") ?? pageItem.Id;

                        await _updater.UpdateAsync(id, sponsorshipUrls, pageItem.CommitTimeStamp);
                    }

                    front.Value = commitGroup.Key;
                    await front.SaveAsync(cancellationToken);
                }
            }

            return true;
        }

        private static IReadOnlyList<string> ReadSponsorshipUrls(JObject leaf)
        {
            var token = leaf["sponsorshipUrls"];
            if (token == null)
            {
                return Array.Empty<string>();
            }

            if (token.Type == JTokenType.Array)
            {
                return token
                    .Select(value => value.Value<string>())
                    .Where(value => !string.IsNullOrEmpty(value))
                    .ToList();
            }

            // A single-valued @set may be framed as a scalar rather than an array.
            var single = token.Value<string>();
            return string.IsNullOrEmpty(single) ? Array.Empty<string>() : new List<string> { single };
        }

        private sealed class RegistrationLevelPageItem
        {
            public RegistrationLevelPageItem(DateTimeOffset commitTimeStamp, string leafUrl, string id)
            {
                CommitTimeStamp = commitTimeStamp;
                LeafUrl = leafUrl;
                Id = id;
            }

            public DateTimeOffset CommitTimeStamp { get; }
            public string LeafUrl { get; }
            public string Id { get; }
        }
    }
}
