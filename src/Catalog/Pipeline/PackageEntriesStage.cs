// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Metadata.Catalog;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    public class PackageEntriesStage : PackagePipelineStage
    {
        public override bool Execute(PipelinePackage package, PackagePipelineContext context)
        {
            ZipArchive zipArchive = PackagePipelineHelpers.GetZipArchive(package, context);

            IEnumerable<PackageEntry> entries = GetEntries(zipArchive);

            IGraph graph = new Graph();

            if (entries != null)
            {
                INode packageEntryPredicate = graph.CreateUriNode(Schema.Predicates.PackageEntry);
                INode packageEntryType = graph.CreateUriNode(Schema.DataTypes.PackageEntry);
                INode fullNamePredicate = graph.CreateUriNode(Schema.Predicates.FullName);
                INode namePredicate = graph.CreateUriNode(Schema.Predicates.Name);
                INode lengthPredicate = graph.CreateUriNode(Schema.Predicates.Length);
                INode compressedLengthPredicate = graph.CreateUriNode(Schema.Predicates.CompressedLength);
                INode rdfTypePredicate = graph.CreateUriNode(Schema.Predicates.Type);

                INode resource = graph.CreateUriNode(context.Uri);

                foreach (PackageEntry entry in entries)
                {
                    Uri entryUri = new Uri(context.Uri.AbsoluteUri + "#" + entry.FullName);

                    INode entryNode = graph.CreateUriNode(entryUri);

                    graph.Assert(resource, packageEntryPredicate, entryNode);
                    graph.Assert(entryNode, rdfTypePredicate, packageEntryType);
                    graph.Assert(entryNode, fullNamePredicate, graph.CreateLiteralNode(entry.FullName));
                    graph.Assert(entryNode, namePredicate, graph.CreateLiteralNode(entry.Name));
                    graph.Assert(entryNode, lengthPredicate, graph.CreateLiteralNode(entry.Length.ToString(), Schema.DataTypes.Integer));
                    graph.Assert(entryNode, compressedLengthPredicate, graph.CreateLiteralNode(entry.CompressedLength.ToString(), Schema.DataTypes.Integer));
                }
            }

            context.StageResults.Add(new GraphPackageMetadata(graph));

            return true;
        }

        IEnumerable<PackageEntry> GetEntries(ZipArchive zipArchive)
        {
            IList<PackageEntry> result = new List<PackageEntry>();

            foreach (ZipArchiveEntry entry in zipArchive.Entries)
            {
                if (entry.FullName.EndsWith("/.rels", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entry.FullName.EndsWith("[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entry.FullName.EndsWith(".psmdcp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(new PackageEntry(entry));
            }

            return result;
        }

        class PackageEntry
        {
            public PackageEntry(ZipArchiveEntry zipArchiveEntry)
            {
                FullName = zipArchiveEntry.FullName;
                Name = zipArchiveEntry.Name;
                Length = zipArchiveEntry.Length;
                CompressedLength = zipArchiveEntry.CompressedLength;
            }

            public string FullName { get; set; }
            public string Name { get; set; }
            public long Length { get; set; }
            public long CompressedLength { get; set; }
        }
    }
}
