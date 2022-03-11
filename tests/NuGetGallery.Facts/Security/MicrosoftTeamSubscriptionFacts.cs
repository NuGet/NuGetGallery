// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.Auditing;
using Xunit;

namespace NuGetGallery.Security
{
    public class MicrosoftTeamSubscriptionFacts
    {
        [Fact]
        public void Policies_ReturnsMicrosoftTeamSubscriptionPolicies()
        {
            // Arrange.
            var subscription = CreateSecurityPolicyService().Subscriptions.Single();
            var policy = subscription.Policies.FirstOrDefault(p => p.Name.Equals(RequirePackageMetadataCompliancePolicy.PolicyName));

            // Act & Assert.
            Assert.Equal(1, subscription.Policies.Count());
            Assert.NotNull(policy);
            Assert.Equal(
                "{\"u\":\"Microsoft\"," +
                "\"copy\":" +
                "[" +
                "\"(c) Microsoft Corporation. All rights reserved.\"," +
                "\"&#169; Microsoft Corporation. All rights reserved.\"," +
                "\"© Microsoft Corporation. All rights reserved.\"," +
                "\"© Microsoft Corporation. Tüm hakları saklıdır.\"," +
                "\"© Microsoft Corporation. Todos os direitos reservados.\"," +
                "\"© Microsoft Corporation. Alle Rechte vorbehalten.\"," +
                "\"© Microsoft Corporation. Všechna práva vyhrazena.\"," +
                "\"© Microsoft Corporation. Todos los derechos reservados.\"," +
                "\"© Microsoft Corporation. Wszelkie prawa zastrzeżone.\"," +
                "\"© Microsoft Corporation. Tous droits réservés.\"," +
                "\"© Microsoft Corporation。 保留所有权利。\"," +
                "\"© Microsoft Corporation. 保留所有权利.\"," +
                "\"© Microsoft Corporation. Tutti i diritti riservati.\"," +
                "\"© корпорация Майкрософт. Все права защищены.\"," +
                "\"© Microsoft Corporation。 著作權所有，並保留一切權利。\"" +
                "]," +
                "\"authors\":[\"Microsoft\"]," +
                "\"licUrlReq\":true," +
                "\"projUrlReq\":true," +
                "\"error\":\"The package is not compliant with metadata requirements for Microsoft packages on NuGet.org. Go to https://aka.ms/Microsoft-NuGet-Compliance for more information.\\r\\nPolicy violations: {0}\"}", 
                policy.Value);
        }

        private TestSecurityPolicyService CreateSecurityPolicyService()
        {
            var auditing = new Mock<IAuditingService>();
            auditing.Setup(s => s.SaveAuditRecordAsync(It.IsAny<AuditRecord>())).Returns(Task.CompletedTask).Verifiable();

            var subscription = new MicrosoftTeamSubscription();

            var service = new TestSecurityPolicyService(
                mockAuditing: auditing,
                userSubscriptions: new[] { subscription },
                organizationSubscriptions: new[] { subscription });

            return service;
        }
    }
}
