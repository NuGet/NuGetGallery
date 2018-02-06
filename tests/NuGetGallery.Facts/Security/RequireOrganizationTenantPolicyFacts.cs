// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
            public void WhenNoMatchingTargetCredential_ReturnsError(string tenantId)
            {
                var result = Evaluate(null);

                Assert.False(result.Success);
                Assert.Equal(
                    string.Format(Strings.AddMember_UserDoesNotMeetOrganizationPolicy, "testUser"),
                    result.ErrorMessage);
            }

            [Fact]
            public void WhenMatchingTargetCredential_ReturnsSuccess()
            {
                var result = Evaluate(TenantId);

                Assert.True(result.Success);
                Assert.Null(result.ErrorMessage);
            }

            private SecurityPolicyResult Evaluate(string userTenantId)
            {
                var credentialBuilder = new CredentialBuilder();
                var fakes = new Fakes();

                if (!string.IsNullOrEmpty(userTenantId))
                {
                    fakes.User.Credentials.Add(
                        credentialBuilder.CreateExternalCredential(
                        issuer: "AzureActiveDirectory",
                        value: "value",
                        identity: "identity",
                        tenantId: userTenantId));
                }

                foreach (var policy in RequireOrganizationTenantPolicy.Create(fakes.Organization.Key, TenantId).Policies)
                {
                    fakes.Organization.SecurityPolicies.Add(policy);
                }

                var context = new UserSecurityPolicyEvaluationContext(
                    fakes.Organization.SecurityPolicies,
                    sourceAccount: fakes.Organization,
                    targetAccount: fakes.User
                    );

                return new RequireOrganizationTenantPolicy().Evaluate(context);
            }
        }
    }
}
