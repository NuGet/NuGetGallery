// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Build.Framework;

namespace NuGet.Services.Build
{
    public class FindDuplicateFiles : Microsoft.Build.Utilities.Task
    {
        [Required]
        public ITaskItem[] Files { get; set; }

        [Output]
        public ITaskItem[] UniqueFiles { get; set; }

        [Output]
        public ITaskItem[] DuplicateFiles { get; set; }

        public override bool Execute()
        {
            var infos = GetUniqueTaskItemInfo();
            Log.LogMessage(
                MessageImportance.High,
                "Of the {0} items provided, {1} are unique file paths that exist. {2} items will be ignored.",
                Files.Length,
                infos.Count,
                Files.Length - infos.Count);

            if (infos.Count == 0)
            {
                UniqueFiles = Array.Empty<ITaskItem>();
                DuplicateFiles = Array.Empty<ITaskItem>();
                return true;
            }

            var filePathToDuplicates = FindDuplicates(infos);

            var fileCount = filePathToDuplicates.Sum(x => x.Value.Count);
            var uniqueCount = filePathToDuplicates.Count;
            var duplicateCount = fileCount - uniqueCount;
            Log.LogMessage(
                MessageImportance.High,
                "Of the {0} unique file paths provided, {1} ({2:P}) are unique and {3} ({4:P}) are duplicate.",
                fileCount,
                uniqueCount,
                (float)uniqueCount / fileCount,
                duplicateCount,
                (float)duplicateCount / fileCount);

            var uniqueFiles = new List<ITaskItem>();
            var duplicateFiles = new List<ITaskItem>();
            foreach (var pair in filePathToDuplicates)
            {
                foreach (var duplicate in pair.Value)
                {
                    if (pair.Key == duplicate.FullPath)
                    {
                        uniqueFiles.Add(duplicate.Item);
                    }
                    else
                    {
                        duplicate.Item.SetMetadata("DuplicateOf", pair.Key);
                        duplicateFiles.Add(duplicate.Item);
                    }
                }
            }

            UniqueFiles = uniqueFiles.ToArray();
            DuplicateFiles = duplicateFiles.ToArray();

            return true;
        }

        private List<TaskItemInfo> GetUniqueTaskItemInfo()
        {
            // Compare paths in a case insensitive manner.
            var uniqueFullPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var infos = new List<TaskItemInfo>();
            foreach (var item in Files)
            {
                var fullPath = item.GetMetadata("FullPath");
                if (fullPath == null || !File.Exists(fullPath))
                {
                    Log.LogWarning("File '{0}' does not exist.", item.ItemSpec);
                    continue;
                }

                if (!uniqueFullPaths.Add(fullPath))
                {
                    continue;
                }

                var info = new TaskItemInfo(item, fullPath);

                infos.Add(info);
            }

            return infos;
        }

        private Dictionary<string, List<TaskItemInfo>> FindDuplicates(List<TaskItemInfo> infos)
        {
            var fileSizeToInfos = GetFileSizeToInfos(infos);

            var filePathToDuplicates = new Dictionary<string, List<TaskItemInfo>>();

            // Inspired by https://stackoverflow.com/a/36113168.
            var buffer = new byte[1024];
            foreach (var fileSizePair in fileSizeToInfos)
            {
                // The file size is unique, the file is unique.
                if (TryAddUnique("file size", filePathToDuplicates, fileSizePair))
                {
                    continue;
                }

                BreakTiesWithLeadingHash(filePathToDuplicates, buffer, fileSizePair);
            }

            return filePathToDuplicates;
        }

        private Dictionary<long, List<TaskItemInfo>> GetFileSizeToInfos(
            IEnumerable<TaskItemInfo> infos)
        {
            var fileSizeToInfos = new Dictionary<long, List<TaskItemInfo>>();
            foreach (var info in infos)
            {
                info.FileSize = new FileInfo(info.FullPath).Length;

                List<TaskItemInfo> infosWithSameFileSize;
                if (!fileSizeToInfos.TryGetValue(info.FileSize, out infosWithSameFileSize))
                {
                    infosWithSameFileSize = new List<TaskItemInfo> { info };
                    fileSizeToInfos.Add(info.FileSize, infosWithSameFileSize);
                }
                else
                {
                    infosWithSameFileSize.Add(info);
                }
            }

            return fileSizeToInfos;
        }

        private void BreakTiesWithLeadingHash(
            Dictionary<string, List<TaskItemInfo>> filePathToDuplicates,
            byte[] buffer,
            KeyValuePair<long, List<TaskItemInfo>> fileSizePair)
        {
            var leadingHashToInfos = GetLeadingHashToInfos(fileSizePair.Value, buffer);

            foreach (var leadingHashPair in leadingHashToInfos)
            {
                // If the leading hash is unique, the file is unique.
                if (TryAddUnique("leading hash", filePathToDuplicates, leadingHashPair))
                {
                    continue;
                }

                BreakTiesWithHash(filePathToDuplicates, leadingHashPair);
            }
        }

