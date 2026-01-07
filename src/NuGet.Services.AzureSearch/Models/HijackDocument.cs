// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Search.Documents.Indexes;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// The different models for reading from and writing to the hijack index.
    /// </summary>
    public static class HijackDocument
    {
        /// <summary>
        /// All fields available in the hijack index. Used for reading the index and updating a document when
        /// <see cref="HijackDocumentChanges.UpdateMetadata"/> is <c>true</c>.
        /// </summary>
        public class Full : BaseMetadataDocument, ILatest, IBaseMetadataDocument
        {
            [SimpleField(IsFilterable = true)]
            public bool? Listed { get; set; }

            public bool? IsLatestStableSemVer1 { get; set; }
            public bool? IsLatestSemVer1 { get; set; }
            public bool? IsLatestStableSemVer2 { get; set; }
            public bool? IsLatestSemVer2 { get; set; }
        }

        /// <summary>
        /// Used for updating a document when <see cref="HijackDocumentChanges.UpdateMetadata"/> is <c>false</c>
        /// and <see cref="HijackDocumentChanges.Delete"/> is <c>false</c>.
        /// </summary>
        public class Latest : CommittedDocument, ILatest
        {
            public bool? IsLatestStableSemVer1 { get; set; }
            public bool? IsLatestSemVer1 { get; set; }
            public bool? IsLatestStableSemVer2 { get; set; }
            public bool? IsLatestSemVer2 { get; set; }
        }

        /// <summary>
        /// Allows index updating code to update the latest booleans.
        /// </summary>
        public interface ILatest : ICommittedDocument
        {
            bool? IsLatestStableSemVer1 { get; set; }
            bool? IsLatestSemVer1 { get; set; }
            bool? IsLatestStableSemVer2 { get; set; }
            bool? IsLatestSemVer2 { get; set; }
        }
    }
}
