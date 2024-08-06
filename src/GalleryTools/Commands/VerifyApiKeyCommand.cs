// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Services.Entities;
using NuGetGallery.Infrastructure.Authentication;

namespace GalleryTools.Commands
{
    public static class VerifyApiKeyCommand
    {
        public static void Configure(CommandLineApplication config)
        {
            config.Description = "Verify a clear text API key matches a hashed API key.";
            config.HelpOption("-? | -h | --help");

            var plainTextOption = config.Option(
                "--clear-text",
                "The clear text value of the API key.",
                CommandOptionType.SingleValue);

            var credentialTypesOption = config.Option(
                "--type",
                "One or more credential types.",
                CommandOptionType.MultipleValue);

            var credentialValuesOption = config.Option(
                "--value",
                "One or more credential values.",
                CommandOptionType.MultipleValue);

            config.OnExecute(() => Execute(
                plainTextOption,
                credentialTypesOption,
                credentialValuesOption));
        }

        private static int Execute(
            CommandOption clearTextOption,
            CommandOption credentialTypesOption,
            CommandOption credentialValuesOption)
        {
            if (!clearTextOption.HasValue())
            {
                Console.Error.WriteLine($"The {clearTextOption.Template} option is required.");
                return 1;
            }

            if (!credentialTypesOption.HasValue())
            {
                Console.Error.WriteLine($"The {credentialTypesOption.Template} option is required.");
                return 1;
            }

            if (!credentialValuesOption.HasValue())
            {
                Console.Error.WriteLine($"The {credentialValuesOption.Template} option is required.");
                return 1;
            }

            if (credentialTypesOption.Values.Count != credentialValuesOption.Values.Count)
            {
                Console.Error.WriteLine($"The should be the same number of {credentialTypesOption.Template} options as {credentialValuesOption.Template} options.");
                return 1;
            }

            var credentialValidator = new CredentialValidator();

            Console.WriteLine($"Testing {credentialTypesOption.Values.Count} API key(s).");

            for (var i = 0; i < credentialTypesOption.Values.Count; i++)
            {
                var credential = new Credential
                {
                    Type = credentialTypesOption.Values[i],
                    Value = credentialValuesOption.Values[i],
                };

                var validCredentials = credentialValidator.GetValidCredentialsForApiKey(
                    new[] { credential }.AsQueryable(),
                    clearTextOption.Value());

                Console.WriteLine();
                Console.WriteLine($"API key {i + 1}:");
                Console.WriteLine($"  Type:    {credential.Type}");
                Console.WriteLine($"  Value:   {credential.Value}");
                Console.WriteLine($"  Matches: {validCredentials.Any().ToString().ToUpperInvariant()}");
            }

            return 0;
        }
    }
}