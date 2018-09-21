// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Net;

namespace NuGetGallery
{
    /// <summary>
    /// Non-exceptional results of calling <see cref="IPackageUploadService.CommitPackageAsync(Package, Stream)"/>.
    /// </summary>
    public enum PackageCommitResult
    {
        /// <summary>
        /// The package was successfully committed to the package file storage and to the database.
        /// </summary>
        Success,

        /// <summary>
        /// The package file conflicts with an existing package file. The package was not committed to the database.
        /// </summary>
        Conflict,
    }

    public enum PackageUploadResult
    {
        Created,
        Unauthorized,
        BadRequest,
        NotFound,
        Conflict,
        Success
    }

    public class PackageUploadOperationResult
    {
        public PackageUploadResult ResultCode;

        public string Message;

        public bool OperationSucceeded;

        public Package Package;

        public PackageUploadOperationResult(Package package, bool success)
            : this(PackageUploadResult.Success, message: null, success: success)
        {
            Package = package;
        }

        public PackageUploadOperationResult(PackageUploadResult resultCode, bool success)
            : this(resultCode, message: null, success: success) { }

        public PackageUploadOperationResult(PackageUploadResult resultCode, string message, bool success)
        {
            ResultCode = resultCode;
            Message = message;
            OperationSucceeded = success;
        }

        public HttpStatusCode GetHttpStatusCode()
        {
            switch(ResultCode)
            {
                case PackageUploadResult.Created:
                    return HttpStatusCode.Created;
                case PackageUploadResult.BadRequest:
                    return HttpStatusCode.BadRequest;
                case PackageUploadResult.Unauthorized:
                    return HttpStatusCode.Unauthorized;
                case PackageUploadResult.NotFound:
                    return HttpStatusCode.NotFound;
                case PackageUploadResult.Conflict:
                    return HttpStatusCode.Conflict;
                default:
                    throw new InvalidDataException($"The Package upload operation result code {ResultCode} is not supported.");
            }
        }
    }
}