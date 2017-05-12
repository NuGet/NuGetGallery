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
using NuGet.Versioning;

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
            package[MetadataConstants.PackageSizePropertyName] = nupkgStream.Length.ToString();
            using (HashAlgorithm hashAlgorithm = HashAlgorithm.Create(MetadataConstants.HashAlgorithmValue))
            {
                string hash = Convert.ToBase64String(hashAlgorithm.ComputeHash(nupkgStream));
                package[MetadataConstants.PackageHashPropertyName] = hash;
                package[MetadataConstants.PackageHashAlgorithmPropertyName] = MetadataConstants.HashAlgorithmValue;
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

            ExtractProperty(package, document, MetadataConstants.IdPropertyName);
            ExtractProperty(package, document, MetadataConstants.NuPkgMetadata.VersionPropertyName, MetadataConstants.VerbatimVersionPropertyName);
            ExtractProperty(package, document, MetadataConstants.TitlePropertyName);
            ExtractProperty(package, document, MetadataConstants.SummaryPropertyName);
            ExtractProperty(package, document, MetadataConstants.TagsPropertyName);
            ExtractProperty(package, document, MetadataConstants.AuthorsPropertyName);
            ExtractProperty(package, document, MetadataConstants.DescriptionPropertyName);
            ExtractProperty(package, document, MetadataConstants.IconUrlPropertyName);
            ExtractProperty(package, document, MetadataConstants.ProjectUrlPropertyName);
            ExtractProperty(package, document, MetadataConstants.MinClientVersionPropertyName);
            ExtractProperty(package, document, MetadataConstants.ReleaseNotesPropertyName);
            ExtractProperty(package, document, MetadataConstants.CopyrightPropertyName);
            ExtractProperty(package, document, MetadataConstants.LanguagePropertyName);
            ExtractProperty(package, document, MetadataConstants.LicenseUrlPropertyName);
            ExtractProperty(package, document, MetadataConstants.RequiresLicenseAcceptancePropertyName);

            // This check is currently in place to allow us to test filtering. Correct way to do it is to extract dependencies and iterate, in addition to the check that is currently here.
            NuGetVersion version;
            if (NuGetVersion.TryParse(package[MetadataConstants.VerbatimVersionPropertyName], out version))
            {
                package[MetadataConstants.SemVerLevelKeyPropertyName] = version.IsSemVer2 ? SemVerHelpers.SemVerLevelKeySemVer2 : String.Empty;
            }

            // TODO: extract from the XML - refer to the XSLT for an accurate definition of the generic structure

            package[MetadataConstants.PublishedPropertyName] = DateTimeOffset.UtcNow.ToString("O");
            package[MetadataConstants.ListedPropertyName] = "true";
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
