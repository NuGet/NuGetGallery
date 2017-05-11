// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using NuGet.Versioning;

namespace NuGet.Indexing
{
    public class VersionsHandler : IIndexReaderProcessorHandler
    {
        private readonly Downloads _downloads;
        private IDictionary<string, List<RegistrationEntry>> _registrations;

        public VersionsHandler(Downloads downloads)
        {
            if (downloads == null)
            {
                throw new ArgumentNullException(nameof(downloads));
            }

            _downloads = downloads;
        }

        public VersionResult[] Result { get; private set; }

        public bool SkipDeletes => false;

        public void Begin(IndexReader indexReader)
        {
            _registrations = new Dictionary<string, List<RegistrationEntry>>();
        }

        public void End(IndexReader indexReader)
        {
            Result = new VersionResult[indexReader.MaxDoc];
            CreateResults();
            _registrations.Clear();
        }

        public void Process(IndexReader indexReader,
            string readerName,
            int perSegmentDocumentNumber,
            int perIndexDocumentNumber,
            Document document,
            string id,
            NuGetVersion version)
        {
            // main index docid
            if (id == null || version == null)
            {
                return;
            }

            List<RegistrationEntry> versions;
            if (!_registrations.TryGetValue(id, out versions))
            {
                versions = new List<RegistrationEntry>();
                _registrations.Add(id, versions);
            }

            var entry = new RegistrationEntry(perIndexDocumentNumber, version, GetListed(document));

            versions.Add(entry);
        }

        private void CreateResults()
        {
            foreach (var registration in _registrations)
            {
                var downloadsByVersion = _downloads[registration.Key];

                VersionResult versionResult = CreateVersionResult(registration.Key, registration.Value, downloadsByVersion);

                foreach (var packageVersion in registration.Value)
                {
                    Result[packageVersion.DocumentId] = versionResult;
                }
            }
        }

        private VersionResult CreateVersionResult(string id, List<RegistrationEntry> registrationEntries, DownloadsByVersion downloadsByVersion)
        {
            VersionResult result = new VersionResult();

            foreach (var registrationEntry in registrationEntries.OrderBy(r => r.Version))
            {
                string fullVersion = String.Intern(registrationEntry.Version.ToFullString());
                string normalizedVersion = String.Intern(registrationEntry.Version.ToNormalizedString());

                int downloads = 0;
                if (downloadsByVersion != null)
                {
                    downloads = downloadsByVersion[normalizedVersion];
                }

                result.AllVersionDetails.Add(new VersionDetail
                {
                    NormalizedVersion = normalizedVersion,
                    FullVersion = fullVersion,
                    Downloads = downloads,
                    IsStable = !registrationEntry.Version.IsPrerelease,
                    IsListed = registrationEntry.IsListed,
                    IsSemVer2 = registrationEntry.Version.IsSemVer2
                });
            }

            return result;
        }

        private static bool GetListed(Document document)
        {
            string listed = document.Get("Listed");
            return (listed == null) ? false : listed.Equals("true", StringComparison.InvariantCultureIgnoreCase);
        }

        private class RegistrationEntry
        {
            public RegistrationEntry(int docId, NuGetVersion version, bool isListed)
            {
                DocumentId = docId;
                Version = version;
                IsListed = isListed;
            }

            public int DocumentId { get; }
            public NuGetVersion Version { get; }
            public bool IsListed { get; }
        }
    }
}
