// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Remoting.Messaging;

namespace NuGet.ApplicationInsights.Owin
{
    public static class OwinRequestIdContext
    {
        public static void Set(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                CallContext.LogicalSetData(RequestTrackingMiddleware.OwinRequestIdKey, value);
            }
        }

        public static string Get()
        {
            return CallContext.LogicalGetData(RequestTrackingMiddleware.OwinRequestIdKey) as string;
        }

        public static void Clear()
        {
            CallContext.LogicalSetData(RequestTrackingMiddleware.OwinRequestIdKey, null);
        }
    }
}