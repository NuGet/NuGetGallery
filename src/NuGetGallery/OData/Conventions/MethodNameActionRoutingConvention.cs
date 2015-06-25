// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.Http.OData.Routing;
using System.Web.Http.OData.Routing.Conventions;

namespace NuGetGallery.OData.Conventions
{
    /// <summary>
    /// Routes an OData request with a given action to a method with the same name as the action.
    /// </summary>
    public class MethodNameActionRoutingConvention 
        : IODataRoutingConvention
    {
        private readonly string _controllerName;

        public MethodNameActionRoutingConvention(string controllerName)
        {
            _controllerName = controllerName;
        }

        public string SelectController(ODataPath odataPath, HttpRequestMessage request)
        {
            if (odataPath.PathTemplate == "~/action")
            {
                return _controllerName;
            }
            return null;
        }

        public string SelectAction(ODataPath odataPath, HttpControllerContext controllerContext, ILookup<string, HttpActionDescriptor> actionMap)
        {
            if (odataPath.PathTemplate != "~/action")
            {
                return null;
            }

            var actionSegment = odataPath.Segments.OfType<ActionPathSegment>().Single();
            var action = actionSegment.Action;

            if (action.IsBindable)
            {
                return null;
            }

            if (actionMap.Contains(action.Name) && actionMap[action.Name].Any(desc => MatchHttpMethod(desc, controllerContext.Request.Method)))
            {
                return action.Name;
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