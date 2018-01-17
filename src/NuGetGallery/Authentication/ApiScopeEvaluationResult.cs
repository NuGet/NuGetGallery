// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Authentication
{

    /// <summary>
    /// The result of evaluating the current user's scopes by using <see cref="ApiScopeEvaluator"/>.
    /// </summary>
    /// <remarks>
    /// When an current user's scopes are evaluated and none evaluate with <see cref="Success"/>, 
    /// the failed result to return is determined by <see cref="ApiScopeEvaluator.ChooseFailureResult(ApiScopeEvaluationResult, ApiScopeEvaluationResult)"/>.
    /// </remarks>
    public enum ApiScopeEvaluationResult
    {
        /// <summary>
        /// An error occurred and scopes were unable to be evaluated.
        /// </summary>
        Unknown,

        /// <summary>
        /// The scopes evaluated successfully.
        /// </summary>
        Success,

        /// <summary>
        /// The scopes do not match the action being performed.
        /// </summary>
        Forbidden,

        /// <summary>
        /// The scopes match the action being performed, but there is a reserved namespace conflict that prevents this action from being successful.
        /// </summary>
        ConflictReservedNamespace
    }
}