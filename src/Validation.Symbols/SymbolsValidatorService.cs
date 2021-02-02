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

        public async Task<INuGetValidationResponse> ValidateSymbolsAsync(SymbolsValidatorMessage message, CancellationToken token)
        {
            _logger.LogInformation("{ValidatorName} :Start ValidateSymbolsAsync. PackageId: {packageId} PackageNormalizedVersion: {packageNormalizedVersion}",
                ValidatorName.SymbolsValidator,
                message.PackageId,
                message.PackageNormalizedVersion);

            try
            {
                using (Stream snupkgstream = await _symbolFileService.DownloadSnupkgFileAsync(message.SnupkgUrl, token))
                {
                    if (!await _zipArchiveService.ValidateZipAsync(snupkgstream, message.SnupkgUrl, token))
                    {
                        return NuGetValidationResponse.FailedWithIssues(ValidationIssue.SymbolErrorCode_SnupkgContainsEntriesNotSafeForExtraction);
                    }

                    try
                    {
                        using (Stream nupkgstream = await _symbolFileService.DownloadNupkgFileAsync(message.PackageId, message.PackageNormalizedVersion, token))
                        {
                            var pdbs = _zipArchiveService.ReadFilesFromZipStream(snupkgstream, SymbolExtension);
                            var pes = _zipArchiveService.ReadFilesFromZipStream(nupkgstream, PEExtensions);

                            if (pdbs.Count == 0)
                            {
                                return NuGetValidationResponse.FailedWithIssues(ValidationIssue.SymbolErrorCode_SnupkgDoesNotContainSymbols);
                            }

                            using (_telemetryService.TrackSymbolValidationDurationEvent(message.PackageId, message.PackageNormalizedVersion, pdbs.Count))
                            {
                                List<string> orphanSymbolFiles;
                                if (!SymbolsHaveMatchingPEFiles(pdbs, pes, out orphanSymbolFiles))
                                {
                                    orphanSymbolFiles.ForEach((symbol) =>
                                    {
                                        _telemetryService.TrackSymbolsAssemblyValidationResultEvent(message.PackageId, message.PackageNormalizedVersion, ValidationStatus.Failed, nameof(ValidationIssue.SymbolErrorCode_MatchingAssemblyNotFound), assemblyName: symbol);
                                    });
                                    _telemetryService.TrackSymbolsValidationResultEvent(message.PackageId, message.PackageNormalizedVersion, ValidationStatus.Failed);
                                    return NuGetValidationResponse.FailedWithIssues(ValidationIssue.SymbolErrorCode_MatchingAssemblyNotFound);
                                }
                                var targetDirectory = Settings.GetWorkingDirectory();
                                try
                                {
                                    _logger.LogInformation("Extracting symbols to {TargetDirectory}", targetDirectory);
                                    var symbolFiles = _zipArchiveService.ExtractFilesFromZipStream(snupkgstream, targetDirectory, SymbolExtension);

                                    _logger.LogInformation("Extracting dlls to {TargetDirectory}", targetDirectory);
                                    _zipArchiveService.ExtractFilesFromZipStream(nupkgstream, targetDirectory, PEExtensions, symbolFiles);

                                    var status = ValidateSymbolMatching(targetDirectory, message.PackageId, message.PackageNormalizedVersion);
                                    return status;
                                }
                                finally
                                {
                                    TryCleanWorkingDirectoryForSeconds(targetDirectory, message.PackageId, message.PackageNormalizedVersion, _cleanWorkingDirectoryTimeSpan);
                                }
                            }
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        _telemetryService.TrackPackageNotFoundEvent(message.PackageId, message.PackageNormalizedVersion);
                        return NuGetValidationResponse.Failed;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                _telemetryService.TrackSymbolsPackageNotFoundEvent(message.PackageId, message.PackageNormalizedVersion);
                return NuGetValidationResponse.Failed;
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
                if (Directory.Exists(workingDirectory))
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
        public virtual INuGetValidationResponse ValidateSymbolMatching(string targetDirectory, string packageId, string packageNormalizedVersion)
        {
            foreach (string extension in PEExtensionsPatterns)
            {
                foreach (string peFile in Directory.GetFiles(targetDirectory, extension, SearchOption.AllDirectories))
                {
                    INuGetValidationResponse validationResponse;

                    if (!IsPortable(GetSymbolPath(peFile)))
                    {
                        _telemetryService.TrackSymbolsValidationResultEvent(packageId, packageNormalizedVersion, ValidationStatus.Failed);
                        return NuGetValidationResponse.FailedWithIssues(ValidationIssue.SymbolErrorCode_PdbIsNotPortable);
                    }
                    if (!IsChecksumMatch(peFile, packageId, packageNormalizedVersion, out validationResponse))
                    {
                        _telemetryService.TrackSymbolsValidationResultEvent(packageId, packageNormalizedVersion, ValidationStatus.Failed);
                        return validationResponse;
                    }
                }
            }
            _telemetryService.TrackSymbolsValidationResultEvent(packageId, packageNormalizedVersion, ValidationStatus.Succeeded);
            return NuGetValidationResponse.Succeeded;
        }

        private bool IsChecksumMatch(string peFilePath, string packageId, string packageNormalizedVersion, out INuGetValidationResponse validationResponse)
        {
            validationResponse = NuGetValidationResponse.Succeeded;
            using (var peStream = File.OpenRead(peFilePath))
            using (var peReader = new PEReader(peStream))
            {
                // This checks if portable PDB is associated with the PE file and opens it for reading. 
                // It also validates that it matches the PE file.
                // It does not validate that the checksum matches, so we need to do that in the following block.
                if (peReader.TryOpenAssociatedPortablePdb(peFilePath, File.OpenRead, out var pdbReaderProvider, out var pdbPath) &&
                   // No need to validate embedded PDB (pdbPath == null for embedded)
                   pdbPath != null)
                {
                    // Get all checksum entries. There can be more than one. At least one must match the PDB.
                    var checksumRecords = peReader.ReadDebugDirectory().Where(entry => entry.Type == DebugDirectoryEntryType.PdbChecksum)
                        .Select(e => peReader.ReadPdbChecksumDebugDirectoryData(e))
                        .ToArray();

                    if (checksumRecords.Length == 0)
                    {
                        _telemetryService.TrackSymbolsAssemblyValidationResultEvent(packageId, packageNormalizedVersion, ValidationStatus.Failed, nameof(ValidationIssue.SymbolErrorCode_ChecksumDoesNotMatch), assemblyName: Path.GetFileName(peFilePath));
                        validationResponse = NuGetValidationResponse.FailedWithIssues(ValidationIssue.SymbolErrorCode_ChecksumDoesNotMatch);
                        return false;
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
                                _telemetryService.TrackSymbolsAssemblyValidationResultEvent(packageId, packageNormalizedVersion,  ValidationStatus.Succeeded, issue:"", assemblyName:Path.GetFileName(peFilePath));
                                return true;
                            }
                        }

                        // Not found any checksum record that matches the PDB.
                        _telemetryService.TrackSymbolsAssemblyValidationResultEvent(packageId, packageNormalizedVersion, ValidationStatus.Failed, nameof(ValidationIssue.SymbolErrorCode_ChecksumDoesNotMatch), assemblyName: Path.GetFileName(peFilePath));
                        validationResponse = NuGetValidationResponse.FailedWithIssues(ValidationIssue.SymbolErrorCode_ChecksumDoesNotMatch);
                        return false;
                    }
                }
            }
            _telemetryService.TrackSymbolsAssemblyValidationResultEvent(packageId, packageNormalizedVersion, ValidationStatus.Failed, nameof(ValidationIssue.SymbolErrorCode_MatchingAssemblyNotFound), assemblyName: Path.GetFileName(peFilePath));
            validationResponse = NuGetValidationResponse.FailedWithIssues(ValidationIssue.SymbolErrorCode_MatchingAssemblyNotFound);
            return false;
        }

        /// <summary>
        /// Verifies if the pdb is portable.
        /// </summary>
        /// <param name="pdbPath">The pdb path.</param>
        /// <returns>True if the pdb is portable.</returns>
        public static bool IsPortable(string pdbPath)
        {
            using (var pdbStream = File.OpenRead(pdbPath))
            {
                return IsPortable(pdbStream);
            }
        }

        /// <summary>
        /// Verifies if the pdb is portable.
        /// It does not dispose the stream.
        /// </summary>
        /// <param name="pdbStream">The pdbStream.</param>
        /// <returns>True if the pdb is portable.</returns>
        public static bool IsPortable(Stream pdbStream)
        {
            // Portable pdbs have the first four bytes "B", "S", "J", "B"
            var portableStamp = new byte[4] { 66, 83, 74, 66 };

            var currentPDBStamp = new byte[4];
            pdbStream.Read(currentPDBStamp, 0, 4);
            return currentPDBStamp.SequenceEqual(portableStamp);
        }

        public static string GetSymbolPath(string pePath)
        {
            return $"{Path.GetDirectoryName(pePath)}\\{Path.GetFileNameWithoutExtension(pePath)}.pdb";
        }

        /// <summary>
        /// Based on the snupkg, nupkg folder structure validate that the symbols have associated binary files.
        /// </summary>
        /// <param name="symbols">Symbol list extracted from the compressed folder.</param>
        /// <param name="PEs">The list of PE files extracted from the compressed folder.</param>
        /// <returns></returns>
        public static bool SymbolsHaveMatchingPEFiles(IEnumerable<string> symbols, IEnumerable<string> PEs, out List<string> orphanSymbolFiles)
        {
            if (symbols == null)
            {
                throw new ArgumentNullException(nameof(symbols));
            }
            if (PEs == null)
            {
                throw new ArgumentNullException(nameof(PEs));
            }
            var symbolsWithoutExtension = ZipArchiveService.RemoveExtension(symbols);
            var PEsWithoutExtensions = ZipArchiveService.RemoveExtension(PEs);
            orphanSymbolFiles = symbolsWithoutExtension.Except(PEsWithoutExtensions, StringComparer.OrdinalIgnoreCase).ToList();

            return !orphanSymbolFiles.Any();
        }
    }
}
