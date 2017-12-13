// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGetGallery.Auditing
{
    public class AuditActorObfuscator : JsonConverter
    {
        public AuditActorObfuscator()
        {
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(AuditActor);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            AuditActor actor = (AuditActor)value;
            JToken t = JToken.FromObject(Obfuscate(actor));
            t.WriteTo(writer);
        }

        private AuditActor Obfuscate(AuditActor actor)
        {
            if (actor == null)
            {
                return null;
            }
            return new AuditActor(actor.MachineName,
                Obfuscator.ObfuscateIp(actor.MachineIP),
                Obfuscator.ObfuscatedUserName,
                actor.AuthenticationType,
                actor.CredentialKey,
                actor.TimestampUtc,
                Obfuscate(actor.OnBehalfOf));
        }
    }
}
