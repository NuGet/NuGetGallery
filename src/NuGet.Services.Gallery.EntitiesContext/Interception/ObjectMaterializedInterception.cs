// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.Gallery.Entities
{
    public static class ObjectMaterializedInterception
    {
        private static readonly List<IObjectMaterializedInterceptor> _interceptors = new List<IObjectMaterializedInterceptor>();

        public static void AddInterceptor(IObjectMaterializedInterceptor interceptor)
        {
            _interceptors.Add(interceptor);
        }

        public static void InterceptObjectMaterialized(object entity)
        {
            if (entity == null)
            {
                return;
            }

            foreach (var interceptor in _interceptors)
            {
                interceptor.InterceptObjectMaterialized(entity);
            }
        }
    }
}