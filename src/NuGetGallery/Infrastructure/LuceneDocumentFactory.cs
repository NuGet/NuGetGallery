// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lucene.Net.Documents;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class LuceneDocumentFactory : ILuceneDocumentFactory
    {
        internal static readonly char[] IdSeparators = new[] { '.', '-' };

        private readonly IIconUrlProvider _iconUrlProvider;

        public LuceneDocumentFactory(IIconUrlProvider iconUrlProvider)
        {
            _iconUrlProvider = iconUrlProvider ?? throw new ArgumentNullException(nameof(iconUrlProvider));
        }

        public Document Create(Package package)
        {
            var document = new Document();

            // Note: Used to identify index records for updates
            document.Add(new Field("PackageRegistrationKey",
                    package.PackageRegistrationKey.ToString(CultureInfo.InvariantCulture),
                    Field.Store.YES,
                    Field.Index.NOT_ANALYZED));

            document.Add(new Field("Key", package.Key.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NOT_ANALYZED));

            var field = new Field("Id-Exact", package.PackageRegistration.Id.ToLowerInvariant(), Field.Store.NO, Field.Index.NOT_ANALYZED);

            field.Boost = 2.5f;
            document.Add(field);

            // Store description so we can show them in search results
            field = new Field("Description", package.Description, Field.Store.YES, Field.Index.ANALYZED);
            field.Boost = 0.1f;
            document.Add(field);

            // We store the Id/Title field in multiple ways, so that it's possible to match using multiple
            // styles of search
            // Note: no matter which way we store it, it will also be processed by the Analyzer later.

            // Style 1: As-Is Id, no tokenizing (so you can search using dot or dash-joined terms)
            // Boost this one
            field = new Field("Id", package.PackageRegistration.Id, Field.Store.NO, Field.Index.ANALYZED);
            document.Add(field);

            // Style 2: dot+dash tokenized (so you can search using undotted terms)
            field = new Field("Id", SplitId(package.PackageRegistration.Id), Field.Store.NO, Field.Index.ANALYZED);
            field.Boost = 0.8f;
            document.Add(field);

            // Style 3: camel-case tokenized (so you can search using parts of the camelCasedWord). 
            // De-boosted since matches are less likely to be meaningful
            field = new Field("Id", CamelSplitId(package.PackageRegistration.Id), Field.Store.NO, Field.Index.ANALYZED);
            field.Boost = 0.25f;
            document.Add(field);

            // If an element does not have a Title, fall back to Id, same as the website.
            var workingTitle = String.IsNullOrEmpty(package.Title)
                                   ? package.PackageRegistration.Id
                                   : package.Title;

            // As-Is (stored for search results)
            field = new Field("Title", workingTitle, Field.Store.YES, Field.Index.ANALYZED);
            field.Boost = 0.9f;
            document.Add(field);

            // no need to store dot+dash tokenized - we'll handle this in the analyzer
            field = new Field("Title", SplitId(workingTitle), Field.Store.NO, Field.Index.ANALYZED);
            field.Boost = 0.8f;
            document.Add(field);

            // camel-case tokenized
            field = new Field("Title", CamelSplitId(workingTitle), Field.Store.NO, Field.Index.ANALYZED);
            field.Boost = 0.5f;
            document.Add(field);

            if (!String.IsNullOrEmpty(package.Tags))
            {
                // Store tags so we can show them in search results
                field = new Field("Tags", package.Tags, Field.Store.YES, Field.Index.ANALYZED);
                field.Boost = 0.8f;
                document.Add(field);
            }

            document.Add(new Field("Authors", package.FlattenedAuthors.ToStringSafe(), Field.Store.YES, Field.Index.ANALYZED));

            // Fields for storing data to avoid hitting SQL while doing searches
            var iconUrl = _iconUrlProvider.GetIconUrlString(package);
            if (iconUrl != null)
            {
                document.Add(new Field("IconUrl", iconUrl, Field.Store.YES, Field.Index.NO));
            }

            if (package.PackageRegistration.Owners.AnySafe())
            {
                string flattenedOwners = String.Join(";", package.PackageRegistration.Owners.Select(o => o.Username));
                document.Add(new Field("Owners", flattenedOwners, Field.Store.NO, Field.Index.ANALYZED));
                document.Add(new Field("FlattenedOwners", flattenedOwners, Field.Store.YES, Field.Index.NO));
            }

            document.Add(new Field("Copyright", package.Copyright.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("Created", package.Created.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("FlattenedDependencies", package.FlattenedDependencies.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("FlattenedPackageTypes", package.FlattenedPackageTypes.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("Hash", package.Hash.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("HashAlgorithm", package.HashAlgorithm.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("Id-Original", package.PackageRegistration.Id, Field.Store.YES, Field.Index.NO));
            document.Add(new Field("IsVerified-Original", package.PackageRegistration.IsVerified.ToString(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("LastUpdated", package.LastUpdated.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NO));
            if (package.LastEdited != null)
            {
                document.Add(new Field("LastEdited", package.LastEdited.Value.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NO));
            }

            document.Add(new Field("Language", package.Language.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("LicenseUrl", package.LicenseUrl.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("MinClientVersion", package.MinClientVersion.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("Version", package.Version.ToStringSafe(), Field.Store.YES, Field.Index.NO));

            string normalizedVersion = String.IsNullOrEmpty(package.NormalizedVersion) ?
                NuGetVersionFormatter.Normalize(package.Version) :
                package.NormalizedVersion;
            document.Add(new Field("NormalizedVersion", normalizedVersion.ToStringSafe(), Field.Store.YES, Field.Index.NO));

            document.Add(new Field("VersionDownloadCount", package.DownloadCount.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("PackageFileSize", package.PackageFileSize.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("ProjectUrl", package.ProjectUrl.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("Published", package.Published.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("ReleaseNotes", package.ReleaseNotes.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("RequiresLicenseAcceptance", package.RequiresLicenseAcceptance.ToString(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("Summary", package.Summary.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("LicenseNames", package.LicenseNames.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("LicenseReportUrl", package.LicenseReportUrl.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("HideLicenseReport", package.HideLicenseReport.ToStringSafe(), Field.Store.YES, Field.Index.NO));

            if (package.SupportedFrameworks.AnySafe())
            {
                string joinedFrameworks = string.Join(";", package.SupportedFrameworks.Select(f => f.FrameworkName));
                document.Add(new Field("JoinedSupportedFrameworks", joinedFrameworks, Field.Store.YES,
                                       Field.Index.NO));
            }

            // Fields meant for filtering, also storing data to avoid hitting SQL while doing searches
            document.Add(new Field("IsLatest", package.IsLatest.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
            document.Add(new Field("IsLatestStable", package.IsLatestStable.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
            document.Add(new Field("IsLatestSemVer2", package.IsLatestSemVer2.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
            document.Add(new Field("IsLatestStableSemVer2", package.IsLatestStableSemVer2.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));

            // Fields meant for filtering, sorting
            document.Add(new Field("PublishedDate", package.Published.Ticks.ToString(CultureInfo.InvariantCulture), Field.Store.NO, Field.Index.NOT_ANALYZED));
            document.Add(new Field("EditedDate", (package.LastEdited ?? package.Published).Ticks.ToString(CultureInfo.InvariantCulture), Field.Store.NO, Field.Index.NOT_ANALYZED));
            document.Add(
                 new Field("DownloadCount", package.PackageRegistration.DownloadCount.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NOT_ANALYZED));

            string displayName = String.IsNullOrEmpty(package.Title) ? package.PackageRegistration.Id : package.Title;
            document.Add(new Field("DisplayName", displayName.ToLower(CultureInfo.CurrentCulture), Field.Store.NO, Field.Index.NOT_ANALYZED));

            return document;
        }

        // Split up the id by - and . then join it back into one string (tokens in the same order).
        internal static string SplitId(string term)
        {
            var split = term.Split(IdSeparators, StringSplitOptions.RemoveEmptyEntries);
            return split.Any() ? string.Join(" ", split) : "";
        }

        internal static string CamelSplitId(string term)
        {
            var split = term.Split(IdSeparators, StringSplitOptions.RemoveEmptyEntries);
            var tokenized = split.SelectMany(CamelCaseTokenize);

            return tokenized.Any() ? string.Join(" ", tokenized) : "";
        }

        internal static IEnumerable<string> TokenizeId(string term)
        {
            // First tokenize the result by id-separators. For e.g. tokenize SignalR.EventStream as SignalR and EventStream
            var tokens = term.Split(IdSeparators, StringSplitOptions.RemoveEmptyEntries);

            // For each token, further attempt to tokenize camelcase values. e.g. .EventStream -> Event, Stream. 
            var result = tokens.Concat(new[] { term })
                .Concat(tokens.SelectMany(CamelCaseTokenize))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return result;
        }

        private static IEnumerable<string> CamelCaseTokenize(string term)
        {
            const int minTokenLength = 3;
            if (term.Length < minTokenLength)
            {
                yield break;
            }

            int tokenCount = 0;
            int tokenEnd = term.Length;
            for (int i = term.Length - 1; i > 0; i--)
            {
                // If the remainder is fewer than 2 chars or we have a token that is at least 2 chars long, tokenize it.
                if (i < minTokenLength || (Char.IsUpper(term[i]) && (tokenEnd - i >= minTokenLength)))
                {
                    if (i < minTokenLength)
                    {
                        // If the remainder is smaller than 2 chars, just return the entire string
                        i = 0;
                    }

                    yield return term.Substring(i, tokenEnd - i);
                    tokenCount++;
                    tokenEnd = i;
                }
            }

            // Finally return the term in entirety, if not already returned
            if (tokenCount != 1)
            {
                yield return term;
            }
        }
    }
}