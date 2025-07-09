// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    [DebuggerDisplay("{Name,nq}")]
    public abstract class TrustedPublisherPolicyDetailsViewModel
    {
        protected TrustedPublisherPolicyDetailsViewModel() { }

        /// <summary>
        /// Publisher type.
        /// </summary>
        public abstract FederatedCredentialType PublisherType { get; }

        /// <summary>
        /// Validates data in the view model. Returns error message if validation fails, otherwise returns null or empty string.
        /// </summary>
        public abstract string Validate();

        /// <summary>
        /// Creates a copy of itself and updates it with JSON data.
        /// </summary>
        /// <param name="javaScriptJson">A JSON string containing the updated details. Propery names match existing C# property names.</param>
        public abstract TrustedPublisherPolicyDetailsViewModel Update(string javaScriptJson);

        /// <summary>
        /// Converts the current object to a JSON string suitable for database storage.
        /// </summary>
        public abstract string ToDatabaseJson();
    }
}
