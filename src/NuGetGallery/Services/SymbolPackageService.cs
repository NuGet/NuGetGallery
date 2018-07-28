// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGetGallery.Auditing;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class SymbolPackageService : CoreSymbolPackageService, ISymbolPackageService
    {
        public SymbolPackageService(
            IEntityRepository<SymbolPackage> symbolPackageRepository,
            IPackageService packageService)
            : base(symbolPackageRepository, packageService)
        { }

        /// <summary>
        /// When no exceptions thrown, this method ensures the symbol package metadata is valid.
        /// </summary>
        /// <param name="packageArchiveReader">
        /// The <see cref="PackageArchiveReader"/> instance providing the package metadata.
        /// </param>
        /// <exception cref="InvalidPackageException">
        /// This exception will be thrown when a package metadata property violates a data validation constraint.
        /// </exception>
        public Task EnsureValid(PackageArchiveReader packageArchiveReader)
        {
            // Validate and throw Invalid package exception for any failed validations
            // 1. Is a symbol package by checking the package type, should be one and only package type.
            // 2. All files have pdb extensions.
            // 3. Has all the portable PDB files.
            return null;
        }

        /// <remarks>
        /// This method will create the symbol package entity. The caller should validate the ownership of packages and 
        /// metadata for the symbols associated for this package.
        /// </remarks>
        public async Task<SymbolPackage> CreateSymbolPackageAsync(Package nugetPackage, PackageStreamMetadata symbolPackageStreamMetadata)
        {
            try
            {
                var symbolPackage = new SymbolPackage()
                {
                    Package = nugetPackage,
                    Created = DateTime.UtcNow,
                    FileSize = symbolPackageStreamMetadata.Size,
                    HashAlgorithm = symbolPackageStreamMetadata.HashAlgorithm,
                    Hash = symbolPackageStreamMetadata.Hash,
                    StatusKey = PackageStatus.Validating
                };

                _symbolPackageRepository.InsertOnCommit(symbolPackage);

                await _symbolPackageRepository.CommitChangesAsync();

                return symbolPackage;
            }
            catch (Exception ex) when (ex is EntityException || ex is PackagingException)
            {
                throw new InvalidPackageException(ex.Message, ex);
            }
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