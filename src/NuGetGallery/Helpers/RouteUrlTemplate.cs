// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;

namespace NuGetGallery.Helpers
{
    public class RouteUrlTemplate<T>
    {
        private IDictionary<string, Func<T, object>> _routesGenerator;

        public RouteUrlTemplate(Func<RouteValueDictionary, string> linkGenerator, IDictionary<string, Func<T, object>> routesGenerator)
        {
            _routesGenerator = routesGenerator ?? throw new ArgumentNullException(nameof(routesGenerator));

            var _linkGenerator = linkGenerator ?? throw new ArgumentNullException(nameof(linkGenerator));
            var encodedLinkTemplate = linkGenerator(GetTemplateValues());
            LinkTemplate = HttpUtility.UrlDecode(encodedLinkTemplate);
        }

        public string LinkTemplate { get; }

        public string Resolve(T item)
        {
            var link = LinkTemplate;
            foreach (var routeValue in GetRouteValues(item))
            {
                var value = routeValue.Value as string ?? string.Empty;
                link = link.Replace($"{{{routeValue.Key}}}", value);
            }
            return link;
        }

        private RouteValueDictionary GetTemplateValues()
        {
            return new RouteValueDictionary(
                _routesGenerator.Keys.ToDictionary(
                    i => i,
                    i => (object)$"{{{i}}}")
                    );
        }

        private IDictionary<string, object> GetRouteValues(T item)
        {
            return _routesGenerator.ToDictionary(
                keySelector: i => i.Key,
                elementSelector: i => i.Value(item)
                );
        }
    }
}