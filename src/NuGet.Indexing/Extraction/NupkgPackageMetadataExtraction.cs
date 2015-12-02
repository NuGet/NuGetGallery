// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

namespace NuGet.Indexing
{
    public static class NupkgPackageMetadataExtraction
    {
        public static IDictionary<string, string> MakePackageMetadata(Stream nupkgStream, List<string> errors)
        {
            var package = new Dictionary<string, string>();
            
            using (var zipArchive = new ZipArchive(nupkgStream, ZipArchiveMode.Read, true))
            {
                ZipArchiveEntry nuspecZipEntry = null;
                foreach (var zipEntry in zipArchive.Entries)
                {
                    if (!zipEntry.FullName.Contains('/') && zipEntry.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
                    {
                        nuspecZipEntry = zipEntry;
                        break;
                    }
                }
                if (nuspecZipEntry == null)
                {
                    errors.Add("unable to find nuspec in nupkg");
                    return new Dictionary<string, string>();
                }
                else
                {
                    using (var nuspecStream = nuspecZipEntry.Open())
                    {
                        MakePackage(nuspecStream, package, errors);
                    }
                }
            }
            
            // TODO: extract supported frameworks from the folder structure

            nupkgStream.Seek(0, SeekOrigin.Begin);
            package["packageSize"] = nupkgStream.Length.ToString();
            using (HashAlgorithm hashAlgorithm = HashAlgorithm.Create("SHA512"))
            {
                string hash = Convert.ToBase64String(hashAlgorithm.ComputeHash(nupkgStream));
                package["packageHash"] = hash;
                package["packageHashAlgorithm"] = "SHA512";
            }

            return package;
        }

        static void MakePackage(Stream stream, IDictionary<string, string> package, List<string> errors)
        {
            XDocument document;
            if (!TryLoad(stream, out document, errors))
            {
                return;
            }

            ExtractProperty(package, document, "id");
            ExtractProperty(package, document, "version", "originalVersion");
            ExtractProperty(package, document, "title");
            ExtractProperty(package, document, "summary");
            ExtractProperty(package, document, "tags");
            ExtractProperty(package, document, "authors");
            ExtractProperty(package, document, "description");
            ExtractProperty(package, document, "iconUrl");
            ExtractProperty(package, document, "projectUrl");
            ExtractProperty(package, document, "minClientVersion");
            ExtractProperty(package, document, "releaseNotes");
            ExtractProperty(package, document, "copyright");
            ExtractProperty(package, document, "language");
            ExtractProperty(package, document, "licenseUrl");
            ExtractProperty(package, document, "requiresLicenseAcceptance");

            // TODO: extract from the XML - refer to the XSLT for an accurate definition of the generic structure

            package["published"] = DateTimeOffset.UtcNow.ToString("O");
            package["listed"] = "true";
        }

        static bool TryLoad(Stream stream, out XDocument document, List<string> errors)
        {
            try
            {
                document = XDocument.Load(stream);
                return true;
            }
            catch (XmlException e)
            {
                errors.Add(e.Message);
                document = null;
                return false;
            }
        }

        static void ExtractProperty(IDictionary<string, string> package, XDocument document, string localName, string destination = null)
        {
            XElement element = document.Root.DescendantsAndSelf().Elements().FirstOrDefault(d => d.Name.LocalName == localName);
            if (element != null)
            {
                package[destination ?? localName] = element.Value;
            }
        }
    }
}
