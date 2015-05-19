// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Security.Claims;
using System.Text;

namespace NuGet.Services.Metadata.Catalog.Ownership
{
    public class AgreementRecord
    {
        public string NameIdentifier { get; set; }
        public string Iss { get; set; }
        public string Email { get; set; }
        public string Agreement { get; set; }
        public string AgreementVersion { get; set; }
        public DateTime DateAccepted { get; set; }

        public static string GetKey(string nameIdentifier, string agreement)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(nameIdentifier + "_" + agreement);
            string base64 = Convert.ToBase64String(bytes);
            return base64;
        }

        public static AgreementRecord Create(ClaimsPrincipal claimsPrincipal, string agreement, string agreementVersion, string email)
        {
            return new AgreementRecord
            {
                NameIdentifier = Get(claimsPrincipal, ClaimTypes.NameIdentifier, true),
                Email = email ?? Get(claimsPrincipal, ClaimTypes.Email),
                Iss = Get(claimsPrincipal, "iss"),
                Agreement = agreement,
                AgreementVersion = agreementVersion,
                DateAccepted = DateTime.UtcNow
            };
        }

        static string Get(ClaimsPrincipal claimsPrincipal, string type, bool isRequired = false)
        {
            Claim subject = claimsPrincipal.FindFirst(type);
            if (subject == null)
            {
                if (isRequired)
                {
                    throw new Exception(string.Format("required Claim {0} not found", type));
                }
                else
                {
                    return null;
                }
            }
            return subject.Value;
        }
    }
}