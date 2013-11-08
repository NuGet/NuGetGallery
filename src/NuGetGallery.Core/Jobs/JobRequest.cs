using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;

namespace NuGetGallery.Jobs
{
    public class JobRequest
    {
        public static readonly string UnknownSource = "<unknown>";

        public string Name { get; private set; }
        public string Source { get; private set; }
        public Dictionary<string, string> Parameters { get; private set; }
        public CloudQueueMessage Message { get; private set; }
        public DateTimeOffset? ExpiresAt { get { return Message == null ? null : Message.ExpirationTime; } }
        
        public DateTimeOffset InsertionTime { get; private set; }
        public string Id { get; private set; }

        public JobRequest(string name, string source, Dictionary<string, string> parameters)
            : this(name, source, parameters, null)
        {
        }

        public JobRequest(string name, string source, Dictionary<string, string> parameters, CloudQueueMessage message)
        {
            Name = name;
            Source = source;
            Parameters = parameters;
            Message = message;

            if (message == null)
            {
                Id = Guid.NewGuid().ToString();
                InsertionTime = DateTimeOffset.UtcNow;
            }
            else
            {
                Id = message.Id;
                InsertionTime = message.InsertionTime ?? DateTimeOffset.UtcNow;
            }
        }

        public static JobRequest Parse(string requestString)
        {
            return Parse(JObject.Parse(requestString), message: null);
        }

        public static JobRequest Parse(string requestString, CloudQueueMessage message)
        {
            return Parse(JObject.Parse(requestString), message);
        }

        public static JobRequest Parse(JObject json)
        {
            return Parse(json, message: null);
        }

        public static JobRequest Parse(JObject json, CloudQueueMessage message)
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
            string name = nameProp.Value.Value<string>();

            var sourceProp = json.Property("source");
            string source = null;
            if (sourceProp != null)
            {
                if (sourceProp.Value.Type != JTokenType.String)
                {
                    throw new InvalidJobRequestException("'source' property must have string value");
                }
                source = sourceProp.Value.Value<string>();
            }

            var parametersProp = json.Property("parameters");
            Dictionary<string, string> parameters = new Dictionary<string, string>();
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

            return new JobRequest(name, source ?? UnknownSource, parameters, message);
        }
    }
}
