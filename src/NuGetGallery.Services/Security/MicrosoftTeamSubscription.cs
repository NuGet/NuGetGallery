// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery.Security
{
    public class MicrosoftTeamSubscription : IUserSecurityPolicySubscription
    {
        private Lazy<List<UserSecurityPolicy>> _policies = new Lazy<List<UserSecurityPolicy>>(InitializePoliciesList, isThreadSafe: true);

        internal const string MicrosoftUsername = "Microsoft";
        internal const string Name = "MicrosoftTeamSubscription";
        internal static readonly string[] AllowedCopyrightNotices = new string[]
        {
            "(c) Microsoft Corporation. All rights reserved.",
            "&#169; Microsoft Corporation. All rights reserved.",
            "© Microsoft Corporation. All rights reserved.",
            "© Microsoft Corporation. Tüm hakları saklıdır.",
            "© Microsoft Corporation. Todos os direitos reservados.",
            "© Microsoft Corporation. Alle Rechte vorbehalten.",
            "© Microsoft Corporation. Všechna práva vyhrazena.",
            "© Microsoft Corporation. Todos los derechos reservados.",
            "© Microsoft Corporation. Wszelkie prawa zastrzeżone.",
            "© Microsoft Corporation. Tous droits réservés.",
            "© Microsoft Corporation。 保留所有权利。",
            "© Microsoft Corporation. Tutti i diritti riservati.",
            "© корпорация Майкрософт. Все права защищены.",
            "© Microsoft Corporation。 著作權所有，並保留一切權利。"
        };

        public string SubscriptionName => Name;

        public MicrosoftTeamSubscription()
        {
        }

        public IEnumerable<UserSecurityPolicy> Policies => _policies.Value;

        public Task OnSubscribeAsync(UserSecurityPolicySubscriptionContext context)
        {
            // Todo:
            // Maybe we should enumerate through the user's packages and add Microsoft as a package owner if the package passes the metadata requirements when a user is onboarded to this policy.
            // We should also unlock the package if it is locked as part of adding Microsoft as co-owner.
            return Task.CompletedTask;
        }

        public Task OnUnsubscribeAsync(UserSecurityPolicySubscriptionContext context)
        {
            return Task.CompletedTask;
        }

        private static List<UserSecurityPolicy> InitializePoliciesList()
        {
            return new List<UserSecurityPolicy>()
            {
                RequirePackageMetadataCompliancePolicy.CreatePolicy(
                    Name,
                    MicrosoftUsername,
                    allowedCopyrightNotices: AllowedCopyrightNotices,
                    allowedAuthors: new[] { MicrosoftUsername },
                    isLicenseUrlRequired: true,
                    isProjectUrlRequired: true,
                    errorMessageFormat: ServicesStrings.SecurityPolicy_RequireMicrosoftPackageMetadataComplianceForPush)
            };
        }
    }
}