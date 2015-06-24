// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Documents;
using Lucene.Net.Index;
using NuGet.Versioning;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Indexing
{
    public class VersionsHandler : IIndexReaderProcessorHandler
    {
        IDictionary<string, IDictionary<string, int>> _downloads;
        IDictionary<string, List<RegistrationEntry>> _registrations;

        public VersionsHandler(IDictionary<string, IDictionary<string, int>> downloads)
        {
            _downloads = downloads;
        }

        public VersionResult[] Result { get; private set; }

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

        public void Process(IndexReader indexReader, string readerName, int n, Document document, string id, NuGetVersion version)
        {
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

            versions.Add(new RegistrationEntry { DocumentId = n, Version = version });
        }

        void CreateResults()
        {
            foreach (var registration in _registrations)
            {
                IDictionary<string, int> downloadsByVersion = null;
                _downloads.TryGetValue(registration.Key, out downloadsByVersion);

                VersionResult versionResult = CreateVersionResult(registration.Key, registration.Value, downloadsByVersion);

                foreach (var packageVersion in registration.Value)
                {
                    Result[packageVersion.DocumentId] = versionResult;
                }
            }
        }

        VersionResult CreateVersionResult(string id, List<RegistrationEntry> registrationEntries, IDictionary<string, int> downloadsByVersion)
        {
            VersionResult result = new VersionResult();

            foreach (var registrationEntry in registrationEntries.OrderBy(r => r.Version))
            {
                string versionStr = registrationEntry.Version.ToNormalizedString();

                int downloads = 0;
                if (downloadsByVersion != null)
                {
                    downloadsByVersion.TryGetValue(versionStr, out downloads);
                }

                result.VersionDetails.Add(new VersionResult.VersionDetail
                {
                    Version = versionStr,
                    Downloads = downloads,
                    IsStable = !registrationEntry.Version.IsPrerelease
                });
            }

            return result;
        }

        class RegistrationEntry
        {
            public int DocumentId { get; set; }
            public NuGetVersion Version { get; set; }
        }

        public class VersionResult
        {
            public VersionResult()
            {
                VersionDetails = new List<VersionDetail>();
            }

            public List<VersionDetail> VersionDetails { get; private set; }

            public IEnumerable<string> Versions { get { return VersionDetails.Select(v => v.Version); } }
            public IEnumerable<string> StableVersions { get { return StableVersionDetails.Select(v => v.Version); } }
            public IEnumerable<VersionDetail> StableVersionDetails { get { return VersionDetails.Where(v => v.IsStable); } }

            public class VersionDetail
            {
                public string Version { get; set; }
                public int Downloads { get; set; }
                public bool IsStable { get; set; }
            }
        }
    }
}
