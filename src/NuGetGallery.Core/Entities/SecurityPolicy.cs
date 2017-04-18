// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuGetGallery
{
    public abstract class SecurityPolicy : IEntity
    {
        private string _value;

        [Required]
        [JsonIgnore]
        public int Key { get; set; }

        [Required]
        [JsonIgnore]
        public string Value
        {
            get
            {
                if (StateChanged)
                {
                    _value = SerializeValue();
                    StateChanged = false;
                }
                return _value;
            }
            set
            {
                _value = value;
                DeserializeValue(_value);
                StateChanged = false;
            }
        }

        [JsonIgnore]
        [NotMapped]
        public bool StateChanged { get; set; }

        protected abstract void UpdateState(SecurityPolicy policy);

        protected virtual void DeserializeValue(string value)
        {
            var policy = (SecurityPolicy)JsonConvert.DeserializeObject(value, GetType());
            UpdateState(policy);
        }

        protected virtual string SerializeValue()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
