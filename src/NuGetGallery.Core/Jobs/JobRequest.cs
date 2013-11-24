using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGetGallery.Jobs
{
    public class JobRequest
    {
        public static readonly string UnknownSource = "Unknown";

        public string Name { get; private set; }
        public string Source { get; private set; }
        public Guid Continuing { get; private set; }
        public Dictionary<string, string> Parameters { get; private set; }
        public CloudQueueMessage Message { get; private set; }
        public DateTimeOffset? ExpiresAt { get { return Message == null ? null : Message.ExpirationTime; } }
        
        public DateTimeOffset InsertionTime { get; private set; }
        public string Id { get; private set; }

        public JobRequest(string name, string source, Dictionary<string, string> parameters)
            : this(name, source, parameters, Guid.Empty, null)
        {
        }

        public JobRequest(string name, string source, Dictionary<string, string> parameters, Guid continuing)
            : this(name, source, parameters, continuing, null)
        {
        }

        public JobRequest(string name, string source, Dictionary<string, string> parameters, CloudQueueMessage message)
            : this(name, source, parameters, Guid.Empty, message)
        {
        }
        
        public JobRequest(string name, string source, Dictionary<string, string> parameters, Guid continuing, CloudQueueMessage message)
        {
            Name = name;
            Source = source;
            Parameters = parameters;
            Message = message;
            Continuing = continuing;

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

        // Manual serialization to ensure we're very flexible. Also so I can be obsessive about private setters :) - anurse
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

            var continuingProp = json.Property("continuing");
            Guid continuing = Guid.Empty;
            if (continuingProp != null)
            {
                if (continuingProp.Value.Type != JTokenType.String)
                {
                    throw new InvalidJobRequestException("'continuing' must be a JSON string");
                }
                else if (!Guid.TryParse(continuingProp.Value.Value<string>(), out continuing))
                {
                    throw new InvalidJobRequestException("'continuing' must be a valid Guid");
                }
            }

            return new JobRequest(name, source ?? UnknownSource, parameters, continuing, message);
        }

        public string Render()
        {
            var rendered = new JObject(
                new JProperty("name", Name),
                new JProperty("source", Source),
                new JProperty("parameters", new JObject(
                    Parameters.Select(pair => new JProperty(pair.Key, pair.Value)))));
            if (Continuing != Guid.Empty)
            {
                rendered.Add(new JProperty("continuing", Continuing.ToString("N").ToLowerInvariant()));
            }
            return rendered.ToString(Formatting.None);
        }
    }
}
