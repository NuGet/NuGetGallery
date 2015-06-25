// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Web.Http.Controllers;
using System.Web.Http.OData.Routing;
using System.Web.Http.OData.Routing.Conventions;

namespace NuGetGallery.OData.Conventions
{
    /// <summary>
    /// Adds support for composite keys in OData requests (e.g. (Id='',Version=''))
    /// </summary>
    public class CompositeKeyRoutingConvention
        : EntityRoutingConvention
    {
        private static readonly char[] KeyTrimChars = new char[] {' ', '\''};

        public override string SelectAction(ODataPath odataPath, HttpControllerContext controllerContext, ILookup<string, HttpActionDescriptor> actionMap)
        {
            var action = base.SelectAction(odataPath, controllerContext, actionMap);
            if (action != null)
            {
                var routeValues = controllerContext.RouteData.Values;
                if (routeValues.ContainsKey(ODataRouteConstants.Key))
                {
                    var keyRaw = routeValues[ODataRouteConstants.Key] as string;
                    if (keyRaw != null)
                    {
                        IEnumerable<string> compoundKeyPairs = keyRaw.Split(',');
                        if (!compoundKeyPairs.Any())
                        {
                            return action;
                        }

                        foreach (var compoundKeyPair in compoundKeyPairs)
                        {
                            string[] pair = compoundKeyPair.Split('=');
                            if (pair.Length != 2)
                            {
                                continue;
                            }
                            var keyName = pair[0].Trim(KeyTrimChars);
                            var keyValue = pair[1].Trim(KeyTrimChars);

                            routeValues.Add(keyName, keyValue);
                        }
                    }
                }
            }

            return action;
        }
    }
}