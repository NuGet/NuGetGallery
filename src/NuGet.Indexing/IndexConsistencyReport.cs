// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;

namespace NuGet.Indexing
{
    public class IndexConsistencyReport
    {
        public int DocumentsInIndex { get; private set; }
        public int DocumentsInDatabase { get; private set; }
        public int Drift { get { return DocumentsInDatabase - DocumentsInIndex; } }

        public IndexConsistencyReport(int documentsInIndex, int documentsInDatabase)
        {
            DocumentsInIndex = documentsInIndex;
            DocumentsInDatabase = documentsInDatabase;
        }

        public string ToJson()
        {
            JObject report = new JObject();
            report.Add("DocumentsInIndex", DocumentsInIndex);
            report.Add("DocumentsInDatabase", DocumentsInDatabase);
            report.Add("Drift", Drift);
            return report.ToString();
        }
    }
}
