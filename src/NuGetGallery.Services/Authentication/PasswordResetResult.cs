// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery.Authentication
{
    public class PasswordResetResult
    {
        public PasswordResetResult(PasswordResetResultType type, User user)
        {
            if (type != PasswordResetResultType.UserNotFound && user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            Type = type;
            User = user;
        }

        public PasswordResetResultType Type { get; }
        public User User { get; }
    }

    public enum PasswordResetResultType
    {
        UserNotFound,
        UserNotConfirmed,
        Success,
    }
}