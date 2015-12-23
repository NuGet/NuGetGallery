// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.Http.OData.Routing;
using System.Web.Http.OData.Routing.Conventions;

namespace NuGetGallery.OData.Conventions
{
    /// <summary>
    /// Routes an OData request with a given entityset, key and property to a method with the structure GetPropertyFrom[EntitySet](string propertyName, ...).
    /// </summary>
    public class EntitySetPropertyRoutingConvention 
        : IODataRoutingConvention
    {
        private readonly string _controllerName;

        public EntitySetPropertyRoutingConvention(string controllerName)
        {
            _controllerName = controllerName;
        }

        public string SelectController(ODataPath odataPath, HttpRequestMessage request)
        {
            if (odataPath.PathTemplate == "~/entityset/key/property")
            {
                return _controllerName;
            }
            return null;
        }

        public string SelectAction(ODataPath odataPath, HttpControllerContext controllerContext, ILookup<string, HttpActionDescriptor> actionMap)
        {
            if (odataPath.PathTemplate != "~/entityset/key/property")
            {
                return null;
            }

            var entitySetPathSegment = odataPath.Segments.OfType<EntitySetPathSegment>().Single();
            var keyValuePathSegment = odataPath.Segments.OfType<KeyValuePathSegment>().Single();
            var propertyAccessPathSegment = odataPath.Segments.OfType<PropertyAccessPathSegment>().Single();

            var actionName = string.Format(CultureInfo.InvariantCulture, "GetPropertyFrom{0}", entitySetPathSegment.EntitySetName);

            if (actionMap.Contains(actionName) && actionMap[actionName].Any(desc => MatchHttpMethod(desc, controllerContext.Request.Method)))
            {
                controllerContext.RouteData.Values.Add("propertyName", propertyAccessPathSegment.PropertyName);

                if (!CompositeODataKeyHelper.TryEnrichRouteValues(keyValuePathSegment.Value, controllerContext.RouteData.Values))
                {
                    controllerContext.RouteData.Values.Add("key", keyValuePathSegment.Value);
                }

                return actionName;
            }

            return null;
        }

        private static bool MatchHttpMethod(HttpActionDescriptor desc, HttpMethod method)
        {
            var supportedMethods = desc.ActionBinding.ActionDescriptor.SupportedHttpMethods;
            return supportedMethods.Contains(method);
        }
    }
}