using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using Lucene.Net.Documents;

namespace NuGetGallery
{
    public class PackageIndexEntity
    {
        internal static readonly char[] IdSeparators = new[] { '.', '-' };

        public Package Package { get; set; }

        public IEnumerable<int> CuratedFeedKeys { get; set; }

        public PackageIndexEntity() { }

        public PackageIndexEntity(Package package)
        {
            this.Package = package;
        }

        public Document ToDocument()
        {
            var document = new Document();

            // Note: Used to identify index records for updates
            document.Add(new Field("PackageRegistrationKey",
                    Package.PackageRegistrationKey.ToString(CultureInfo.InvariantCulture),
                    Field.Store.YES,
                    Field.Index.NOT_ANALYZED));

            document.Add(new Field("Key", Package.Key.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NOT_ANALYZED));

            if (CuratedFeedKeys != null)
            {
                foreach (var feedKey in CuratedFeedKeys)
                {
                    document.Add(new Field("CuratedFeedKey", feedKey.ToString(CultureInfo.InvariantCulture), Field.Store.NO, Field.Index.NOT_ANALYZED));
                }
            }

            var field = new Field("Id-Exact", Package.PackageRegistration.Id.ToLowerInvariant(), Field.Store.NO, Field.Index.NOT_ANALYZED);

            field.Boost = 2.5f;
            document.Add(field);

            // Store description so we can show them in search results
            field = new Field("Description", Package.Description, Field.Store.YES, Field.Index.ANALYZED);
            field.Boost = 0.1f;
            document.Add(field);

            // We store the Id/Title field in multiple ways, so that it's possible to match using multiple
            // styles of search
            // Note: no matter which way we store it, it will also be processed by the Analyzer later.

            // Style 1: As-Is Id, no tokenizing (so you can search using dot or dash-joined terms)
            // Boost this one
            field = new Field("Id", Package.PackageRegistration.Id, Field.Store.NO, Field.Index.ANALYZED);
            document.Add(field);

            // Style 2: dot+dash tokenized (so you can search using undotted terms)
            field = new Field("Id", SplitId(Package.PackageRegistration.Id), Field.Store.NO, Field.Index.ANALYZED);
            field.Boost = 0.8f;
            document.Add(field);

            // Style 3: camel-case tokenized (so you can search using parts of the camelCasedWord). 
            // De-boosted since matches are less likely to be meaningful
            field = new Field("Id", CamelSplitId(Package.PackageRegistration.Id), Field.Store.NO, Field.Index.ANALYZED);
            field.Boost = 0.25f;
            document.Add(field);

            // If an element does not have a Title, fall back to Id, same as the website.
            var workingTitle = String.IsNullOrEmpty(Package.Title)
                                   ? Package.PackageRegistration.Id
                                   : Package.Title;

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

            if (!String.IsNullOrEmpty(Package.Tags))
            {
                // Store tags so we can show them in search results
                field = new Field("Tags", Package.Tags, Field.Store.YES, Field.Index.ANALYZED);
                field.Boost = 0.8f;
                document.Add(field);
            }

            document.Add(new Field("Authors", Package.FlattenedAuthors.ToStringSafe(), Field.Store.YES, Field.Index.ANALYZED));

            // Fields for storing data to avoid hitting SQL while doing searches
            if (!String.IsNullOrEmpty(Package.IconUrl))
            {
                document.Add(new Field("IconUrl", Package.IconUrl, Field.Store.YES, Field.Index.NO));
            }

            if (Package.PackageRegistration.Owners.AnySafe())
            {
                string flattenedOwners = String.Join(";", Package.PackageRegistration.Owners.Select(o => o.Username));
                document.Add(new Field("Owners", flattenedOwners, Field.Store.NO, Field.Index.ANALYZED));
                document.Add(new Field("FlattenedOwners", flattenedOwners, Field.Store.YES, Field.Index.NO));
            }

            document.Add(new Field("Copyright", Package.Copyright.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("Created", Package.Created.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("FlattenedDependencies", Package.FlattenedDependencies.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("Hash", Package.Hash.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("HashAlgorithm", Package.HashAlgorithm.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("Id-Original", Package.PackageRegistration.Id, Field.Store.YES, Field.Index.NO));
            document.Add(new Field("LastUpdated", Package.LastUpdated.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NO));
            if (Package.LastEdited != null)
            {
                document.Add(new Field("LastEdited", Package.LastEdited.Value.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NO));
            }

            document.Add(new Field("Language", Package.Language.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("LicenseUrl", Package.LicenseUrl.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("MinClientVersion", Package.MinClientVersion.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("Version", Package.Version.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            
            string normalizedVersion = String.IsNullOrEmpty(Package.NormalizedVersion) ? 
                SemanticVersionExtensions.Normalize(Package.Version) : 
                Package.NormalizedVersion;
            document.Add(new Field("NormalizedVersion", normalizedVersion.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            
            document.Add(new Field("VersionDownloadCount", Package.DownloadCount.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("PackageFileSize", Package.PackageFileSize.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("ProjectUrl", Package.ProjectUrl.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("Published", Package.Published.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("ReleaseNotes", Package.ReleaseNotes.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("RequiresLicenseAcceptance", Package.RequiresLicenseAcceptance.ToString(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("Summary", Package.Summary.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("LicenseNames", Package.LicenseNames.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("LicenseReportUrl", Package.LicenseReportUrl.ToStringSafe(), Field.Store.YES, Field.Index.NO));
            document.Add(new Field("HideLicenseReport", Package.HideLicenseReport.ToStringSafe(), Field.Store.YES, Field.Index.NO));

            if (Package.SupportedFrameworks.AnySafe())
            {
                string joinedFrameworks = string.Join(";", Package.SupportedFrameworks.Select(f => f.FrameworkName));
                document.Add(new Field("JoinedSupportedFrameworks", joinedFrameworks, Field.Store.YES,
                                       Field.Index.NO));
            }

            // Fields meant for filtering, also storing data to avoid hitting SQL while doing searches
            document.Add(new Field("IsLatest", Package.IsLatest.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
            document.Add(new Field("IsLatestStable", Package.IsLatestStable.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));

            // Fields meant for filtering, sorting
            document.Add(new Field("PublishedDate", Package.Published.Ticks.ToString(CultureInfo.InvariantCulture), Field.Store.NO, Field.Index.NOT_ANALYZED));
            document.Add(new Field("EditedDate", (Package.LastEdited ?? Package.Published).Ticks.ToString(CultureInfo.InvariantCulture), Field.Store.NO, Field.Index.NOT_ANALYZED));
            document.Add(
                 new Field("DownloadCount", Package.PackageRegistration.DownloadCount.ToString(CultureInfo.InvariantCulture), Field.Store.YES, Field.Index.NOT_ANALYZED));

            string displayName = String.IsNullOrEmpty(Package.Title) ? Package.PackageRegistration.Id : Package.Title;
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
                    tokenEnd = i;
                }
            }

            // Finally return the term in entirety
            yield return term;
        }
    }
}