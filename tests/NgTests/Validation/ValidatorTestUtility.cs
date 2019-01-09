// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NgTests.Infrastructure;
using NgTests.Validation;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Versioning;

namespace NgTests
{
    public static class ValidatorTestUtility
    {
        public static ValidatorConfiguration CreateValidatorConfig(
            string packageBaseAddress = "https://nuget.test/packages",
            bool requireRepositorySignature = false)
        {
            return new ValidatorConfiguration(packageBaseAddress, requireRepositorySignature);
        }

        public static IEnumerable<Tuple<T, T>> GetPairs<T>(IEnumerable<Func<T>> valueFactories)
        {
            var set = valueFactories;
            for (var factoryI = 0; factoryI < set.Count(); factoryI++)
            {
                for (var factoryJ = 0; factoryJ < set.Count(); factoryJ++)
                {
                    yield return Tuple.Create(set.ElementAt(factoryI)(), set.ElementAt(factoryJ)());
                }
            }
        }

        public static IEnumerable<Tuple<T, T>> GetBigraphPairs<T>(IEnumerable<Func<T>> valueFactories1, IEnumerable<Func<T>> valueFactories2)
        {
            return GetOneSidedBigraphPairs(valueFactories1, valueFactories2)
                .Concat(GetOneSidedBigraphPairs(valueFactories2, valueFactories1));
        }

        private static IEnumerable<Tuple<T, T>> GetOneSidedBigraphPairs<T>(IEnumerable<Func<T>> valueFactories1, IEnumerable<Func<T>> valueFactories2)
        {
            for (var factoryI = 0; factoryI < valueFactories1.Count(); factoryI++)
            {
                for (var factoryJ = 0; factoryJ < valueFactories2.Count(); factoryJ++)
                {
                    yield return Tuple.Create(valueFactories1.ElementAt(factoryI)(), valueFactories2.ElementAt(factoryJ)());
                }
            }
        }

        public static IEnumerable<Tuple<T, T, bool>> GetSpecialPairs<T>(IEnumerable<Func<Tuple<T, T, bool>>> pairFactories)
        {
            foreach (var pairFactory in pairFactories)
            {
                yield return pairFactory();
            }
        }

        public static IEnumerable<Tuple<T, T>> GetEqualPairs<T>(IEnumerable<Func<T>> valueFactories)
        {
            foreach (var factory in valueFactories)
            {
                yield return Tuple.Create(factory(), factory());
            }
        }

        public static IEnumerable<Tuple<T, T>> GetUnequalPairs<T>(IEnumerable<Func<T>> valueFactories)
        {
            var set = valueFactories;
            for (var factoryI = 0; factoryI < set.Count(); factoryI++)
            {
                for (var factoryJ = 0; factoryJ < set.Count(); factoryJ++)
                {
                    if (factoryI != factoryJ)
                    {
                        yield return Tuple.Create(set.ElementAt(factoryI)(), set.ElementAt(factoryJ)());
                    }
                }
            }
        }

        public static IEnumerable<T> GetImplementations<T>()
        {
            var types =
                Assembly.GetExecutingAssembly().GetTypes()
                    .Where(p =>
                        typeof(T)
                        .IsAssignableFrom(p)
                        && !p.IsAbstract);

            return types.Select(
                t => (T)t.GetConstructor(new Type[] { }).Invoke(null));
        }

        public static ValidationContext GetFakeValidationContext()
        {
            return ValidationContextStub.Create(new PackageIdentity("testPackage", new NuGetVersion(1, 0, 0)));
        }

        internal static void AddPackageToMockServer(MockServerHttpClientHandler clientHandler, PackageIdentity packageIdentity, string filePath)
        {
            string packageId = packageIdentity.Id.ToLowerInvariant();
            string packageVersion = packageIdentity.Version.ToNormalizedString().ToLowerInvariant();

            clientHandler.SetAction($"/packages/{packageId}/{packageVersion}/{packageId}.{packageVersion}.nupkg", request =>
            {
                byte[] bytes = File.ReadAllBytes(filePath);

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(bytes)
                });
            });
        }

        internal static void AddCatalogLeafToMockServer(MockServerHttpClientHandler clientHandler, Uri uri, CatalogLeaf leaf)
        {
            string relativeUrl = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.ToString();

            clientHandler.SetAction(relativeUrl, request =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(leaf))
                });
            });
        }
    }
}