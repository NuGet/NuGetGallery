// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.RegularExpressions;

namespace NuGet.Services.Validation.Orchestrator
{
    // TODO: Remove this type.
    // See: https://github.com/NuGet/Engineering/issues/1582
    // See: https://github.com/NuGet/Engineering/issues/1592
    public static class UsernameHelper
    {
        private const string UsernameRegex = @"^[A-Za-z0-9][A-Za-z0-9_\.-]+[A-Za-z0-9]$";

        public static bool IsInvalid(string username)
        {
            return !Regex.IsMatch(username, UsernameRegex, RegexOptions.None, TimeSpan.FromSeconds(5));
        }
    }
}
