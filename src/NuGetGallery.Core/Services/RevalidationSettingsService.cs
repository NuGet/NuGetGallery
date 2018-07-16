// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;

namespace NuGetGallery
{
    public class RevalidationSettingsService : IRevalidationSettingsService
    {
        private const string SettingsFileName = "settings.json";

        private readonly ICoreFileStorageService _fileService;
        private readonly JsonSerializer _serializer;

        public RevalidationSettingsService(ICoreFileStorageService fileService)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _serializer = new JsonSerializer();
        }

        public async Task<RevalidationSettings> GetSettingsAsync()
        {
            return (await GetSettingsInternalAsync()).Item2;
        }

        public async Task UpdateSettingsAsync(Action<RevalidationSettings> updateAction)
        {
            await MaybeUpdateSettingsAsync(s =>
            {
                updateAction(s);
                return true;
            });
        }

        public async Task<RevalidationSettings> MaybeUpdateSettingsAsync(Func<RevalidationSettings, bool> updateAction)
        {
            var getResult = await GetSettingsInternalAsync();
            var fileReference = getResult.Item1;
            var settings = getResult.Item2;

            // Only update the settings if the update action returns true.
            if (!updateAction(settings))
            {
                return settings;
            }

            try
            {
                var accessCondition = AccessConditionWrapper.GenerateIfMatchCondition(fileReference.ContentId);

                using (var stream = new MemoryStream())
                using (var writer = new StreamWriter(stream))
                using (var jsonWriter = new JsonTextWriter(writer))
                {
                    _serializer.Serialize(jsonWriter, settings);
                    jsonWriter.Flush();
                    stream.Position = 0;

                    await _fileService.SaveFileAsync(CoreConstants.RevalidationFolderName, SettingsFileName, stream, accessCondition);
                }

                return settings;
            }
            catch (StorageException e) when (e.IsPreconditionFailedException())
            {
                throw new InvalidOperationException("Failed to update the settings blob as the access condition failed", e);
            }
        }

        private async Task<Tuple<IFileReference, RevalidationSettings>> GetSettingsInternalAsync()
        {
            var fileReference = await _fileService.GetFileReferenceAsync(CoreConstants.RevalidationFolderName, SettingsFileName);

            if (fileReference == null)
            {
                throw new InvalidOperationException($"Could not find file '{SettingsFileName}' in folder '{CoreConstants.RevalidationFolderName}'");
            }

            using (var fileStream = fileReference.OpenRead())
            using (var reader = new StreamReader(fileStream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                var settings = _serializer.Deserialize<RevalidationSettings>(jsonReader);

                if (settings == null)
                {
                    throw new InvalidOperationException($"Settings blob '{SettingsFileName}' in folder '{CoreConstants.RevalidationFolderName}' is malformed");
                }

                return Tuple.Create(fileReference, settings);
            }
        }
    }
}
