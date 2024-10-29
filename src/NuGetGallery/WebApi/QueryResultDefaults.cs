// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Http.OData.Query;

namespace NuGetGallery.WebApi
{
    public static class QueryResultDefaults
    {
        public static ODataQuerySettings DefaultQuerySettings = new ODataQuerySettings()
        {
            HandleNullPropagation = HandleNullPropagationOption.False,
            EnsureStableOrdering = true,
            EnableConstantParameterization = false
        };
    }
}