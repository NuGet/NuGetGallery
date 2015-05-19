// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            
            IEnumerable<string> validationErrors = ValidateGetAgreementAcceptance(context);

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
        
        static IEnumerable<string> ValidateGetAgreementAcceptance(IOwinContext context)
        {
            IList<string> errors = new List<string>();

            if (string.IsNullOrEmpty(context.Request.Query["agreement"]))
            {
                errors.Add("required property 'agreement' is missing from request");
            } 
            if (string.IsNullOrEmpty(context.Request.Query["agreementVersion"]))
            {
                errors.Add("required property \'agreementVersion\' is missing from request");
            }

            if (errors.Count == 0)
            {
                return null;
            }

            return errors;
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
            if (!TryReadBody(context, out body))
            {
                await ServiceHelpers.WriteErrorResponse(context, "request body content must be JSON", HttpStatusCode.BadRequest);
                return;
            }

            IEnumerable<string> validationErrors = ValidateAcceptAgreement(body);

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

        static bool TryReadBody(IOwinContext context, out JObject body)
        {
            try
            {
                using (StreamReader reader = new StreamReader(context.Request.Body))
                {
                    JObject obj = JObject.Parse(reader.ReadToEnd());
                    body = obj;
                    return true;
                }
            }
            catch (FormatException)
            {
                body = null;
                return false;
            }
        }

        static IEnumerable<string> ValidateAcceptAgreement(JObject obj)
        {
            IList<string> errors = new List<string>();

            CheckRequiredProperty(obj, errors, "agreement");
            CheckRequiredProperty(obj, errors, "agreementVersion");

            if (errors.Count == 0)
            {
                return null;
            }

            return errors;
        }

        static JToken CheckRequiredProperty(JObject obj, IList<string> errors, string name)
        {
            JToken token;
            if (!obj.TryGetValue(name, out token))
            {
                errors.Add(string.Format("required property '{0}' is missing from request", name));
            }
            return token;
        }
    }
}