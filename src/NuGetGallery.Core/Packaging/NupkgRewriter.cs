﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGetGallery.Packaging
{
    public class NupkgRewriter
    {
        /// <summary>
        /// Given the nupkg file as a read-write stream with random access (e.g. MemoryStream),
        /// This will replace the .nuspec file with a new .nuspec generated from 
        /// a) the old .nuspec file
        /// b) supplied edits
        /// 
        /// This function leaves readWriteStream open.
        /// </summary>
        public static void RewriteNupkgManifest(Stream readWriteStream, IEnumerable<Action<ManifestEdit>> edits)
        {
            if (!readWriteStream.CanRead)
            {
                throw new ArgumentException(CoreStrings.StreamMustBeReadable, nameof(readWriteStream));
            }

            if (!readWriteStream.CanWrite)
            {
                throw new ArgumentException(CoreStrings.StreamMustBeWriteable, nameof(readWriteStream));
            }

            if (!readWriteStream.CanSeek)
            {
                throw new ArgumentException(CoreStrings.StreamMustBeSeekable, nameof(readWriteStream));
            }

            using (var packageArchiveReader = new PackageArchiveReader(readWriteStream, leaveStreamOpen: true))
            {
                var nuspecReader = packageArchiveReader.GetNuspecReader();

                // Read <metadata> node from nuspec
                var metadataNode = nuspecReader.Xml.Root.Elements()
                    .FirstOrDefault(e => StringComparer.Ordinal.Equals(e.Name.LocalName, PackageMetadataStrings.Metadata));
                if (metadataNode == null)
                {
                    throw new PackagingException($"The package manifest is missing the '{PackageMetadataStrings.Metadata}' node.");
                }

                // Convert metadata into a ManifestEdit so that we can run it through the editing pipeline
                var editableManifestElements = new ManifestEdit
                {
                    Title = ReadFromMetadata(metadataNode, PackageMetadataStrings.Title),
                    Authors = ReadFromMetadata(metadataNode, PackageMetadataStrings.Authors),
                    Copyright = ReadFromMetadata(metadataNode, PackageMetadataStrings.Copyright),
                    Description = ReadFromMetadata(metadataNode, PackageMetadataStrings.Description),
                    IconUrl = ReadFromMetadata(metadataNode, PackageMetadataStrings.IconUrl),
                    LicenseUrl = ReadFromMetadata(metadataNode, PackageMetadataStrings.LicenseUrl),
                    ProjectUrl = ReadFromMetadata(metadataNode, PackageMetadataStrings.ProjectUrl),
                    ReleaseNotes = ReadFromMetadata(metadataNode, PackageMetadataStrings.ReleaseNotes),
                    RequireLicenseAcceptance = ReadBoolFromMetadata(metadataNode, PackageMetadataStrings.RequireLicenseAcceptance),
                    Summary = ReadFromMetadata(metadataNode, PackageMetadataStrings.Summary),
                    Tags = ReadFromMetadata(metadataNode, PackageMetadataStrings.Tags)
                };

                var originalManifestElements = (ManifestEdit)editableManifestElements.Clone();
                // Perform edits
                foreach (var edit in edits)
                {
                    edit.Invoke(editableManifestElements);
                }

                // Update the <metadata> node
                // Modify metadata elements only if they are changed.
                // 1. Do not add empty/null elements to metadata
                // 2. Remove the empty/null elements from metadata after edit
                // Apart from Authors, Description, Id and Version all other elements are optional.
                // Defined by spec here: https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Packaging/compiler/resources/nuspec.xsd
                WriteToMetadata(metadataNode, PackageMetadataStrings.Title, originalManifestElements.Title, editableManifestElements.Title, canBeRemoved: true);
                WriteToMetadata(metadataNode, PackageMetadataStrings.Authors, originalManifestElements.Authors, editableManifestElements.Authors);
                WriteToMetadata(metadataNode, PackageMetadataStrings.Copyright, originalManifestElements.Copyright, editableManifestElements.Copyright, canBeRemoved: true);
                WriteToMetadata(metadataNode, PackageMetadataStrings.Description, originalManifestElements.Description, editableManifestElements.Description);
                WriteToMetadata(metadataNode, PackageMetadataStrings.IconUrl, originalManifestElements.IconUrl, editableManifestElements.IconUrl, canBeRemoved: true);
                WriteToMetadata(metadataNode, PackageMetadataStrings.LicenseUrl, originalManifestElements.LicenseUrl, editableManifestElements.LicenseUrl, canBeRemoved: true);
                WriteToMetadata(metadataNode, PackageMetadataStrings.ProjectUrl, originalManifestElements.ProjectUrl, editableManifestElements.ProjectUrl, canBeRemoved: true);
                WriteToMetadata(metadataNode, PackageMetadataStrings.ReleaseNotes, originalManifestElements.ReleaseNotes, editableManifestElements.ReleaseNotes, canBeRemoved: true);
                WriteToMetadata(metadataNode, PackageMetadataStrings.RequireLicenseAcceptance,
                    originalManifestElements.RequireLicenseAcceptance.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
                    editableManifestElements.RequireLicenseAcceptance.ToString(CultureInfo.InvariantCulture).ToLowerInvariant());
                WriteToMetadata(metadataNode, PackageMetadataStrings.Summary, originalManifestElements.Summary, editableManifestElements.Summary, canBeRemoved: true);
                WriteToMetadata(metadataNode, PackageMetadataStrings.Tags, originalManifestElements.Tags, editableManifestElements.Tags, canBeRemoved: true);

                // Update the package stream
                using (var newManifestStream = new MemoryStream())
                {
                    nuspecReader.Xml.Save(newManifestStream);

                    using (var archive = new ZipArchive(readWriteStream, ZipArchiveMode.Update, leaveOpen: true))
                    {
                        var manifestEntries = archive.Entries
                            .Where(entry => entry.FullName.IndexOf("/", StringComparison.OrdinalIgnoreCase) == -1
                                && entry.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)).ToList();

                        if (manifestEntries.Count == 0)
                        {
                            throw new PackagingException("Nuspec file does not exist in package.");
                        }

                        if (manifestEntries.Count > 1)
                        {
                            throw new PackagingException("Package contains multiple nuspec files.");
                        }

                        var manifestEntry = manifestEntries[0];

                        using (var manifestOutputStream = manifestEntry.Open())
                        {
                            manifestOutputStream.SetLength(0);
                            newManifestStream.Position = 0;
                            newManifestStream.CopyTo(manifestOutputStream);
                        }
                    }
                }
            }
        }

        private static string ReadFromMetadata(XElement metadataElement, string elementName)
        {
            var element = metadataElement.Elements(XName.Get(elementName, metadataElement.GetDefaultNamespace().NamespaceName))
                .FirstOrDefault();

            return element?.Value;
        }

        private static bool ReadBoolFromMetadata(XElement metadataElement, string elementName)
        {
            var value = ReadFromMetadata(metadataElement, elementName);

            if (!string.IsNullOrEmpty(value))
            {
                bool result;
                if (bool.TryParse(value, out result))
                {
                    return result;
                }
            }

            return false;
        }

        private static void WriteToMetadata(XElement metadataElement, string elementName, string oldValue, string newValue, bool canBeRemoved = false)
        {
            if (oldValue == newValue)
            {
                return;
            }

            var element = metadataElement.Elements(XName.Get(elementName, metadataElement.GetDefaultNamespace().NamespaceName))
                .FirstOrDefault();

            if (element != null)
            {
                // Always set a non-null newValue for an element. For null values remove the element if possible.
                if (!string.IsNullOrEmpty(newValue))
                {
                    element.Value = newValue;
                }
                else if (canBeRemoved)
                {
                    element.Remove();
                }
            }
            else if (!string.IsNullOrEmpty(newValue))
            {
                metadataElement.Add(new XElement(XName.Get(elementName, metadataElement.GetDefaultNamespace().NamespaceName), newValue));
            }
        }
    }
}