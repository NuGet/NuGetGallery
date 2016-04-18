// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Index;
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Ng
{
    public class LuceneCursor : ReadWriteCursor
    {
        private readonly IndexWriter _indexWriter;
        private readonly DateTime _defaultValue;

        public LuceneCursor(IndexWriter indexWriter, DateTime defaultValue)
        {
            _indexWriter = indexWriter;
            _defaultValue = defaultValue;
        }

        public override Task Save(CancellationToken cancellationToken)
        {
            //  no-op because we will do the Save in the Lucene.Commit

            return Task.FromResult(true);
        }

        public override Task Load(CancellationToken cancellationToken)
        {
            IDictionary<string, string> commitUserData;
            using (var reader = _indexWriter.GetReader())
            {
                commitUserData = reader.CommitUserData;
            }

            string value;
            if (commitUserData != null && commitUserData.TryGetValue("commitTimeStamp", out value))
            {
                Value = DateTime.ParseExact(value, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            else
            {
                Value = _defaultValue;
            }

            Trace.TraceInformation("LuceneCursor.Load: {0}", this);
            return Task.FromResult(true);
        }
    }
}
