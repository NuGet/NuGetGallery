// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class SymbolPackageService : CoreSymbolPackageService, ISymbolPackageService
    {
        private const string PDBExtension = ".pdb";
        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>() {
            PDBExtension,
            ".nuspec",
            ".xml",
            ".psmdcp",
            ".rels",
            ".p7s"
        };

        public SymbolPackageService(
            IEntityRepository<SymbolPackage> symbolPackageRepository,
            IPackageService packageService)
            : base(symbolPackageRepository, packageService)
        { }

        /// <summary>
        /// When no exceptions thrown, this method ensures the symbol package metadata is valid.
        /// </summary>
        /// <param name="symbolPackageArchiveReader">
        /// The <see cref="PackageArchiveReader"/> instance providing the package metadata.
        /// </param>
        /// <exception cref="InvalidPackageException">
        /// This exception will be thrown when a package metadata property violates a data validation constraint.
        /// </exception>
        public async Task EnsureValidAsync(PackageArchiveReader symbolPackageArchiveReader)
        {
            if (symbolPackageArchiveReader == null)
            {
                throw new ArgumentNullException(nameof(symbolPackageArchiveReader));
            }

            // Validate following checks:
            // 1. Is a symbol package.
            // 2. The nuspec shouldn't have the 'owners'/'authors' field.
            // 3. All files have pdb extensions.
            // 4. Other package manifest validations.
            try
            {
                var packageMetadata = PackageMetadata.FromNuspecReader(
                    symbolPackageArchiveReader.GetNuspecReader(),
                    strict: true);

                if (!packageMetadata.IsSymbolsPackage())
                {
                    throw new InvalidPackageException(Strings.SymbolsPackage_NotSymbolPackage);
                }

                ValidateSymbolPackage(symbolPackageArchiveReader, packageMetadata);

                // This will throw if the package contains an entry which will extract outside of the target extraction directory
                await symbolPackageArchiveReader.ValidatePackageEntriesAsync(CancellationToken.None);
            }
            catch (Exception ex) when (ex is EntityException || ex is PackagingException)
            {
                // Wrap the exception for consistency of this API.
                throw new InvalidPackageException(ex.Message, ex);
            }
        }

        /// <remarks>
        /// This method will create the symbol package entity. The caller should validate the ownership of packages and 
        /// metadata for the symbols associated for this package. Its the caller's responsibility to commit as well.
        /// </remarks>
        public SymbolPackage CreateSymbolPackage(Package nugetPackage, PackageStreamMetadata symbolPackageStreamMetadata)
        {
            if (nugetPackage == null)
            {
                throw new ArgumentNullException(nameof(nugetPackage));
            }

            if (symbolPackageStreamMetadata == null)
            {
                throw new ArgumentNullException(nameof(symbolPackageStreamMetadata));
            }

            try
            {
                var symbolPackage = new SymbolPackage()
                {
                    Package = nugetPackage,
                    PackageKey = nugetPackage.Key,
                    Created = DateTime.UtcNow,
                    FileSize = symbolPackageStreamMetadata.Size,
                    HashAlgorithm = symbolPackageStreamMetadata.HashAlgorithm,
                    Hash = symbolPackageStreamMetadata.Hash
                };

                _symbolPackageRepository.InsertOnCommit(symbolPackage);

                return symbolPackage;
            }
            catch (Exception ex) when (ex is EntityException)
            {
                throw new InvalidPackageException(ex.Message, ex);
            }
        }

        private static void ValidateSymbolPackage(PackageArchiveReader symbolPackage, PackageMetadata metadata)
        {
            PackageHelper.ValidateNuGetPackageMetadata(metadata);

            // Validate nuspec manifest.
            var errors = ManifestValidator.Validate(symbolPackage.GetNuspec(), out var nuspec, out var packageMetadata).ToArray();
            if (errors.Length > 0)
            {
                var errorsString = string.Join("', '", errors.Select(error => error.ErrorMessage));
                throw new InvalidDataException(string.Format(
                    CultureInfo.CurrentCulture,
                    errors.Length > 1 ? Strings.UploadPackage_InvalidNuspecMultiple : Strings.UploadPackage_InvalidNuspec,
                    errorsString));
            }

            // Validate that the PII is not embedded in nuspec
            var invalidItems = new List<string>();
            if (metadata.Authors != null
                && (metadata.Authors.Count > 1
                    || !string.IsNullOrWhiteSpace(metadata.Authors.FirstOrDefault())))
            {
                invalidItems.Add("Authors");
            }

            if (metadata.Owners != null && metadata.Owners.Any())
            {
                invalidItems.Add("Owners");
            }

            if (invalidItems.Any())
            {
                throw new InvalidDataException(string.Format(Strings.SymbolsPackage_InvalidDataInNuspec, string.Join(",", invalidItems.ToArray())));
            }

            if (!CheckForAllowedFiles(symbolPackage))
            {
                throw new InvalidDataException(string.Format(Strings.SymbolsPackage_InvalidFiles, PDBExtension));
            }

            if (!CheckForPDBFiles(symbolPackage))
            {
                throw new InvalidDataException(string.Format(Strings.SymbolsPackage_NoSymbols));
            }
        }

        private static bool CheckForAllowedFiles(PackageArchiveReader symbolPackage)
        {
            foreach (var filePath in symbolPackage.GetFiles())
            {
                var fi = new FileInfo(filePath);
                if (!string.IsNullOrEmpty(fi.Name)
                    && !string.IsNullOrEmpty(fi.Extension)
                    && !AllowedExtensions.Contains(fi.Extension))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// If there are not any pdb files in the snupkg the publish action will be rejected.
        /// </summary>
        /// <param name="symbolPackage"></param>
        /// <returns></returns>
        private static bool CheckForPDBFiles(PackageArchiveReader symbolPackage)
        {
            return symbolPackage.GetFiles()
                .Any(filePath => !string.IsNullOrEmpty(Path.GetFileName(filePath)) && Path.GetExtension(filePath) == PDBExtension);
        }

        private static bool IsPortable(string pdbFile)
        {
            byte[] currentPDBStamp = new byte[4];
            // Portable pdbs have the first four bytes "B", "S", "J", "B"
            byte[] portableStamp = new byte[4] { 66, 83, 74, 66 };

            using (var peStream = File.OpenRead(pdbFile))
            {
                peStream.Read(currentPDBStamp, 0, 4);
            }

            return currentPDBStamp.SequenceEqual(portableStamp);
        }
    }
}