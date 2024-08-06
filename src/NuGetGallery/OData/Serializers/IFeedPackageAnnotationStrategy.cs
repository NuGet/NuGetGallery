// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.OData;
using System.Net.Http;

namespace NuGetGallery.OData.Serializers
{
    internal interface IFeedPackageAnnotationStrategy
    {
        bool CanAnnotate(object entityInstance);
        void Annotate(HttpRequestMessage request, ODataEntry entry, object entityInstance);
    }
}