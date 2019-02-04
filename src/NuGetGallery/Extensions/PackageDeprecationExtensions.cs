// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public static class PackageDeprecationExtensions
    {
        public static IEnumerable<string> GetCVEIds(this PackageDeprecation deprecation)
        {
            return GetIds<CVEIdList, IEnumerable<string>>(
                deprecation,
                CVEIdList.SchemaVersion,
                d => d.CVEIds,
                l => l.CVEIds);
        }

        public static void SetCVEIds(this PackageDeprecation deprecation, IEnumerable<string> cveIds)
        {
            SetIds(
                deprecation,
                cveIds,
                (ids) => new CVEIdList(ids),
                (d, s) => d.CVEIds = s);
        }

        private class CVEIdList : IIdList
        {
            public static int SchemaVersion = 1;

            public CVEIdList()
            {
            }

            public CVEIdList(IEnumerable<string> cveIds)
            {
                Version = SchemaVersion;
                CVEIds = cveIds;
            }

            public int Version { get; set; }
            public IEnumerable<string> CVEIds { get; set; }
        }

        public static IEnumerable<string> GetCWEIds(this PackageDeprecation deprecation)
        {
            return GetIds<CWEIdList, IEnumerable<string>>(
                deprecation,
                CWEIdList.SchemaVersion,
                d => d.CWEIds,
                l => l.CWEIds);
        }

        public static void SetCWEIds(this PackageDeprecation deprecation, IEnumerable<string> cweIds)
        {
            SetIds(
                deprecation,
                cweIds,
                (ids) => new CWEIdList(ids),
                (d, s) => d.CWEIds = s);
        }

        private class CWEIdList : IIdList
        {
            public static int SchemaVersion = 1;

            public CWEIdList()
            {
            }

            public CWEIdList(IEnumerable<string> cweIds)
            {
                Version = SchemaVersion;
                CWEIds = cweIds;
            }

            public int Version { get; set; }
            public IEnumerable<string> CWEIds { get; set; }
        }

        private interface IIdList
        {
            int Version { get; }
        }

        private static TReturn GetIds<TDeserializedIdList, TReturn>(
            PackageDeprecation deprecation,
            int currentSchemaVersion,
            Func<PackageDeprecation, string> getSerialized,
            Func<TDeserializedIdList, TReturn> getReturn)
            where TDeserializedIdList : IIdList
            where TReturn : class
        {
            var serialized = getSerialized(deprecation);
            if (serialized == null)
            {
                return null;
            }

            var list = JsonConvert.DeserializeObject<TDeserializedIdList>(serialized);
            if (list.Version != currentSchemaVersion)
            {
                return null;
            }

            return getReturn(list);
        }

        private static void SetIds<TDeserializedIdList, TInput>(
            PackageDeprecation deprecation,
            TInput input,
            Func<TInput, TDeserializedIdList> getDeserialized,
            Action<PackageDeprecation, string> setSerialized)
        {
            string serialized;
            if (input != null)
            {
                var list = getDeserialized(input);
                serialized = JsonConvert.SerializeObject(list);
            }
            else
            {
                serialized = null;
            }

            setSerialized(deprecation, serialized);
        }
    }
}