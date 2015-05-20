// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Owin;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Publish
{
    public class CheckAccessImpl
    {
        IRegistrationOwnership _registrationOwnership;

        public CheckAccessImpl(IRegistrationOwnership registrationOwnership)
        {
            _registrationOwnership = registrationOwnership;
        }

        public async Task CheckAccess(IOwinContext context)
        {
            Trace.TraceInformation("CheckAccessImpl.CheckAccess");

            if (!_registrationOwnership.IsAuthenticated)
            {
                await ServiceHelpers.WriteErrorResponse(context, "user does not have access to the service", HttpStatusCode.Forbidden);
                return;
            }

            if (!await _registrationOwnership.HasTenantEnabled())
            {
                await ServiceHelpers.WriteErrorResponse(context, "package publication has not been enabled in this tenant", HttpStatusCode.Forbidden);
                return;
            }

            JObject body;
            if (!RequestHelpers.TryReadBody(context, out body))
            {
                await ServiceHelpers.WriteErrorResponse(context, "request body content must be JSON", HttpStatusCode.BadRequest);
                return;
            }

            IEnumerable<string> validationErrors = ValidationHelpers.CheckRequiredProperties(body, "namespace", "id", "version");

            if (validationErrors != null)
            {
                await ServiceHelpers.WriteErrorResponse(context, validationErrors, HttpStatusCode.BadRequest);
                return;
            }

            Trace.TraceInformation("CheckAccess process request");

            await ProcessRequest(context, body);
        }
        
        async Task ProcessRequest(IOwinContext context, JObject obj)
        {
            string ns = obj["namespace"].ToString();
            string id = obj["id"].ToString();
            string version = obj["version"].ToString();

            JObject content;

            if (await _registrationOwnership.HasRegistration(ns, id))
            {
                if (await _registrationOwnership.HasOwner(ns, id))
                {
                    if (await _registrationOwnership.HasVersion(ns, id, version))
                    {
                        content = new JObject
                        {
                            { "status", false },
                            { "message", string.Format("The package version {0}/{1}/{2} already exists", ns, id, version) }
                        };
                    }
                    else
                    {
                        content = new JObject
                        {
                            { "status", true },
                            { "message", string.Format("The package identification {0}/{1}/{2} is available and access is permitted", ns, id, version) }
                        };
                    }
                }
                else
                {
                    content = new JObject
                        {
                            { "status", false },
                            { "message", string.Format("The current user is not an owner of the package registration {0}/{1}", ns, id) }
                        };
                }
            }
            else
            {
                content = new JObject
                {
                    { "status", true },
                    { "message", string.Format("The package identification {0}/{1}/{2} is available and access is permitted", ns, id, version) }
                };
            }

            await ServiceHelpers.WriteResponse(context, content, HttpStatusCode.OK);
        }
    }
}
