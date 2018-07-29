// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Jobs.Validation.Symbols.Core
{
    public class Settings
    {
        public const string SymbolServerTempDir = "NuGetSymServ";

        public static string GetWorkingDirectory()
        {
            return Path.Combine(Path.GetTempPath(), SymbolServerTempDir, Guid.NewGuid().ToString());
        }
    }
}
