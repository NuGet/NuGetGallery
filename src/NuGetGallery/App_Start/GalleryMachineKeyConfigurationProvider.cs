// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Configuration;
using System.Diagnostics;
using System.Xml;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class GalleryMachineKeyConfigurationProvider : ProtectedConfigurationProvider
    {
        public static IGalleryConfigurationService Configuration { get; set; }

        public override XmlNode Decrypt(XmlNode encryptedNode)
        {
            Trace.TraceInformation($"[{nameof(GalleryMachineKeyConfigurationProvider)}] Initializing machine key configuration.");

            var xmlDoc = new XmlDocument();
            xmlDoc.XmlResolver = null;
            xmlDoc.AppendChild(xmlDoc.CreateElement(string.Empty, "machineKey", string.Empty));

            // Get the configuration used for fetching the machine key settings. These will be cached for the lifetime
            // of the process. This is acceptable because this function's outupt is also cached for the duration of the
            // process by the .NET Framework configuration system.
            var config = Configuration;
            if (Configuration == null)
            {
                Trace.TraceWarning($"[{nameof(GalleryMachineKeyConfigurationProvider)}] Initializing dedicated configuration service.");
                config = ConfigurationService.Initialize();
                Trace.TraceWarning($"[{nameof(GalleryMachineKeyConfigurationProvider)}] Initialized dedicated configuration service.");
                Configuration = config;
            }

            // The machine keys are used for encrypting/decrypting cookies used by ASP.NET, these are usually set by IIS in 'Auto' mode. 
            // During a deployment to Azure cloud service the same machine key values are set on all the instances of a given cloud service,
            // thereby providing session persistence across different instances in the same deployment slot. However, across different slots(staging vs production)
            // these session keys are different. Thereby causing the loss of session upon a slot swap. Manually setting these values on role start ensures same
            // keys are used by all the instances across all the slots of a Azure cloud service. See more analysis here: https://github.com/NuGet/Engineering/issues/1329
            Trace.TraceInformation($"[{nameof(GalleryMachineKeyConfigurationProvider)}] Checking current gallery configuration.");
            if (config.Current.EnableMachineKeyConfiguration
                && !string.IsNullOrWhiteSpace(config.Current.MachineKeyDecryption)
                && !string.IsNullOrWhiteSpace(config.Current.MachineKeyDecryptionKey)
                && !string.IsNullOrWhiteSpace(config.Current.MachineKeyValidationAlgorithm)
                && !string.IsNullOrWhiteSpace(config.Current.MachineKeyValidationKey))
            {
                Trace.TraceInformation($"[{nameof(GalleryMachineKeyConfigurationProvider)}] Using machine key settings from gallery configuration.");
                xmlDoc.DocumentElement.SetAttribute("decryptionKey", config.Current.MachineKeyDecryptionKey);
                xmlDoc.DocumentElement.SetAttribute("decryption", config.Current.MachineKeyDecryption);
                xmlDoc.DocumentElement.SetAttribute("validationKey", config.Current.MachineKeyValidationKey);
                xmlDoc.DocumentElement.SetAttribute("validation", config.Current.MachineKeyValidationAlgorithm);
            }
            else
            {
                Trace.TraceInformation($"[{nameof(GalleryMachineKeyConfigurationProvider)}] Gallery configuration does not have custom machine key settings.");
            }

            Trace.TraceInformation($"[{nameof(GalleryMachineKeyConfigurationProvider)}] Machine key configuration is complete.");

            return xmlDoc.DocumentElement;
        }

        public override XmlNode Encrypt(XmlNode node)
        {
            throw new NotImplementedException();
        }
    }
}
