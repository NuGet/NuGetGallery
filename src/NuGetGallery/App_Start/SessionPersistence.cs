// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Configuration;
using System.Reflection;
using System.Web.Configuration;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public static class SessionPersistence
    {
        public static void Setup(IGalleryConfigurationService config)
        {
            // The machine keys are used for encrypting/decrypting cookies used by ASP.NET, these are usually set by IIS in 'Auto' mode. 
            // During a deployment to Azure cloud service the same machine key values are set on all the instances of a given cloud service,
            // thereby providing session persistence across different instances in the same deployment slot. However, across different slots(staging vs production)
            // these session keys are different. Thereby causing the loss of session upon a slot swap. Manually setting these values on role start ensures same
            // keys are used by all the instances across all the slots of a Azure cloud service. See more analysis here: https://github.com/NuGet/Engineering/issues/1329
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