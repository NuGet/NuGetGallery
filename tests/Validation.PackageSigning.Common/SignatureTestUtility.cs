// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;
using Xunit;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    public static class SignatureTestUtility
    {
        // Central Directory file header size excluding signature, file name, extra field and file comment
        private const uint CentralDirectoryFileHeaderSizeWithoutSignature = 46;

        /// <summary>
        /// Get the certificate fingerprint for a given hash algorithm
        /// </summary>
        /// <param name="cert">Certificate to calculate fingerprint</param>
        /// <param name="hashAlgorithm">Hash algorithm to calculate fingerprint</param>
        /// <returns></returns>
        public static string GetFingerprint(X509Certificate2 cert, HashAlgorithmName hashAlgorithm)
        {
            return CertificateUtility.GetHashString(cert, hashAlgorithm);
        }

        public static Task WaitForCertificateExpirationAsync(X509Certificate2 certificate)
        {
            DateTimeOffset notAfter = DateTime.SpecifyKind(certificate.NotAfter, DateTimeKind.Local);

            // Ensure the certificate has expired.
            TimeSpan delay = notAfter.AddSeconds(1) - DateTimeOffset.UtcNow;

            if (delay > TimeSpan.Zero)
            {
                return Task.Delay(delay);
            }

            Assert.True(DateTimeOffset.Now > notAfter);

            return Task.CompletedTask;
        }

        public static PrimarySignature GenerateInvalidPrimarySignature(PrimarySignature signature)
        {
            var hash = Encoding.UTF8.GetBytes(signature.SignatureContent.HashValue);
            var newHash = Encoding.UTF8.GetBytes(new string('0', hash.Length));

            var bytes = signature.SignedCms.Encode();
            var newBytes = FindAndReplaceSequence(bytes, hash, newHash);

            return PrimarySignature.Load(newBytes);
        }

        public static byte[] FindAndReplaceSequence(byte[] bytes, byte[] find, byte[] replace)
        {
            var found = false;
            var from = -1;

            for (var i = 0; !found && i < bytes.Length - find.Length; ++i)
            {
                for (var j = 0; j < find.Length; ++j)
                {
                    if (bytes[i + j] != find[j])
                    {
                        break;
                    }

                    if (j == find.Length - 1)
                    {
                        from = i;
                        found = true;
                    }
                }
            }

            if (!found)
            {
                throw new Exception("Byte sequence not found.");
            }

            var byteList = new List<byte>(bytes);

            byteList.RemoveRange(from, find.Length);
            byteList.InsertRange(from, replace);

            return byteList.ToArray();
        }

        /// <summary>
        /// unsigns a package for test purposes.
        /// This does not timestamp a signature and can be used outside corp network.
        /// </summary>
        public static async Task ShiftSignatureMetadataAsync(SigningSpecifications spec, string signedPackagePath, string dir, int centralDirectoryIndex, int fileHeaderIndex)
        {
            var testSignatureProvider = new X509SignatureProvider(timestampProvider: null);

            // Create a temp path
            var copiedSignedPackagePath = Path.Combine(dir, Guid.NewGuid().ToString());

            using (var signedReadStream = File.OpenRead(signedPackagePath))
            using (var signedPackage = new BinaryReader(signedReadStream))
            using (var shiftedWriteStream = File.OpenWrite(copiedSignedPackagePath))
            using (var shiftedPackage = new BinaryWriter(shiftedWriteStream))
            {
                await ShiftSignatureMetadata(spec, signedPackage, shiftedPackage, centralDirectoryIndex, fileHeaderIndex);
            }

            // Overwrite the original package with the shifted one
            File.Copy(copiedSignedPackagePath, signedPackagePath, overwrite: true);
        }

        private static Task ShiftSignatureMetadata(SigningSpecifications spec, BinaryReader reader, BinaryWriter writer, int centralDirectoryIndex, int fileHeaderIndex)
        {
            var metadata = SignedPackageArchiveIOUtility.ReadSignedArchiveMetadata(reader);

            // Calculate new central directory record metadata with the the signature record and entry shifted
            var shiftedCdr = ShiftMetadata(spec, metadata, newSignatureFileEntryIndex: fileHeaderIndex, newSignatureCentralDirectoryRecordIndex: centralDirectoryIndex);

            // Order records by shifted ofset (new offset = old offset + change in offset).
            // This is the order they will appear in the new shifted package, but not necesarily the same order they were in the old package
            shiftedCdr.Sort((x, y) => (x.OffsetToLocalFileHeader + x.ChangeInOffset).CompareTo(y.OffsetToLocalFileHeader + y.ChangeInOffset));

            // Write data from start of file to first file entry
            reader.BaseStream.Seek(offset: 0, origin: SeekOrigin.Begin);
            SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(reader, writer, metadata.StartOfLocalFileHeaders);

            // Write all file entries in the new order
            foreach (var entry in shiftedCdr)
            {
                // We need to read each entry from their position in the old package and write them sequencially to the new package
                // The order in which they will appear in the new shited package is defined by the sorting done before starting to write
                reader.BaseStream.Seek(offset: entry.OffsetToLocalFileHeader, origin: SeekOrigin.Begin);
                SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(reader, writer, entry.OffsetToLocalFileHeader + entry.FileEntryTotalSize);
            }

            // Write all central directory records with updated offsets
            // We first need to sort them in the order they will appear in the new shifted package
            shiftedCdr.Sort((x, y) => x.IndexInHeaders.CompareTo(y.IndexInHeaders));
            foreach (var entry in shiftedCdr)
            {
                reader.BaseStream.Seek(offset: entry.Position, origin: SeekOrigin.Begin);
                // Read and write from the start of the central directory record until the relative offset of local file header (42 from the start of central directory record, incluing signature length)
                SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(reader, writer, reader.BaseStream.Position + 42);

                var relativeOffsetOfLocalFileHeader = (uint)(reader.ReadUInt32() + entry.ChangeInOffset);
                writer.Write(relativeOffsetOfLocalFileHeader);

                // We already read and hash the whole header, skip only filenameLength + extraFieldLength + fileCommentLength (46 is the size of the header without those lengths)
                SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(reader, writer, reader.BaseStream.Position + entry.HeaderSize - CentralDirectoryFileHeaderSizeWithoutSignature);
            }

            // Write everything after central directory records
            reader.BaseStream.Seek(offset: metadata.EndOfCentralDirectory, origin: SeekOrigin.Begin);
            SignedPackageArchiveIOUtility.ReadAndWriteUntilPosition(reader, writer, reader.BaseStream.Length);

            return Task.CompletedTask;
        }

        private static List<CentralDirectoryHeaderMetadata> ShiftMetadata(
            SigningSpecifications spec,
            SignedPackageArchiveMetadata metadata,
            int newSignatureFileEntryIndex,
            int newSignatureCentralDirectoryRecordIndex)
        {
            var shiftedCdr = new List<CentralDirectoryHeaderMetadata>(metadata.CentralDirectoryHeaders);

            // Sort Central Directory records in the order the file entries appear  in the original archive
            shiftedCdr.Sort((x, y) => x.OffsetToLocalFileHeader.CompareTo(y.OffsetToLocalFileHeader));

            // Shift Central Directory records to the desired position.
            // Because we sorted in the file entry order this will shift
            // the file entries
            ShiftSignatureToIndex(spec, shiftedCdr, newSignatureFileEntryIndex);

            // Calculate the change in offsets for the shifted file entries
            var lastEntryEnd = 0L;
            foreach (var cdr in shiftedCdr)
            {
                cdr.ChangeInOffset = lastEntryEnd - cdr.OffsetToLocalFileHeader;

                lastEntryEnd = cdr.OffsetToLocalFileHeader + cdr.FileEntryTotalSize + cdr.ChangeInOffset;
            }

            // Now we sort the central directory records in the order thecentral directory records appear in the original archive
            shiftedCdr.Sort((x, y) => x.Position.CompareTo(y.Position));

            // Shift Central Directory records to the desired position.
            // Because we sorted in the central directory records order this will shift
            // the central directory records
            ShiftSignatureToIndex(spec, shiftedCdr, newSignatureCentralDirectoryRecordIndex);

            // Calculate the new indexes for each central directory record
            var lastIndex = 0;
            foreach (var cdr in shiftedCdr)
            {
                cdr.IndexInHeaders = lastIndex;
                lastIndex++;
            }

            return shiftedCdr;
        }

        private static void ShiftSignatureToIndex(
            SigningSpecifications spec,
            List<CentralDirectoryHeaderMetadata> cdr,
            int index)
        {
            // Check for the signature object in the entries.
            // We have to do a manual check because we have no context
            // of the order in which the central directory records list is sorted.
            var recordIndex = 0;
            for (; recordIndex < cdr.Count; recordIndex++)
            {
                if (cdr[recordIndex].IsPackageSignatureFile)
                {
                    break;
                }
            }
            // Remove the signature object and add it to the new index
            var signatureCD = cdr[recordIndex];
            cdr.RemoveAt(recordIndex);
            cdr.Insert(index, signatureCD);
        }
    }
}
