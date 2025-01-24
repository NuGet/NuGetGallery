// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGetGallery.Services.Authentication
{
    public enum FederatedCredentialValidationType
    {
        /// <summary>
        /// The federated credential validation resulted in the request being unauthorized. A user visible error may be provided.
        /// This result will take precedence over other results. This may be due to a bad credential format or another failure reason.
        /// </summary>
        Unauthorized = 1,

        /// <summary>
        /// The validator is not applicable to the given input. If no other validator is available, the token should be considered invalid.
        /// </summary>
        NotApplicable = 2,

        /// <summary>
        /// The token is valid. This will take precedence over other <see cref="NotApplicable"/> results.
        /// </summary>
        Valid = 3,
    }

    public class FederatedCredentialValidation
    {
        private static readonly FederatedCredentialValidation ValidInstance = new(FederatedCredentialValidationType.Valid, userError: null);
        private static readonly FederatedCredentialValidation NotApplicableInstance = new(FederatedCredentialValidationType.NotApplicable, userError: null);

        private FederatedCredentialValidation(FederatedCredentialValidationType type, string? userError)
        {
            Type = type;
            UserError = userError;
        }

        public FederatedCredentialValidationType Type { get; }
        public string? UserError { get; }

        public static FederatedCredentialValidation Unauthorized(string? userError) => new(FederatedCredentialValidationType.Unauthorized, userError);
        public static FederatedCredentialValidation NotApplicable() => NotApplicableInstance;
        public static FederatedCredentialValidation Valid() => ValidInstance;
    }
}
