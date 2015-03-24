using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace NuGet.Indexing
{
    public abstract class FrameworkCompatibility
    {
        public static readonly FrameworkName AnyFramework = new FrameworkName("Any", new Version(0, 0));
        public static readonly string FileName = "frameworkCompat.v1.json";

        public static FrameworkCompatibility Empty = new EmptyFrameworkCompatibility();

        public abstract string Path { get; }
        protected abstract JObject LoadJson();

        public IDictionary<string,ISet<string>> Load()
        {
            JObject obj = LoadJson();
            
            Dictionary<string,ISet<string>> dict = new Dictionary<string, ISet<string>>();
            if (obj == null)
            {
                return dict;
            }

            var data = obj.Value<JObject>("data");

            foreach (var val in data)
            {
                dict[val.Key] = new HashSet<string>(((IDictionary<string, JToken>)val.Value).Select(x => x.Key));
            }

            return dict;
        }

        private class EmptyFrameworkCompatibility : FrameworkCompatibility
        {
            public override string Path
            {
                get { return "<empty>"; }
            }

            protected override JObject LoadJson()
            {
                return null;
            }
        }
    }
}
