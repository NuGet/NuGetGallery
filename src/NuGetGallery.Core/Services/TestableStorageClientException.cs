// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.Storage;

namespace NuGetGallery
{
    public class TestableStorageClientException : Exception
    {
        public TestableStorageClientException()
        {
        }

        public TestableStorageClientException(StorageException ex)
        {
            ErrorCode = ex.RequestInformation.ExtendedErrorInformation.ErrorCode;
        }

        public string ErrorCode { get; set; }
    }
}