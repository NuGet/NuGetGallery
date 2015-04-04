using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace NuGet.Indexing
{
    public abstract class FrameworksList
    {
        public static readonly FrameworkName AnyFramework = new FrameworkName("Any", new Version(0, 0));
        public static readonly string FileName = "projectframeworks.v1.json";

        public static FrameworksList Empty = new EmptyFrameworksList();

        public abstract string Path { get; }
        protected abstract JObject LoadJson();

        public IList<FrameworkName> Load()
        {
            JObject obj = LoadJson();
            if (obj == null)
            {
                return new List<FrameworkName>();
            }
            var data = obj.Value<JArray>("data");
            var list = data.Select(t => new FrameworkName(t.ToString())).ToList();
            list.Add(FrameworksList.AnyFramework);
            return list;
        }

        private class EmptyFrameworksList : FrameworksList
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
