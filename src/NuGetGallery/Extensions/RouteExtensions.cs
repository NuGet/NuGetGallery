﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;

namespace NuGetGallery
{
    public static class RouteExtensions
    {
        public struct ObfuscatedMetadata
        {
            public int ObfuscatedSegment
            { get; }

            public string ObfuscateValue
            { get; }

            public ObfuscatedMetadata(int obfuscatedSegment, string obfuscateValue)
            {
                ObfuscatedSegment = obfuscatedSegment;
                ObfuscateValue = obfuscateValue;
            }
        }

        internal static Dictionary<string, ObfuscatedMetadata> ObfuscatedRouteMap = new Dictionary<string, ObfuscatedMetadata>();

        public static void MapRoute(this RouteCollection routes, string name, string url, object defaults, ObfuscatedMetadata obfuscationMetadata)
        {
            routes.MapRoute(name, url, defaults);
            if (!ObfuscatedRouteMap.ContainsKey(url)) { ObfuscatedRouteMap.Add(url, obfuscationMetadata); }
        }

        public static string ObfuscateUrlPath(this Route route, string urlPath)
        {
            var path = route.Url;
           if (!ObfuscatedRouteMap.ContainsKey(path))
            {
                return null;
            }
            var metadata = ObfuscatedRouteMap[path];
            string[] segments = urlPath.Split('/');
            segments[metadata.ObfuscatedSegment] = metadata.ObfuscateValue;
            return segments.Aggregate((x, y) => { return x + "/" + y; });
        }
    }
}