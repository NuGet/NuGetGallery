// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.FeatureFlags;
using NuGetGallery.Configuration;
using NuGetGallery.Features;

namespace NuGetGallery
{
    /// <summary>
    /// Represents a type that depends on <see cref="IFileStorageService"/>.
    /// </summary>
    internal class StorageDependent
    {
        private StorageDependent(
            string bindingKey,
            string azureStorageConnectionString,
            Type implementationType,
            Type interfaceType,
            bool isSingleInstance)
        {
            BindingKey = bindingKey;
            AzureStorageConnectionString = azureStorageConnectionString;
            ImplementationType = implementationType;
            InterfaceType = interfaceType;
            IsSingleInstance = isSingleInstance;
        }

        public StorageDependent SetBindingKey(string bindingKey)
        {
            return new StorageDependent(
                bindingKey,
                AzureStorageConnectionString,
                ImplementationType,
                InterfaceType,
                IsSingleInstance);
        }

        /// <summary>
        /// A key to be used by Autofac's <see cref="ResolutionExtensions.ResolveKeyed(IComponentContext, object, Type)"/>.
        /// </summary>
        public string BindingKey { get; }

        /// <summary>
        /// The connection string to be used for a <see cref="CloudBlobClientWrapper"/> instance.
        /// </summary>
        public string AzureStorageConnectionString { get; }

        /// <summary>
        /// The storage dependent's implementation type.
        /// </summary>
        public Type ImplementationType { get; }

        /// <summary>
        /// The storage dependent's interface type.
        /// </summary>
        public Type InterfaceType { get; }

        /// <summary>
        /// Indicates if the implementation type should be declared as a singleton in dependency injection container.
        /// </summary>
        public bool IsSingleInstance { get; }

        public static StorageDependent Create<TImplementation, TInterface>(
            string azureStorageConnectionString, bool isSingleInstance) where TImplementation : TInterface
        {
            return new StorageDependent(
                typeof(TImplementation).FullName,
                azureStorageConnectionString,
                typeof(TImplementation),
                typeof(TInterface),
                isSingleInstance);
        }

        /// <summary>
        /// Group the storage dependents by Azure Storage connection string then generate a binding key so that
        /// <see cref="IFileStorageService"/> instances are shared.
        /// </summary>
        public static IEnumerable<StorageDependent> GetAll(IAppConfiguration configuration)
        {
            const string DefaultBindingKey = "Default";

            /// This array must be added to as we implement more services that use <see cref="IFileStorageService"/>.
            var dependents = new[]
            {
                Create<CertificateService, ICertificateService>(configuration.AzureStorage_UserCertificates_ConnectionString, isSingleInstance: false),
                Create<ContentService, IContentService>(configuration.AzureStorage_Content_ConnectionString, isSingleInstance: true),
                Create<PackageFileService, IPackageFileService>(configuration.AzureStorage_Packages_ConnectionString, isSingleInstance: false),
                Create<SymbolPackageFileService, ISymbolPackageFileService>(configuration.AzureStorage_Packages_ConnectionString, isSingleInstance: false),
                Create<UploadFileService, IUploadFileService>(configuration.AzureStorage_Uploads_ConnectionString, isSingleInstance: false),
                Create<CoreLicenseFileService, ICoreLicenseFileService>(configuration.AzureStorage_Packages_ConnectionString, isSingleInstance: false),
                Create<RevalidationStateService, IRevalidationStateService>(configuration.AzureStorage_Revalidation_ConnectionString, isSingleInstance: false),
                Create<FeatureFlagFileStorageService, IFeatureFlagStorageService>(configuration.AzureStorage_FeatureFlags_ConnectionString, isSingleInstance: true)
            };

            var connectionStringToBindingKey = dependents
                .GroupBy(d => d.AzureStorageConnectionString ?? DefaultBindingKey)
                .ToDictionary(g => g.Key, g => string.Join(" ", g.Select(d => d.BindingKey)));

            foreach (var dependent in dependents)
            {
                var bindingKey = connectionStringToBindingKey[dependent.AzureStorageConnectionString ?? DefaultBindingKey];
                yield return dependent.SetBindingKey(bindingKey);
            }
        }
    }
}