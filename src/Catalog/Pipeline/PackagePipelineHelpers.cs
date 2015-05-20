// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGet.Services.Metadata.Catalog;
using System;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    static class PackagePipelineHelpers
    {
        public static ZipArchive GetZipArchive(PipelinePackage package, PackagePipelineContext context)
        {
            object obj;
            if (context.Shelf.TryGetValue(PackagePipelinePropertyNames.ZipArchivePropertyName, out obj))
            {
                return (ZipArchive)obj;
            }
            else
            {
                ZipArchive zipArchive = new ZipArchive(package.Stream, ZipArchiveMode.Read, true);
                context.Shelf.Add(PackagePipelinePropertyNames.ZipArchivePropertyName, zipArchive);
                return zipArchive;
            }
        }

        public static XDocument GetNuspec(PipelinePackage package, PackagePipelineContext context)
        {
            object obj;
            if (context.Shelf.TryGetValue(PackagePipelinePropertyNames.NuspecPropertyName, out obj))
            {
                return (XDocument)obj;
            }
            else
            {
                ZipArchive zipArchive = GetZipArchive(package, context);
                XDocument nuspec = Utils.GetNuspec(zipArchive);
                context.Shelf.Add(PackagePipelinePropertyNames.NuspecPropertyName, nuspec);
                return nuspec;
            }
        }

        public static Stream GetZipArchiveEntry(PipelinePackage package, PackagePipelineContext context, string entryName)
        {
            ZipArchive zipArchive = GetZipArchive(package, context);
            ZipArchiveEntry entry = zipArchive.GetEntry(entryName);
            return entry.Open();
        }

        public static XDocument GetXmlZipArchiveEntry(PipelinePackage package, PackagePipelineContext context, string entryName)
        {
            using (Stream stream = GetZipArchiveEntry(package, context, entryName))
            {
                return XDocument.Load(stream);
            }
        }

        public static DateTime? GetCommitTimeStamp(PackagePipelineContext context)
        {
            object obj;
            if (context.Shelf.TryGetValue(PackagePipelinePropertyNames.CommitTimeStampPropertyName, out obj))
            {
                return (DateTime)obj;
            }
            else
            {
                return null;
            }
        }

        public static void SetCommitTimeStamp(PackagePipelineContext context, DateTime commitTimeStamp)
        {
            context.Shelf[PackagePipelinePropertyNames.CommitTimeStampPropertyName] = commitTimeStamp;
        }

        public static Guid? GetCommitId(PackagePipelineContext context)
        {
            object obj;
            if (context.Shelf.TryGetValue(PackagePipelinePropertyNames.CommitIdPropertyName, out obj))
            {
                return (Guid)obj;
            }
            else
            {
                return null;
            }
        }

        public static void SetCommitId(PackagePipelineContext context, Guid commitId)
        {
            context.Shelf[PackagePipelinePropertyNames.CommitIdPropertyName] = commitId;
        }
    }
}
