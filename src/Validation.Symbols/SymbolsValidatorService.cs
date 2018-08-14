// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;

namespace Validation.Symbols
{
    public class SymbolsValidatorService : ISymbolsValidatorService
    {
        private static TimeSpan _cleanWorkingDirectoryTimeSpan = TimeSpan.FromSeconds(20);
        private static readonly string[] PEExtensionsPatterns = new string[] { "*.dll", "*.exe" };
        private static readonly string SymbolExtensionPattern = "*.pdb";
        private static readonly string[] PEExtensions = new string[] { ".dll", ".exe" };
        private static readonly string[] SymbolExtension = new string[] { ".pdb" };

        private readonly ISymbolsFileService _symbolFileService;
        private readonly IZipArchiveService _zipArchiveService;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<SymbolsValidatorService> _logger;

        public SymbolsValidatorService(
            ISymbolsFileService symbolFileService,
            IZipArchiveService zipArchiveService,
            ITelemetryService telemetryService,
            ILogger<SymbolsValidatorService> logger)
        {
            _symbolFileService = symbolFileService ?? throw new ArgumentNullException(nameof(symbolFileService));
            _zipArchiveService = zipArchiveService ?? throw new ArgumentNullException(nameof(zipArchiveService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IValidationResult> ValidateSymbolsAsync(string packageId, string packageNormalizedVersion, CancellationToken token)
        {
            _logger.LogInformation("{ValidatorName} :Start ValidateSymbolsAsync. PackageId: {packageId} PackageNormalizedVersion: {packageNormalizedVersion}",
                ValidatorName.SymbolsValidator,
                packageId,
                packageNormalizedVersion);

            Stream snupkgstream;
            Stream nupkgstream;
            try
            {
                snupkgstream = await _symbolFileService.DownloadSnupkgFileAsync(packageId, packageNormalizedVersion, token);
            }
            catch(FileNotFoundException)
            {
                _telemetryService.TrackSymbolsPackageNotFoundEvent(packageId, packageNormalizedVersion);
                return ValidationResult.Failed;
            }
            try
            {
                nupkgstream = await _symbolFileService.DownloadNupkgFileAsync(packageId, packageNormalizedVersion, token);
            }
            catch (FileNotFoundException)
            {
                _telemetryService.TrackPackageNotFoundEvent(packageId, packageNormalizedVersion);
                return ValidationResult.Failed;
            }
            var pdbs = _zipArchiveService.ReadFilesFromZipStream(snupkgstream, SymbolExtension);
            var pes = _zipArchiveService.ReadFilesFromZipStream(nupkgstream, PEExtensions);

            using (_telemetryService.TrackSymbolValidationDurationEvent(packageId, packageNormalizedVersion, pdbs.Count))
            {
                if (!SymbolsHaveMatchingPEFiles(pdbs, pes))
                {
                    return ValidationResult.FailedWithIssues(ValidationIssue.SymbolErrorCode_MatchingPortablePDBNotFound);
                }
                var targetDirectory = Settings.GetWorkingDirectory();
                try
                {
                    _logger.LogInformation("Extracting symbols to {TargetDirectory}", targetDirectory);
                    var symbolFiles = _zipArchiveService.ExtractFilesFromZipStream(snupkgstream, targetDirectory, SymbolExtension);

                    _logger.LogInformation("Extracting dlls to {TargetDirectory}", targetDirectory);
                    _zipArchiveService.ExtractFilesFromZipStream(nupkgstream, targetDirectory, PEExtensions, symbolFiles);

                    var status = ValidateSymbolMatching(targetDirectory, packageId, packageNormalizedVersion);
                    return status;
                }
                finally
                {
                    TryCleanWorkingDirectoryForSeconds(targetDirectory, packageId, packageNormalizedVersion, _cleanWorkingDirectoryTimeSpan);
                }
            }
        }

        private void TryCleanWorkingDirectoryForSeconds(string workingDirectory, string packageId, string packageNormalizedVersion, TimeSpan seconds)
        {
            CancellationTokenSource cts = new CancellationTokenSource(seconds);
            _logger.LogInformation("{ValidatorName} :Start cleaning working directory {WorkingDirectory}. PackageId: {packageId} PackageNormalizedVersion: {packageNormalizedVersion}",
                ValidatorName.SymbolsValidator,
                workingDirectory,
                packageId,
                packageNormalizedVersion);
            Task cleanTask = new Task(() =>
            {
                IOException lastException = new IOException("NoStarted");
                bool directoryExists = Directory.Exists(workingDirectory);
                while (!cts.Token.IsCancellationRequested && directoryExists)
                {
                    directoryExists = Directory.Exists(workingDirectory);
                    try
                    {
                        if (directoryExists)
                        {
                            Directory.Delete(workingDirectory, true);
                        }
                    }
                    catch (IOException e)
                    {
                        lastException = e;
                    }
                }
                if(Directory.Exists(workingDirectory))
                {
                    _logger.LogWarning(0, lastException, "{ValidatorName} :TryCleanWorkingDirectory failed. WorkingDirectory:{WorkingDirectory}", ValidatorName.SymbolsValidator, workingDirectory);
                }
            }, TaskCreationOptions.LongRunning);

            cleanTask.Start();
        }

        /// <summary>
        /// The method that performs the actual validation.
        /// More information about checksum algorithm: 
        /// https://github.com/dotnet/corefx/blob/master/src/System.Reflection.Metadata/specs/PE-COFF.md#portable-pdb-checksum 
        /// </summary>
        /// <param name="targetDirectory">The directory used during the current validation.</param>
        /// <param name="packageId">Package Id.</param>
        /// <param name="packageNormalizedVersion">PackageNormalized version.</param>
        /// <returns></returns>
        public virtual IValidationResult ValidateSymbolMatching(string targetDirectory, string packageId, string packageNormalizedVersion)
        {
            foreach (string extension in PEExtensionsPatterns)
            {
                foreach (string peFile in Directory.GetFiles(targetDirectory, extension, SearchOption.AllDirectories))
                {
                    using (var peStream = File.OpenRead(peFile))
                    using (var peReader = new PEReader(peStream))
                    {
                        // This checks if portable PDB is associated with the PE file and opens it for reading. 
                        // It also validates that it matches the PE file.
                        // It does not validate that the checksum matches, so we need to do that in the following block.
                        if (peReader.TryOpenAssociatedPortablePdb(peFile, File.OpenRead, out var pdbReaderProvider, out var pdbPath) &&
                           // No need to validate embedded PDB (pdbPath == null for embedded)
                           pdbPath != null)
                        {
                            // Get all checksum entries. There can be more than one. At least one must match the PDB.
                            var checksumRecords = peReader.ReadDebugDirectory().Where(entry => entry.Type == DebugDirectoryEntryType.PdbChecksum)
                                .Select(e => peReader.ReadPdbChecksumDebugDirectoryData(e))
                                .ToArray();

                            if (checksumRecords.Length == 0)
                            {
                                return ValidationResult.FailedWithIssues(ValidationIssue.SymbolErrorCode_ChecksumDoesNotMatch);
                            }

                            var pdbBytes = File.ReadAllBytes(pdbPath);
                            var hashes = new Dictionary<string, byte[]>();

                            using (pdbReaderProvider)
                            {
                                var pdbReader = pdbReaderProvider.GetMetadataReader();
                                int idOffset = pdbReader.DebugMetadataHeader.IdStartOffset;

                                foreach (var checksumRecord in checksumRecords)
                                {
                                    if (!hashes.TryGetValue(checksumRecord.AlgorithmName, out var hash))
                                    {
                                        HashAlgorithmName han = new HashAlgorithmName(checksumRecord.AlgorithmName);
                                        using (var hashAlg = IncrementalHash.CreateHash(han))
                                        {
                                            hashAlg.AppendData(pdbBytes, 0, idOffset);
                                            hashAlg.AppendData(new byte[20]);
                                            int offset = idOffset + 20;
                                            int count = pdbBytes.Length - offset;
                                            hashAlg.AppendData(pdbBytes, offset, count);
                                            hash = hashAlg.GetHashAndReset();
                                        }
                                        hashes.Add(checksumRecord.AlgorithmName, hash);
                                    }
                                    if (checksumRecord.Checksum.ToArray().SequenceEqual(hash))
                                    {
                                        // found the right checksum
                                        return ValidationResult.Succeeded;
                                    }
                                }

                                // Not found any checksum record that matches the PDB.
                                return ValidationResult.FailedWithIssues(ValidationIssue.SymbolErrorCode_ChecksumDoesNotMatch);
                            }
                        }
                    }
                    return ValidationResult.FailedWithIssues(ValidationIssue.SymbolErrorCode_MatchingPortablePDBNotFound);
                }
            }
            // If did not return there were not any PE files to validate. In this case return error to not proceeed with an ingestion.
            _logger.LogError("{ValidatorName}: There were not any dll or exe files found locally." +
                             "This could indicate an issue in the execution or the package was not correct created. PackageId {PackageId} PackageNormalizedVersion {PackageNormalizedVersion}. " +
                             "SymbolCount: {SymbolCount}",
                             ValidatorName.SymbolsValidator,
                             packageId,
                             packageNormalizedVersion,
                             Directory.GetFiles(targetDirectory, SymbolExtensionPattern, SearchOption.AllDirectories));
            return ValidationResult.FailedWithIssues(ValidationIssue.SymbolErrorCode_MatchingPortablePDBNotFound);
        }

        /// <summary>
        /// Based on the snupkg, nupkg folder structure validate that the symbols have associated binary files.
        /// </summary>
        /// <param name="symbols">Symbol list extracted from the compressed folder.</param>
        /// <param name="PEs">The list of PE files extracted from the compressed folder.</param>
        /// <returns></returns>
        public static bool SymbolsHaveMatchingPEFiles(IEnumerable<string> symbols, IEnumerable<string> PEs)
        {
            if(symbols == null)
            {
                throw new ArgumentNullException(nameof(symbols));
            }
            if (PEs == null)
            {
                throw new ArgumentNullException(nameof(PEs));
            }
            var symbolsWithoutExtension = ZipArchiveService.RemoveExtension(symbols);
            var PEsWithoutExtensions = ZipArchiveService.RemoveExtension(PEs);
            return !symbolsWithoutExtension.Except(PEsWithoutExtensions, StringComparer.OrdinalIgnoreCase).Any();
        }
    }
}
