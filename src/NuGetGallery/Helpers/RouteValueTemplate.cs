// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;

namespace NuGetGallery.Helpers
{
    public class RouteValueTemplate<T>
    {
        public RouteValueTemplate(Func<RouteValueDictionary, string> linkGenerator, IDictionary<string, Func<T, object>> routesGenerator)
        {
            RoutesGenerator = routesGenerator;

            var templateValues = GetTemplateValues();
            var encodedLinkTemplate = linkGenerator(templateValues);
            LinkTemplate = HttpUtility.UrlDecode(encodedLinkTemplate);
        }

        private string LinkTemplate { get; set; }

        private IDictionary<string, Func<T, object>> RoutesGenerator { get; set; }

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
                RoutesGenerator.Keys.ToDictionary(
                    i => i,
                    i => (object)$"{{{i}}}")
                    );
        }

        private IDictionary<string, object> GetRouteValues(T item)
        {
            return RoutesGenerator.ToDictionary(
                keySelector: i => i.Key,
                elementSelector: i => i.Value(item)
                );
        }
    }
}