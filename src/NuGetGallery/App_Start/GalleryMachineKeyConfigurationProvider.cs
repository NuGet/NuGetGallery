// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Configuration;
using System.Web.Mvc;
using System.Xml;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class GalleryMachineKeyConfigurationProvider : ProtectedConfigurationProvider
    {
        public override XmlNode Decrypt(XmlNode encryptedNode)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.XmlResolver = null;
            xmlDoc.AppendChild(xmlDoc.CreateElement(string.Empty, "machineKey", string.Empty));

            // The machine keys are used for encrypting/decrypting cookies used by ASP.NET, these are usually set by IIS in 'Auto' mode. 
            // During a deployment to Azure cloud service the same machine key values are set on all the instances of a given cloud service,
            // thereby providing session persistence across different instances in the same deployment slot. However, across different slots(staging vs production)
            // these session keys are different. Thereby causing the loss of session upon a slot swap. Manually setting these values on role start ensures same
            // keys are used by all the instances across all the slots of a Azure cloud service. See more analysis here: https://github.com/NuGet/Engineering/issues/1329
            var config = DependencyResolver.Current.GetService<IGalleryConfigurationService>();
            if (config.Current.EnableMachineKeyConfiguration
                && !string.IsNullOrWhiteSpace(config.Current.MachineKeyDecryption)
                && !string.IsNullOrWhiteSpace(config.Current.MachineKeyDecryptionKey)
                && !string.IsNullOrWhiteSpace(config.Current.MachineKeyValidationAlgorithm)
                && !string.IsNullOrWhiteSpace(config.Current.MachineKeyValidationKey))
            {
                xmlDoc.DocumentElement.SetAttribute("decryptionKey", config.Current.MachineKeyDecryptionKey);
                xmlDoc.DocumentElement.SetAttribute("decryption", config.Current.MachineKeyDecryption);
                xmlDoc.DocumentElement.SetAttribute("validationKey", config.Current.MachineKeyValidationKey);
                xmlDoc.DocumentElement.SetAttribute("validation", config.Current.MachineKeyValidationAlgorithm);
            }

            return xmlDoc.DocumentElement;
        }

        public override XmlNode Encrypt(XmlNode node)
        {
            throw new NotImplementedException();
        }
    }
}
