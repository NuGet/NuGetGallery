// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace NuGetGallery
{
    public static class FileStorageResultExtensions
    {
        public static async Task<ActionResult> ToActionResultAsync(this Task<FileStorageResult> resultTask)
        {
            if (resultTask == null)
            {
                throw new ArgumentNullException(nameof(resultTask));
            }

            return (await resultTask).ToActionResult();
        }

        public static ActionResult ToActionResult(this FileStorageResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            switch (result)
            {
                case FileStorageResult.NotFound _:
                    return new HttpNotFoundResult();
                case FileStorageResult.Redirect redirect:
                    return new RedirectResult(redirect.RedirectUri.AbsoluteUri, permanent: false);
                case FileStorageResult.FilePath filePath:
                    return new FilePathResult(filePath.Path, filePath.ContentType)
                    {
                        FileDownloadName = filePath.FileDownloadName
                    };
                default:
                    throw new ArgumentException($"Unsupported file storage result type: {result.GetType().FullName}", nameof(result));
            }
        }
    }
}
