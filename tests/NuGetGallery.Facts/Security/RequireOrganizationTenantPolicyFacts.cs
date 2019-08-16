// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Authentication;
using Xunit;

namespace NuGetGallery.Security
{
    public class RequireOrganizationTenantPolicyFacts
    {
        public const string TenantId = "tenant";

        public class TheEvaluateMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("different-tenant")]
            public async Task WhenNoMatchingTargetCredential_ReturnsError(string tenantId)
            {
                var result = await EvaluateAsync(tenantId);

                Assert.False(result.Success);
                Assert.Equal(
                    string.Format(Strings.AddMember_UserDoesNotMeetOrganizationPolicy, "testUser"),
                    result.ErrorMessage);
            }

            [Fact]
            public async Task WhenMatchingTargetCredential_ReturnsSuccess()
            {
                var result = await EvaluateAsync(TenantId);

                Assert.True(result.Success);
                Assert.Null(result.ErrorMessage);
            }

            private Task<SecurityPolicyResult> EvaluateAsync(string userTenantId)
            {
                var credentialBuilder = new CredentialBuilder();
                var fakes = new Fakes();

                if (!string.IsNullOrEmpty(userTenantId))
                {
                    // We can only have a single AAD account, remove previous one if present.
                    var aadCredential = fakes.User.Credentials.Single(c => c.Type.Contains(CredentialTypes.External.AzureActiveDirectoryAccount));
                    if (aadCredential != null)
                    {
                        fakes.User.Credentials.Remove(aadCredential);
                    }

                    // Add the new AAD credential
                    fakes.User.Credentials.Add(
                        credentialBuilder.CreateExternalCredential(
                        issuer: "AzureActiveDirectory",
                        value: "value",
                        identity: "identity",
                        tenantId: userTenantId));
                }

                foreach (var policy in RequireOrganizationTenantPolicy.Create(TenantId).Policies)
                {
                    fakes.Organization.SecurityPolicies.Add(policy);
                }

                var context = new UserSecurityPolicyEvaluationContext(
                    fakes.Organization.SecurityPolicies,
                    sourceAccount: fakes.Organization,
                    targetAccount: fakes.User
                    );

                return RequireOrganizationTenantPolicy
                    .Create()
                    .EvaluateAsync(context);
            }
        }
    }
}
