using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace NuGet.Services.Storage
{
    public class JsonDictionarySerializer : TablePropertySerializer
    {
        public override void Write(string name, object value, IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            var dict = value as Dictionary<string, string>;
            if (dict != null)
            {
                properties[name] = EntityProperty.GeneratePropertyForString(JsonConvert.SerializeObject(dict));
            }
        }

        public override object Read(Type targetType, string name, IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            if (targetType == typeof(Dictionary<string, string>))
            {
                EntityProperty prop;
                if (properties.TryGetValue(name, out prop))
                {
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(prop.StringValue);
                }
                else
                {
                    return new Dictionary<string, string>();
                }
            }
            return null;
        }
    }
}
