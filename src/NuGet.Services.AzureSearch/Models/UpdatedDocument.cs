// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch
{
    public abstract class UpdatedDocument : KeyedDocument
    {
        private readonly CurrentTimestamp _lastUpdatedDocument = new CurrentTimestamp();

        public DateTimeOffset? LastUpdatedDocument
        {
            get => _lastUpdatedDocument.Value;
            set => _lastUpdatedDocument.Value = value;
        }

        public string LastDocumentType { get; set; }

        public bool? LastUpdatedFromCatalog { get; set; }

        public void SetLastUpdatedDocumentOnNextRead()
        {
            _lastUpdatedDocument.SetOnNextRead();
        }
    }
}