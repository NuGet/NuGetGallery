// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;

namespace NuGetGallery
{
    public class RevalidationStateService : IRevalidationStateService
    {
        private const string StateFileName = "state.json";

        private readonly ICoreFileStorageService _storage;
        private readonly JsonSerializer _serializer;

        public RevalidationStateService(ICoreFileStorageService storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _serializer = new JsonSerializer();
        }

        public async Task<RevalidationState> GetStateAsync()
        {
            return (await GetStateInternalAsync()).Item2;
        }

        public async Task UpdateStateAsync(Action<RevalidationState> updateAction)
        {
            await MaybeUpdateStateAsync(s =>
            {
                updateAction(s);
                return true;
            });
        }

        public async Task<RevalidationState> MaybeUpdateStateAsync(Func<RevalidationState, bool> updateAction)
        {
            var getResult = await GetStateInternalAsync();
            var fileReference = getResult.Item1;
            var state = getResult.Item2;

            // Only update the state if the update action returns true.
            if (!updateAction(state))
            {
                return state;
            }

            try
            {
                var accessCondition = AccessConditionWrapper.GenerateIfMatchCondition(fileReference.ContentId);

                using (var stream = new MemoryStream())
                using (var writer = new StreamWriter(stream))
                using (var jsonWriter = new JsonTextWriter(writer))
                {
                    _serializer.Serialize(jsonWriter, state);
                    jsonWriter.Flush();
                    stream.Position = 0;

                    await _storage.SaveFileAsync(CoreConstants.RevalidationFolderName, StateFileName, stream, accessCondition);
                }

                return state;
            }
            catch (StorageException e) when (e.IsPreconditionFailedException())
            {
                throw new InvalidOperationException("Failed to update the state blob as the access condition failed", e);
            }
        }

        private async Task<Tuple<IFileReference, RevalidationState>> GetStateInternalAsync()
        {
            var fileReference = await _storage.GetFileReferenceAsync(CoreConstants.RevalidationFolderName, StateFileName);

            if (fileReference == null)
            {
                throw new InvalidOperationException($"Could not find file '{StateFileName}' in folder '{CoreConstants.RevalidationFolderName}'");
            }

            using (var fileStream = fileReference.OpenRead())
            using (var reader = new StreamReader(fileStream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                var state = _serializer.Deserialize<RevalidationState>(jsonReader);

                if (state == null)
                {
                    throw new InvalidOperationException($"State blob '{StateFileName}' in folder '{CoreConstants.RevalidationFolderName}' is malformed");
                }

                return Tuple.Create(fileReference, state);
            }
        }
    }
}
