// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Configuration;
using System.Configuration;
using System.Reflection;
using System.Web.Configuration;

namespace NuGetGallery
{
    public static class MachineKey
    {
        public static void Setup(IGalleryConfigurationService config)
        {
            if (config.Current.EnableMachineKeyConfiguration
                && !string.IsNullOrWhiteSpace(config.Current.MachineKeyDecryption)
                && !string.IsNullOrWhiteSpace(config.Current.MachineKeyDecryptionKey)
                && !string.IsNullOrWhiteSpace(config.Current.MachineKeyValidationAlgorithm)
                && !string.IsNullOrWhiteSpace(config.Current.MachineKeyValidationKey))
            {
                var mksType = typeof(MachineKeySection);
                var mksSection = ConfigurationManager.GetSection("system.web/machineKey") as MachineKeySection;
                var resetMethod = mksType.GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Instance);

                var machineKeyConfig = new MachineKeySection();
                machineKeyConfig.ApplicationName = mksSection.ApplicationName;
                machineKeyConfig.CompatibilityMode = mksSection.CompatibilityMode;
                machineKeyConfig.DataProtectorType = mksSection.DataProtectorType;
                machineKeyConfig.Validation = mksSection.Validation;

                machineKeyConfig.DecryptionKey = config.Current.MachineKeyDecryptionKey;
                machineKeyConfig.Decryption = config.Current.MachineKeyDecryption;
                machineKeyConfig.ValidationKey = config.Current.MachineKeyValidationKey;
                machineKeyConfig.ValidationAlgorithm = config.Current.MachineKeyValidationAlgorithm;

                resetMethod.Invoke(mksSection, new object[] { machineKeyConfig });
            }
        }
    }
}