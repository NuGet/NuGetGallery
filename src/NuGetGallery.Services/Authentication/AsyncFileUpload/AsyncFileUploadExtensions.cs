// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;

namespace NuGetGallery.AsyncFileUpload
{
    public static class AsyncFileUploadExtensions
    {
        public static void SetProgress(this ICacheService cacheService, string uploadKey, AsyncFileUploadProgress progress)
        {
            string cacheKey = GetUpdateCacheKey(uploadKey);
            cacheService.SetItem(cacheKey, progress, TimeSpan.FromHours(1));
        }

        public static AsyncFileUploadProgress GetProgress(this ICacheService cacheService, string uploadKey)
        {
            string cacheKey = GetUpdateCacheKey(uploadKey);
            return cacheService.GetItem(cacheKey) as AsyncFileUploadProgress;
        }

        public static void RemoveProgress(this ICacheService cacheService, string uploadKey)
        {
            string cacheKey = GetUpdateCacheKey(uploadKey);
            cacheService.RemoveItem(cacheKey);
        }

        private static string GetUpdateCacheKey(string uploadKey)
        {
            return "upload-" + uploadKey;
        }
    }
}