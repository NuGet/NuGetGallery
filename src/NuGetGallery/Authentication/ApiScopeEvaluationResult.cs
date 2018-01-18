// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Authentication
{

    /// <summary>
    /// The result of evaluating the current user's scopes by using <see cref="ApiScopeEvaluator"/>.
    /// </summary>
    public class ApiScopeEvaluationResult : IComparable<ApiScopeEvaluationResult>
    {
        public bool ScopesAreValid { get; }
        public PermissionsCheckResult PermissionsCheckResult { get; }
        public User Owner { get; }

        public ApiScopeEvaluationResult(bool scopesAreValid, PermissionsCheckResult permissionsCheckResult, User owner)
        {
            ScopesAreValid = scopesAreValid;
            PermissionsCheckResult = permissionsCheckResult;
            Owner = owner;
        }

        public bool IsSuccessful()
        {
            return ScopesAreValid && PermissionsCheckResult == PermissionsCheckResult.Allowed;
        }

        public int CompareTo(ApiScopeEvaluationResult other)
        {
            if (ScopesAreValid == false)
            {
                return other.ScopesAreValid == true ? -1 : 0;
            }

            if (other.ScopesAreValid == false)
            {
                return ScopesAreValid == true ? 1 : 0;
            }

            var permissionsCheckResultDifference = (int)PermissionsCheckResult - (int)other.PermissionsCheckResult;
            if (permissionsCheckResultDifference == 0)
            {
                return (Owner != null ? 1 : 0) + (other.Owner != null ? -1 : 0);
            }

            return permissionsCheckResultDifference;
        }
    }
}