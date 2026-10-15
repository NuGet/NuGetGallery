// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// A stable signing identity that survives certificate rotation, such as the Durable Identity Value that Azure
    /// Artifact Signing places in an EKU of every certificate it issues for a validated identity.
    /// </summary>
    [DisplayColumn(nameof(Value))]
    public class DurableIdentityValue : IEntity
    {
        public DurableIdentityValue()
        {
            UserDurableIdentityValues = new List<UserDurableIdentityValue>();
            Certificates = new List<Certificate>();
        }

        public int Key { get; set; }

        /// <summary>
        /// The Durable Identity Value, which is an OID string such as
        /// 1.3.6.1.4.1.311.97.990309390.766961637.194916062.941502583.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// The subject distinguished name of the most recent certificate seen with this value.
        /// </summary>
        public string Subject { get; set; }

        public string ShortSubject { get; set; }

        /// <summary>
        /// The issuer distinguished name of the most recent certificate seen with this value.
        /// </summary>
        public string Issuer { get; set; }

        public string ShortIssuer { get; set; }

        public virtual ICollection<UserDurableIdentityValue> UserDurableIdentityValues { get; set; }

        public virtual ICollection<Certificate> Certificates { get; set; }
    }
}
