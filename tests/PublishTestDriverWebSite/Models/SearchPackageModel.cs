using Newtonsoft.Json.Linq;
using PublishTestDriverWebSite.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PublishTestDriverWebSite.Models
{
    public class SearchPackageModel
    {
        public SearchPackageModel(JObject searchResult)
        {
            Domain = JsonUtils.Get(searchResult, "domain");
            Registration = JsonUtils.Get(searchResult, "registration");
            Description = JsonUtils.Get(searchResult, "description");
            Summary = JsonUtils.Get(searchResult, "summary");
            Title = JsonUtils.Get(searchResult, "title");
            Id = JsonUtils.Get(searchResult, "id");
            IconUrl = JsonUtils.Get(searchResult, "iconUrl");
            Version = JsonUtils.Get(searchResult, "version");
            Visibility = JsonUtils.Get(searchResult, "visibility");
            Tags = JsonUtils.GetList(searchResult, "tags");
            Authors = JsonUtils.GetList(searchResult, "authors");

            Versions = new List<SearchPackageVersionModel>();
            JToken versionsToken;
            if (searchResult.TryGetValue("versions", out versionsToken))
            {
                foreach (JToken item in (JArray)versionsToken)
                {
                    Versions.Add(new SearchPackageVersionModel((JObject)item));
                }
            }
        }

        public string Domain { get; private set; }
        public string Registration { get; private set; }
        public string Description { get; private set; }
        public string Summary { get; private set; }
        public string Title { get; private set; }
        public string Id { get; private set; }
        public string IconUrl { get; private set; }
        public string Visibility { get; private set; }
        public List<string> Tags { get; private set; }
        public List<string> Authors { get; private set; }
        public string Version { get; private set; }
        public List<SearchPackageVersionModel> Versions { get; private set; }
    }
}