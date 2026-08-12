// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Carries the authenticated caller, the credential-resolved staging owner, and the credential (with its scopes)
    /// through every Unit 9 management operation. Preserving the current user and credential — rather than passing the
    /// owner alone — lets the service apply the credential's <c>package:stage</c> subject restrictions consistently
    /// with ordinary package API behavior and the Unit 8 upload path.
    /// </summary>
    public sealed class StagingAuthorizationContext
    {
        public StagingAuthorizationContext(User currentUser, User owner, Credential credential)
        {
            CurrentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Credential = credential ?? throw new ArgumentNullException(nameof(credential));
        }

        /// <summary>The authenticated user making the request.</summary>
        public User CurrentUser { get; }

        /// <summary>The account resolved from the credential's owner scope; owns the staging work and its quota.</summary>
        public User Owner { get; }

        /// <summary>The API key or Trusted Publishing credential used for the request.</summary>
        public Credential Credential { get; }

        /// <summary>The scopes carried by <see cref="Credential"/>, or an empty set when it has none.</summary>
        public IEnumerable<Scope> Scopes => Credential.Scopes ?? Array.Empty<Scope>();
    }
}
