// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Http.OData.Routing;
using Microsoft.Data.Edm;

namespace NuGetGallery.OData.Routing
{
    public class CountODataPathHandler
        : DefaultODataPathHandler
    {
        protected override ODataPathSegment ParseAtEntityCollection(IEdmModel model, ODataPathSegment previous, IEdmType previousEdmType, string segment)
        {
            if (segment == "$count")
            {
                return new CountPathSegment();
            }

            return base.ParseAtEntityCollection(model, previous, previousEdmType, segment);
        }
    }
}