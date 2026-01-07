// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Http.OData.Formatter.Serialization;
using Microsoft.Data.Edm;

namespace NuGetGallery.OData.Serializers
{
    public class CustomSerializerProvider 
        : DefaultODataSerializerProvider
    {
        private readonly ODataEdmTypeSerializer _entitySerializer;

        public CustomSerializerProvider(Func<DefaultODataSerializerProvider, ODataEdmTypeSerializer> factory)
        {
            _entitySerializer = factory(this);
        }

        public override ODataEdmTypeSerializer GetEdmTypeSerializer(IEdmTypeReference edmType)
        {
            if (edmType.IsEntity())
            {
                return _entitySerializer;
            }

            return base.GetEdmTypeSerializer(edmType);
        }
    }
}