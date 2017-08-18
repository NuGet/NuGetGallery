// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureManagement
{
    [Serializable]
    public class AzureManagementException : Exception
    {
        public AzureManagementException(string message) : base(message)
        {
        }

        public AzureManagementException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
