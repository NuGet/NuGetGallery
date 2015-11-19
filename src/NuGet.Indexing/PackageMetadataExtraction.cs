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
    public static class PackageMetadataExtraction
    {
        public static IEnumerable<IDictionary<string, string>> GetPackages(string path, IDictionary<string, List<string>> errors)
        {
            var directoryInfo = new DirectoryInfo(path);
            foreach (var fileInfo in directoryInfo.EnumerateFiles("*.nupkg"))
            {
                var perPackageErrors = new List<string>();
                errors.Add(fileInfo.FullName, perPackageErrors);

                using (var stream = fileInfo.OpenRead())
                {
                    yield return MakePackageMetadata(stream, perPackageErrors);
                }
            }
        }

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

            var supportedFrameworks = new List<string>();
            //TODO: extract supported frameworks from the folder structure

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

        public static void MakePackage(Stream stream, IDictionary<string, string> package, List<string> errors)
        {
            XDocument document;
            if (!TryLoad(stream, out document, errors))
            {
                return;
            }

            ExtractRequiredProperty(package, document, errors, "id");
            ExtractRequiredProperty(package, document, errors, "version");
            ExtractProperty(package, document, "title");
            ExtractProperty(package, document, "summary");
            ExtractProperty(package, document, "tags");
            ExtractProperty(package, document, "authors", "flattenedAuthors");
            ExtractProperty(package, document, "description");
            ExtractProperty(package, document, "iconUrl");
            ExtractProperty(package, document, "projectUrl");
            ExtractProperty(package, document, "minClientVersion");
            ExtractProperty(package, document, "releaseNotes");
            ExtractProperty(package, document, "copyright");
            ExtractProperty(package, document, "language");
            ExtractProperty(package, document, "licenseUrl");
            ExtractProperty(package, document, "requiresLicenseAcceptance");
            ExtractDependencies(package, document);

            package["published"] = DateTimeOffset.UtcNow.ToString("O");
            package["listed"] = "true";
        }

        private static bool TryLoad(Stream stream, out XDocument document, List<string> errors)
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

        /// <summary>
        /// Same as <see cref="ExtractProperty"/> except an error is added if the property is not found in the .nuspec.
        /// </summary>
        /// <param name="package">The package data.</param>
        /// <param name="document">The parsed .nuspec.</param>
        /// <param name="errors">The list of errors encountered while parsing the .nuspec.</param>
        /// <param name="localName">The local name of the XML element to read the text from.</param>
        /// <param name="key">Optionally, the key to set in the <see cref="package"/> dictionary.</param>
        private static void ExtractRequiredProperty(IDictionary<string, string> package, XDocument document, List<string> errors, string localName, string key = null)
        {
            if (!ExtractProperty(package, document, localName, key))
            {
                errors.Add($"unable to find the '{localName}' element in the nuspec");
            }
        }

        /// <summary>
        /// Extract the property with local name <see cref="localName"/> from the .nuspec XML and added it to the
        /// <see cref="package"/> dictionary. If <see cref="key"/> is provided, this value will be used as the key in
        /// <see cref="package"/>. Otherwise, <see cref="localName"/> is used as the key.
        /// </summary>
        /// <param name="package">The package data.</param>
        /// <param name="document">The parsed .nuspec.</param>
        /// <param name="localName">The local name of the XML element to read the text from.</param>
        /// <param name="key">Optionally, the key to set in the <see cref="package"/> dictionary.</param>
        /// <returns>True if the element with the provided <see cref="localName"/> is found. False, otherwise.</returns>
        private static bool ExtractProperty(IDictionary<string, string> package, XDocument document, string localName, string key = null)
        {
            XElement element = document.Root.DescendantsAndSelf().Elements().FirstOrDefault(d => d.Name.LocalName == localName);
            if (element != null)
            {
                package[key ?? localName] = element.Value;
                return true;
            }

            return false;
        }

        private static void ExtractDependencies(IDictionary<string, string> package, XDocument document)
        {
            //TODO: extract from the XML - refer to the XSLT for an accurate definition of the generic structure
        }
    }
}