        private static Dictionary<string, List<TaskItemInfo>> GetLeadingHashToInfos(
            IEnumerable<TaskItemInfo> infos,
            byte[] buffer)
        {
            var leadingHashToInfos = new Dictionary<string, List<TaskItemInfo>>();
            foreach (var info in infos)
            {
                using (var fileStream = new FileStream(info.FullPath, FileMode.Open))
                {
                    var totalRead = 0;
                    while (totalRead < buffer.Length)
                    {
                        var read = fileStream.Read(buffer, totalRead, buffer.Length - totalRead);
                        totalRead += read;
                        if (read == 0)
                        {
                            break;
                        }
                    }

                    using (var hashAlgorithm = SHA256.Create())
                    {
                        var hashBytes = hashAlgorithm.ComputeHash(buffer, 0, totalRead);
                        var hash = GetHashString(hashBytes);
                        info.HeaderHash = hash;

                        List<TaskItemInfo> infosWithSameLeadingHash;
                        if (!leadingHashToInfos.TryGetValue(hash, out infosWithSameLeadingHash))
                        {
                            infosWithSameLeadingHash = new List<TaskItemInfo> { info };
                            leadingHashToInfos.Add(hash, infosWithSameLeadingHash);
                        }
                        else
                        {
                            infosWithSameLeadingHash.Add(info);
                        }
                    }
                }
            }

            return leadingHashToInfos;
        }

        private void BreakTiesWithHash(
            Dictionary<string, List<TaskItemInfo>> filePathToDuplicates,
            KeyValuePair<string, List<TaskItemInfo>> leadingHashPair)
        {
            var hashToInfos = GetHashToInfos(leadingHashPair.Value);

            foreach (var hashPair in hashToInfos)
            {
                // If the hash is unique, the file is unique.
                if (TryAddUnique("hash", filePathToDuplicates, hashPair))
                {
                    continue;
                }

                // If multiple files has the same hash, they are duplicates.
                filePathToDuplicates.Add(hashPair.Value[0].FullPath, hashPair.Value);

                Log.LogMessage(
                    "File with {0} duplicates: {1}{2}{3}",
                    hashPair.Value.Count - 1,
                    hashPair.Value[0].FullPath,
                    string.Concat(hashPair.Value.Skip(1).Select(x => Environment.NewLine + " - " + x.FullPath)),
                    Environment.NewLine);
            }
        }

        private bool TryAddUnique<T>(
            string keyName,
            Dictionary<string, List<TaskItemInfo>> filePathToDuplicates,
            KeyValuePair<T, List<TaskItemInfo>> pair)
        {
            if (pair.Value.Count == 1)
            {
                Log.LogMessage(
                    "Unique file by {0} {1}: {2}{3}",
                    keyName,
                    pair.Key,
                    pair.Value[0].FullPath,
                    Environment.NewLine);

                filePathToDuplicates.Add(pair.Value[0].FullPath, new List<TaskItemInfo> { pair.Value[0] });
                return true;
            }

            return false;
        }

        private static Dictionary<string, List<TaskItemInfo>> GetHashToInfos(
            IEnumerable<TaskItemInfo> infos)
        {
            var hashToInfos = new Dictionary<string, List<TaskItemInfo>>();
            foreach (var info in infos)
            {
                using (var fileStream = new FileStream(info.FullPath, FileMode.Open))
                using (var hashAlgorithm = SHA256.Create())
                {
                    var hashBytes = hashAlgorithm.ComputeHash(fileStream);
                    var hash = GetHashString(hashBytes);
                    info.Hash = hash;

                    List<TaskItemInfo> infosWithSameHash;
                    if (!hashToInfos.TryGetValue(hash, out infosWithSameHash))
                    {
                        infosWithSameHash = new List<TaskItemInfo> { info };
                        hashToInfos.Add(hash, infosWithSameHash);
                    }
                    else
                    {
                        infosWithSameHash.Add(info);
                    }
                }
            }

            return hashToInfos;
        }

        private static string GetHashString(byte[] hashBytes)
        {
            return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
        }

        private class TaskItemInfo
        {
            public TaskItemInfo(ITaskItem item, string fullPath)
            {
                Item = item;
                FullPath = fullPath;
            }

            public ITaskItem Item { get; private set; }
            public string FullPath { get; private set; }
            public long FileSize { get; set; }
            public string HeaderHash { get; set; }
            public string Hash { get; set; }
        }
    }
}
