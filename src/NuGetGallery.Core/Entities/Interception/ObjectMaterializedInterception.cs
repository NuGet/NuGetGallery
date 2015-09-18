// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public static class ObjectMaterializedInterception
    {
        private static readonly List<IObjectMaterializedInterceptor> Interceptors = new List<IObjectMaterializedInterceptor>();

        public static void AddInterceptor(IObjectMaterializedInterceptor interceptor)
        {
            Interceptors.Add(interceptor);
        }

        public static void InterceptObjectMaterialized(object entity)
        {
            if (entity == null)
            {
                return;
            }
            
            foreach (var interceptor in Interceptors)
            {
                interceptor.InterceptObjectMaterialized(entity);
            }
        }
    }
}