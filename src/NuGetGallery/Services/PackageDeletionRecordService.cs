// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NuGetGallery
{
    /// <summary>
    /// Saves <see cref="PackageDeletionRecord"/>s using a <see cref="IFileStorageService"/>.
    /// </summary>
    public class PackageDeletionRecordService : IPackageDeletionRecordService
    {
        public IFileStorageService _fileStorageService;

        public PackageDeletionRecordService(IFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
        }

        public async Task SaveDeletionRecord(Package package)
        {
            var deletionRecord = new PackageDeletionRecord(package);
            var deletionRecordString = JsonConvert.SerializeObject(deletionRecord);
            using (var deletionRecordStream = new MemoryStream(Encoding.UTF8.GetBytes(deletionRecordString)))
            {
                await _fileStorageService.SaveFileAsync(Constants.PackageDeletesFolderName, GetDeletionRecordFileName(deletionRecord), deletionRecordStream);
            }
        }

        private string GetDeletionRecordFileName(PackageDeletionRecord deletionRecord)
        {
            return $"{deletionRecord.Id.ToLowerInvariant()}/{deletionRecord.NormalizedVersion.ToLowerInvariant()}/{deletionRecord.DeletedTimestamp.ToUniversalTime().Ticks}.json";
        }
    }
}