// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Auditing
{
    public class AuditActor
    {
        public AuditActor(string userName, string authenticationType)
            : this(userName, authenticationType, null, null)
        {
        }

        private AuditActor(string userName, string authenticationType, string machineName, AuditActor onBehalfOf)
        {
            MachineName = machineName;
            UserName = userName;
            AuthenticationType = authenticationType;
            TimestampUtc = DateTime.UtcNow;
            OnBehalfOf = onBehalfOf;
        }

        public string MachineName { get; set; }
        public string UserName { get; set; }
        public string AuthenticationType { get; set; }
        public DateTime TimestampUtc { get; set; }
        public AuditActor OnBehalfOf { get; set; }

        public static AuditActor GetCurrentMachineActor()
        {
            return GetCurrentMachineActor(null);
        }

        public static AuditActor GetCurrentMachineActor(AuditActor onBehalfOf)
        {
            return new AuditActor(
                String.Format(@"{0}\{1}", Environment.UserDomainName, Environment.UserName),
                "MachineUser",
                Environment.MachineName,
                onBehalfOf);
        }
    }
}
