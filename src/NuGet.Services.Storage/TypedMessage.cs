// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Storage
{
    public class TypedMessage
    {
        public string Message { get; }

        public string Type { get; }

        public int Version { get; }

        public TypedMessage(string message, string type, int version)
        {
            Message = message;
            Type = type;
            Version = version;
        }
    }
}
