// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
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
            return (await GetInternalStateAsync()).State;
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
            var internalState = await GetInternalStateAsync();
            var fileReference = internalState.FileReference;
            var state = internalState.State;

            // Only update the state if the update action returns true.
            var originalState =  new RevalidationState
            {
                IsInitialized = state.IsInitialized,
                IsKillswitchActive = state.IsKillswitchActive,
                DesiredPackageEventRate = state.DesiredPackageEventRate,
            };

            if (!updateAction(state))
            {
                return originalState;
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

                    await _storage.SaveFileAsync(CoreConstants.Folders.RevalidationFolderName, StateFileName, stream, accessCondition);
                }

                return state;
            }
            catch (CloudBlobPreconditionFailedException e)
            {
                throw new InvalidOperationException("Failed to update the state blob since the access condition failed", e);
            }
        }

        private async Task<InternalState> GetInternalStateAsync()
        {
            var fileReference = await _storage.GetFileReferenceAsync(CoreConstants.Folders.RevalidationFolderName, StateFileName);

            if (fileReference == null)
            {
                throw new InvalidOperationException($"Could not find file '{StateFileName}' in folder '{CoreConstants.Folders.RevalidationFolderName}'");
            }

            using (var fileStream = fileReference.OpenRead())
            using (var reader = new StreamReader(fileStream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                var state = _serializer.Deserialize<RevalidationState>(jsonReader);

                if (state == null)
                {
                    throw new InvalidOperationException($"State blob '{StateFileName}' in folder '{CoreConstants.Folders.RevalidationFolderName}' is malformed");
                }

                return new InternalState(fileReference, state);
            }
        }

        private class InternalState
        {
            public InternalState(IFileReference fileReference, RevalidationState state)
            {
                FileReference = fileReference ?? throw new ArgumentNullException(nameof(fileReference));
                State = state ?? throw new ArgumentNullException(nameof(state));
            }

            public IFileReference FileReference { get; }
            public RevalidationState State { get; }
        }
    }
}
