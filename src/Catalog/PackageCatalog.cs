// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public static class PackageCatalog
    {
        public static IGraph CreateCommitMetadata(Uri indexUri, CommitMetadata commitMetadata)
        {
            IGraph graph = new Graph();

            if (commitMetadata.LastCreated != null)
            {
                graph.Assert(
                    graph.CreateUriNode(indexUri),
                    graph.CreateUriNode(Schema.Predicates.LastCreated),
                    graph.CreateLiteralNode(commitMetadata.LastCreated.Value.ToString("O"), Schema.DataTypes.DateTime));
            }
            if (commitMetadata.LastEdited != null)
            {
                graph.Assert(
                    graph.CreateUriNode(indexUri),
                    graph.CreateUriNode(Schema.Predicates.LastEdited),
                    graph.CreateLiteralNode(commitMetadata.LastEdited.Value.ToString("O"), Schema.DataTypes.DateTime));
            }
            if (commitMetadata.LastDeleted != null)
            {
                graph.Assert(
                    graph.CreateUriNode(indexUri),
                    graph.CreateUriNode(Schema.Predicates.LastDeleted),
                    graph.CreateLiteralNode(commitMetadata.LastDeleted.Value.ToString("O"), Schema.DataTypes.DateTime));
            }

            return graph;
        }

        public static async Task<CommitMetadata> ReadCommitMetadata(CatalogWriterBase writer, CancellationToken cancellationToken)
        {
            CommitMetadata commitMetadata = new CommitMetadata();

            string json = await writer.Storage.LoadStringAsync(writer.RootUri, cancellationToken);

            if (json != null)
            {
                JObject obj;

                using (JsonReader jsonReader = new JsonTextReader(new StringReader(json)))
                {
                    jsonReader.DateParseHandling = DateParseHandling.None;
                    obj = JObject.Load(jsonReader);
                }

                commitMetadata.LastCreated = TryGetDateTimeFromJObject(obj, "nuget:lastCreated");
                commitMetadata.LastEdited = TryGetDateTimeFromJObject(obj, "nuget:lastEdited");
                commitMetadata.LastDeleted = TryGetDateTimeFromJObject(obj, "nuget:lastDeleted");
            }

            return commitMetadata;
        }

        private static DateTime? TryGetDateTimeFromJObject(JObject target, string propertyName)
        {
            JToken token;
            if (target.TryGetValue(propertyName, out token))
            {
                return DateTime.Parse(token.ToString(), null, DateTimeStyles.RoundtripKind);
            }
            return null;
        }
    }
}