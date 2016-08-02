// Copyright (c) .NET Foundation. All rights reserved.
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
                throw new ArgumentException(Strings.StreamMustBeReadable, nameof(readWriteStream));
            }

            if (!readWriteStream.CanWrite)
            {
                throw new ArgumentException(Strings.StreamMustBeWriteable, nameof(readWriteStream));
            }

            if (!readWriteStream.CanSeek)
            {
                throw new ArgumentException(Strings.StreamMustBeSeekable, nameof(readWriteStream));
            }

            using (var packageArchiveReader = new PackageArchiveReader(readWriteStream, leaveStreamOpen: true))
            {
                var nuspecReader = packageArchiveReader.GetNuspecReader();

                // Read <metadata> node from nuspec
                var metadataNode = nuspecReader.Xml.Root.Elements()
                    .FirstOrDefault(e => StringComparer.Ordinal.Equals(e.Name.LocalName, "metadata"));
                if (metadataNode == null)
                {
                    throw new PackagingException("The package manifest is missing the 'metadata' node.");
                }

                // Convert metadata into a ManifestEdit so that we can run it through the editing pipeline
                var editableManifestElements = new ManifestEdit
                {
                    Title = ReadFromMetadata(metadataNode, "title"),
                    Authors = ReadFromMetadata(metadataNode, "authors"),
                    Copyright = ReadFromMetadata(metadataNode, "copyright"),
                    Description = ReadFromMetadata(metadataNode, "description"),
                    IconUrl = ReadFromMetadata(metadataNode, "iconUrl"),
                    LicenseUrl = ReadFromMetadata(metadataNode, "licenseUrl"),
                    ProjectUrl = ReadFromMetadata(metadataNode, "projectUrl"),
                    ReleaseNotes = ReadFromMetadata(metadataNode, "releaseNotes"),
                    RequireLicenseAcceptance = ReadBoolFromMetadata(metadataNode, "requireLicenseAcceptance"),
                    Summary = ReadFromMetadata(metadataNode, "summary"),
                    Tags = ReadFromMetadata(metadataNode, "tags")
                };

                // Perform edits
                foreach (var edit in edits)
                {
                    edit.Invoke(editableManifestElements);
                }

                // Update the <metadata> node
                WriteToMetadata(metadataNode, "title", editableManifestElements.Title);
                WriteToMetadata(metadataNode, "authors", editableManifestElements.Authors);
                WriteToMetadata(metadataNode, "copyright", editableManifestElements.Copyright);
                WriteToMetadata(metadataNode, "description", editableManifestElements.Description);
                WriteToMetadata(metadataNode, "iconUrl", editableManifestElements.IconUrl);
                WriteToMetadata(metadataNode, "licenseUrl", editableManifestElements.LicenseUrl);
                WriteToMetadata(metadataNode, "projectUrl", editableManifestElements.ProjectUrl);
                WriteToMetadata(metadataNode, "releaseNotes", editableManifestElements.ReleaseNotes);
                WriteToMetadata(metadataNode, "requireLicenseAcceptance", editableManifestElements.RequireLicenseAcceptance.ToString(CultureInfo.InvariantCulture).ToLowerInvariant());
                WriteToMetadata(metadataNode, "summary", editableManifestElements.Summary);
                WriteToMetadata(metadataNode, "tags", editableManifestElements.Tags);

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

            if (element != null)
            {
                return element.Value;
            }

            return null;
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

        private static void WriteToMetadata(XElement metadataElement, string elementName, string value)
        {
            var element = metadataElement.Elements(XName.Get(elementName, metadataElement.GetDefaultNamespace().NamespaceName))
                .FirstOrDefault();

            if (element != null)
            {
                element.Value = value;
            }
            else
            {
                metadataElement.Add(new XElement(XName.Get(elementName, metadataElement.GetDefaultNamespace().NamespaceName), value));
            }
        }
    }
}