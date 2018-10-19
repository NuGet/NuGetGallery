// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using NuGet.Services.Entities;

namespace NuGet.Services.Messaging.Email
{
    /// <summary>
    /// APIs that provide lightweight extensibility for the User entity.
    /// </summary>
    public static class UserExtensionsCore
    {
        /// <summary>
        /// Convert a User's email to a System.Net MailAddress.
        /// </summary>
        public static MailAddress ToMailAddress(this User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (!user.Confirmed)
            {
                return new MailAddress(user.UnconfirmedEmailAddress, user.Username);
            }

            return new MailAddress(user.EmailAddress, user.Username);
        }
    }
}