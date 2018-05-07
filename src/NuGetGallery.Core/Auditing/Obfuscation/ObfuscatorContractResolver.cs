// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace NuGetGallery.Auditing.Obfuscation
{
    public class ObfuscatorContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            var obfuscateAttribute = member.GetCustomAttribute<ObfuscateAttribute>();
            if (obfuscateAttribute != null)
            {
                property.Converter = new ObfuscatorJsonConverter(obfuscateAttribute.ObfuscationType);
            }

            return property;
        }
    }
}
