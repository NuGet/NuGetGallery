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

            ExtractId(package, document, errors);
            ExtractVersion(package, document, errors);
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
            ExtractDependencies(package, document);
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

        static void ExtractId(IDictionary<string, string> package, XDocument document, List<string> errors)
        {
            XElement idElement = document.Root.DescendantsAndSelf().Elements().Where(d => d.Name.LocalName == "id").FirstOrDefault();
            if (idElement != null)
            {
                package["id"] = (idElement.Value);
            }
            else
            {
                errors.Add("unable to find the id element in the nuspec");
            }
        }

        static void ExtractVersion(IDictionary<string, string> package, XDocument document, List<string> errors)
        {
            XElement idElement = document.Root.DescendantsAndSelf().Elements().Where(d => d.Name.LocalName == "version").FirstOrDefault();
            if (idElement != null)
            {
                package["version"] = (idElement.Value);
            }
            else
            {
                errors.Add("unable to find the version element in the nuspec");
            }
        }

        static void ExtractProperty(IDictionary<string, string> package, XDocument document, string name)
        {
            XElement element = document.Root.DescendantsAndSelf().Elements().Where(d => d.Name.LocalName == name).FirstOrDefault();
            if (element != null)
            {
                package[name] = element.Value;
            }
        }

        static void ExtractDependencies(IDictionary<string, string> package, XDocument document)
        {
            //TODO: extract from the XML - refer to the XSLT for an accurate definition of the generic structure
        }
    }
}
