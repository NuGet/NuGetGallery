// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Configuration;
using System.Diagnostics;
using System.Threading;
using System.Xml;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class GalleryMachineKeyConfigurationProvider : ProtectedConfigurationProvider
    {
        private static ManualResetEventSlim _configurationSet = new ManualResetEventSlim(false);

        private static IAppConfiguration _configuration = null;
        public static IAppConfiguration Configuration
        {
            get 
            {
                return _configuration;
            }
            set 
            {
                _configuration = value;
                _configurationSet.Set();
            }
        }

        public override XmlNode Decrypt(XmlNode encryptedNode)
        {
            Trace.TraceInformation($"[{nameof(GalleryMachineKeyConfigurationProvider)}] Initializing machine key configuration.");

            var xmlDoc = new XmlDocument();
            xmlDoc.XmlResolver = null;
            xmlDoc.AppendChild(xmlDoc.CreateElement(string.Empty, "machineKey", string.Empty));

            // Get the configuration used for fetching the machine key settings. These will be cached for the lifetime
            // of the process. This is acceptable because this function's outupt is also cached for the duration of the
            // process by the .NET Framework configuration system.
            _configurationSet.Wait();
            var config = Configuration;

            // The machine keys are used for encrypting/decrypting cookies used by ASP.NET, these are usually set by IIS in 'Auto' mode. 
            // During a deployment to Azure cloud service the same machine key values are set on all the instances of a given cloud service,
            // thereby providing session persistence across different instances in the same deployment slot. However, across different slots(staging vs production)
            // these session keys are different. Thereby causing the loss of session upon a slot swap. Manually setting these values on role start ensures same
            // keys are used by all the instances across all the slots of a Azure cloud service. See more analysis here: https://github.com/NuGet/Engineering/issues/1329
            Trace.TraceInformation($"[{nameof(GalleryMachineKeyConfigurationProvider)}] Checking current gallery configuration.");
            if (config.EnableMachineKeyConfiguration
                && !string.IsNullOrWhiteSpace(config.MachineKeyDecryption)
                && !string.IsNullOrWhiteSpace(config.MachineKeyDecryptionKey)
                && !string.IsNullOrWhiteSpace(config.MachineKeyValidationAlgorithm)
                && !string.IsNullOrWhiteSpace(config.MachineKeyValidationKey))
            {
                Trace.TraceInformation($"[{nameof(GalleryMachineKeyConfigurationProvider)}] Using machine key settings from gallery configuration.");
                xmlDoc.DocumentElement.SetAttribute("decryptionKey", config.MachineKeyDecryptionKey);
                xmlDoc.DocumentElement.SetAttribute("decryption", config.MachineKeyDecryption);
                xmlDoc.DocumentElement.SetAttribute("validationKey", config.MachineKeyValidationKey);
                xmlDoc.DocumentElement.SetAttribute("validation", config.MachineKeyValidationAlgorithm);
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
