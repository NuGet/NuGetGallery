using Microsoft.Owin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

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

            IEnumerable<string> validationErrors = Validate(body);

            if (validationErrors != null)
            {
                await ServiceHelpers.WriteErrorResponse(context, validationErrors, HttpStatusCode.BadRequest);
                return;
            }

            await ProcessRequest(context, body);
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
            //TODO: catch more specific Exception
            catch (Exception e)
            {
                body = null;
                return false;
            }
        }

        static IEnumerable<string> Validate(JObject obj)
        {
            IList<string> errors = new List<string>();

            CheckRequiredProperty(obj, errors, "namespace");
            CheckRequiredProperty(obj, errors, "id");
            CheckRequiredProperty(obj, errors, "version");

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
