using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace NuGetGallery.Backend
{
    public class JobRequest
    {
        public string Name { get; private set; }
        public Dictionary<string, string> Parameters { get; private set; }

        public JobRequest(string name, Dictionary<string, string> parameters)
        {
            Name = name;
            Parameters = parameters;
        }

        public static JobRequest Parse(string requestString)
        {
            return Parse(JObject.Parse(requestString));
        }

        public static JobRequest Parse(JObject json)
        {
            var nameProp = json.Property("name");
            if (nameProp == null)
            {
                throw new InvalidJobRequestException("Missing 'name' property");
            }
            else if (nameProp.Value.Type != JTokenType.String)
            {
                throw new InvalidJobRequestException("'name' property must have string value");
            }
            name = nameProp.Value.Value<string>();

            var parametersProp = json.Property("parameters");
            if (parametersProp != null)
            {
                if (parametersProp.Value.Type != JTokenType.Object)
                {
                    throw new InvalidJobRequestException("'parameters' must be a JSON object");
                }
                foreach (var prop in ((JObject)parametersProp.Value).Properties())
                {
                    parameters[prop.Name] = prop.Value.Value<string>();
                }
            }

            return new JobRequest(name, parameters);
        }
    }
}
