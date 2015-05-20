// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Owin;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Publish
{
    public class AgreementImpl
    {
        IRegistrationOwnership _registrationOwnership;

        public AgreementImpl(IRegistrationOwnership registrationOwnership)
        {
            _registrationOwnership = registrationOwnership;
        }

        public async Task GetAgreementAcceptance(IOwinContext context)
        {
            Trace.TraceInformation("AgreementImpl.GetAgreementAcceptance");

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
            
            IEnumerable<string> validationErrors = ValidationHelpers.CheckRequiredRequestParameters(context.Request, "agreement", "agreementVersion");
            if (validationErrors != null)
            {
                await ServiceHelpers.WriteErrorResponse(context, validationErrors, HttpStatusCode.BadRequest);
                return;
            }

            var agreement = context.Request.Query["agreement"];
            var agreementVersion = context.Request.Query["agreementVersion"];

            JObject content;

            var agreementRecord = await _registrationOwnership.GetAgreement(agreement, agreementVersion);
            if (agreementRecord == null)
            {
                content = new JObject
                {
                    { "agreement", agreement },
                    { "agreementVersion", agreementVersion },
                    { "accepted", false }
                };
            }
            else
            {
                content = new JObject
                {
                    { "agreement", agreementRecord.Agreement },
                    { "agreementVersion", agreementRecord.AgreementVersion },
                    { "email", agreementRecord.Email },
                    { "accepted", true },
                    { "dateAccepted", agreementRecord.DateAccepted }
                };
            }
            
            await ServiceHelpers.WriteResponse(context, content, HttpStatusCode.OK);
        }

        public async Task AcceptAgreement(IOwinContext context)
        {
            Trace.TraceInformation("AgreementImpl.AcceptAgreement");

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

            IEnumerable<string> validationErrors = ValidationHelpers.CheckRequiredProperties(body, "agreement", "agreementVersion");
            if (validationErrors != null)
            {
                await ServiceHelpers.WriteErrorResponse(context, validationErrors, HttpStatusCode.BadRequest);
                return;
            }

            var principal = context.Request.User as ClaimsPrincipal;
            var agreement = body["agreement"].ToString();
            var agreementVersion = body["agreementVersion"].ToString();
            var email = body["email"] != null ? body["email"].ToString() : principal.FindFirst(ClaimTypes.Email).Value;

            JObject content;

            var agreementRecord = await _registrationOwnership.AcceptAgreement(agreement, agreementVersion, email);
            if (agreementRecord == null)
            {
                content = new JObject
                {
                    { "status", false },
                    { "message", string.Format("The agreement {0} version {1} was not accepted.", agreement, agreementVersion) }
                };
            }
            else
            {
                content = new JObject
                {
                    { "status", true },
                    { "message", string.Format("The agreement {0} version {1} was accepted.", agreement, agreementVersion) }
                };
            }

            await ServiceHelpers.WriteResponse(context, content, HttpStatusCode.OK);
        }
    }
}