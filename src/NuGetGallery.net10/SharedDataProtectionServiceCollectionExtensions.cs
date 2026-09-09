// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGetGallery.Authentication;
using NuGetGallery.DataProtection;
using NuGetGallery.Diagnostics;

namespace NuGetGallery;

public static class SharedDataProtectionServiceCollectionExtensions
{
    public const string ConfigurationSectionName = "DataProtection";

    public static IServiceCollection AddGallerySharedAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isProduction)
    {
        SharedDataProtectionConfiguration dataProtectionConfiguration = configuration
            .GetRequiredSection(ConfigurationSectionName)
            .Get<SharedDataProtectionConfiguration>()
            ?? throw new InvalidOperationException("The shared Data Protection configuration is missing.");
        dataProtectionConfiguration.Validate(isProduction);

        services
            .AddOptions<SharedDataProtectionConfiguration>()
            .Bind(configuration.GetRequiredSection(ConfigurationSectionName))
            .Validate(options => TryValidate(options, isProduction), "The shared Data Protection configuration is invalid.")
            .ValidateOnStart();

        ICoreFileStorageService storage = CreateStorage(configuration, dataProtectionConfiguration);
        var repository = new FileStorageXmlRepository(storage);
        KeyVaultConfiguration keyVaultConfiguration = dataProtectionConfiguration.EncryptKeysAtRest
            ? CreateKeyVaultConfiguration(configuration)
            : null;

        services
            .AddDataProtection()
            .ConfigureSharedDataProtection(
                repository,
                dataProtectionConfiguration,
                keyVaultConfiguration,
                isProduction);

        services
            .AddAuthentication(SharedCookieConstants.AuthenticationScheme)
            .AddCookie(SharedCookieConstants.AuthenticationScheme, options =>
            {
                options.Cookie.Name = SharedCookieConstants.CookieName;
                options.Cookie.Path = SharedCookieConstants.CookiePath;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = SharedCookieConstants.Expiration;
                options.SlidingExpiration = false;
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return System.Threading.Tasks.Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return System.Threading.Tasks.Task.CompletedTask;
                    },
                };
            });

        return services;
    }

    private static bool TryValidate(SharedDataProtectionConfiguration configuration, bool isProduction)
    {
        try
        {
            configuration.Validate(isProduction);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static ICoreFileStorageService CreateStorage(
        IConfiguration configuration,
        SharedDataProtectionConfiguration dataProtectionConfiguration)
    {
        if (string.Equals(
            dataProtectionConfiguration.StorageType,
            DataProtectionStorageType.FileSystem,
            StringComparison.Ordinal))
        {
            return new FileSystemFileStorageService(
                dataProtectionConfiguration.StorageLocation,
                new FileSystemService());
        }

        string managedIdentityClientId = configuration[Constants.StorageManagedIdentityClientIdPropertyName];

        CloudBlobClientWrapper client;
        switch (GetStorageCredentialMode(configuration))
        {
            case StorageCredentialMode.ConnectionString:
                client = new CloudBlobClientWrapper(dataProtectionConfiguration.StorageLocation);
                break;
            case StorageCredentialMode.DefaultAzureCredential:
                client = CloudBlobClientWrapper.UsingDefaultAzureCredential(dataProtectionConfiguration.StorageLocation);
                break;
            default:
                client = CloudBlobClientWrapper.UsingMsi(
                    dataProtectionConfiguration.StorageLocation,
                    managedIdentityClientId);
                break;
        }

        return new CloudBlobCoreFileStorageService(
            client,
            NullHostDiagnosticsService.Instance,
            new GalleryCloudBlobContainerInformationProvider());
    }

    internal static StorageCredentialMode GetStorageCredentialMode(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>(Constants.StorageUseManagedIdentityPropertyName))
        {
            return StorageCredentialMode.ConnectionString;
        }

        string managedIdentityClientId = configuration[Constants.StorageManagedIdentityClientIdPropertyName];
        bool localDevelopment = configuration.GetValue<bool>(Constants.ConfigureForLocalDevelopment);
        return localDevelopment && string.IsNullOrWhiteSpace(managedIdentityClientId)
            ? StorageCredentialMode.DefaultAzureCredential
            : StorageCredentialMode.ManagedIdentity;
    }

    private static KeyVaultConfiguration CreateKeyVaultConfiguration(IConfiguration configuration)
    {
        string vaultName = configuration[Constants.KeyVaultVaultNameKey];
        bool useManagedIdentity = configuration.GetValue<bool>(Constants.KeyVaultUseManagedIdentity);
        string clientId = configuration[Constants.KeyVaultClientIdKey];
        if (useManagedIdentity)
        {
            clientId = string.IsNullOrWhiteSpace(clientId)
                ? configuration[Constants.ManagedIdentityClientIdKey]
                : clientId;
            return new KeyVaultConfiguration(
                vaultName,
                clientId,
                configuration.GetValue<bool>(Constants.ConfigureForLocalDevelopment));
        }

        string storeName = configuration[Constants.KeyVaultStoreNameKey];
        string storeLocation = configuration[Constants.KeyVaultStoreLocationKey];
        X509Certificate2 certificate = CertificateUtility.FindCertificateByThumbprint(
            string.IsNullOrWhiteSpace(storeName) ? StoreName.My : Enum.Parse<StoreName>(storeName),
            string.IsNullOrWhiteSpace(storeLocation) ? StoreLocation.LocalMachine : Enum.Parse<StoreLocation>(storeLocation),
            configuration[Constants.KeyVaultCertificateThumbprintKey],
            configuration.GetValue<bool>(Constants.KeyVaultValidateCertificateKey));

        return new KeyVaultConfiguration(
            vaultName,
            configuration[Constants.KeyVaultTenantIdKey],
            clientId,
            certificate,
            configuration.GetValue<bool>(Constants.KeyVaultSendX5c));
    }

    private sealed class NullHostDiagnosticsService : IDiagnosticsService
    {
        public static readonly NullHostDiagnosticsService Instance = new();

        public IDiagnosticsSource GetSource(string name)
        {
            return NullDiagnosticsSource.Instance;
        }
    }

    internal enum StorageCredentialMode
    {
        ConnectionString,
        ManagedIdentity,
        DefaultAzureCredential,
    }
}
