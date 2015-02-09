using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Services.Metadata.Catalog.Ownership;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    public static class RegistrationTests
    {
        static string Primary = "DefaultEndpointsProtocol=https;AccountName=nugetjohtaylo;AccountKey=Vm1hvKk77IIKVWdQwoofq3TUFhe3HbKMSfK6ilimrXEnYIRK5zIvA4SDi/e1HXcAY2crC3R17BvpOE9bLp2/jA==";

        public static async Task Test0Async()
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(Primary);

            StorageFactory storageFactory = new AzureStorageFactory(account, "ownership");

            IRegistration registrationRecord = new StorageRegistration(storageFactory);

            await registrationRecord.AddOwner(new RegistrationId { Domain = "nuget.org", Id = "MyPackageA" }, "tom");
            await registrationRecord.AddOwner(new RegistrationId { Domain = "nuget.org", Id = "MyPackageA" }, "dick");
            await registrationRecord.AddOwner(new RegistrationId { Domain = "nuget.org", Id = "MyPackageA" }, "harry");

            await registrationRecord.AddOwner(new RegistrationId { Domain = "nuget.org", Id = "MyPackageB" }, "tom");
            await registrationRecord.AddOwner(new RegistrationId { Domain = "nuget.org", Id = "MyPackageB" }, "dick");

            await registrationRecord.AddOwner(new RegistrationId { Domain = "nuget.org", Id = "MyPackageC" }, "harry");

            await registrationRecord.Add(new PackageId { Domain = "nuget.org", Id = "MyPackageA", Version = NuGetVersion.Parse("1.0.0") });
            await registrationRecord.Add(new PackageId { Domain = "nuget.org", Id = "MyPackageA", Version = NuGetVersion.Parse("2.0.0") });
            await registrationRecord.Add(new PackageId { Domain = "nuget.org", Id = "MyPackageA", Version = NuGetVersion.Parse("3.0.0") });

            await registrationRecord.Add(new PackageId { Domain = "nuget.org", Id = "MyPackageB", Version = NuGetVersion.Parse("1.0.0") });

            await registrationRecord.Add(new PackageId { Domain = "nuget.org", Id = "MyPackageC", Version = NuGetVersion.Parse("1.0.0") });
            await registrationRecord.Add(new PackageId { Domain = "nuget.org", Id = "MyPackageC", Version = NuGetVersion.Parse("2.0.0") });
        }

        public static void Test0()
        {
            Test0Async().Wait();
        }

        public static async Task Test1Async()
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(Primary);

            StorageFactory storageFactory = new AzureStorageFactory(account, "ownership");

            IRegistration registrationRecord = new StorageRegistration(storageFactory);

            await registrationRecord.RemoveOwner(new RegistrationId { Domain = "nuget.org", Id = "MyPackageA" }, "tom");
        }

        public static void Test1()
        {
            Test1Async().Wait();
        }

        public static async Task Test2Async()
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(Primary);

            StorageFactory storageFactory = new AzureStorageFactory(account, "ownership");

            IRegistration registrationRecord = new StorageRegistration(storageFactory);

            await registrationRecord.RemoveOwner(new RegistrationId { Domain = "nuget.org", Id = "MyPackageA" }, "tom");
            await registrationRecord.RemoveOwner(new RegistrationId { Domain = "nuget.org", Id = "MyPackageA" }, "dick");
            await registrationRecord.RemoveOwner(new RegistrationId { Domain = "nuget.org", Id = "MyPackageA" }, "harry");
        }

        public static void Test2()
        {
            //Test0();
            //Test1();

            Test2Async().Wait();
        }

        public static async Task Test3Async()
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(Primary);

            StorageFactory storageFactory = new AzureStorageFactory(account, "ownership");

            IRegistration registrationRecord = new StorageRegistration(storageFactory);

            await registrationRecord.Remove(new PackageId { Domain = "nuget.org", Id = "MyPackageC", Version = NuGetVersion.Parse("1.0.0") });
            await registrationRecord.Remove(new PackageId { Domain = "nuget.org", Id = "MyPackageC", Version = NuGetVersion.Parse("2.0.0") });
        }

        public static void Test3()
        {
            Test3Async().Wait();
        }

        public static async Task Test4Async()
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(Primary);

            StorageFactory storageFactory = new AzureStorageFactory(account, "ownership");

            IRegistration registrationRecord = new StorageRegistration(storageFactory);

            await registrationRecord.Remove(new RegistrationId { Domain = "nuget.org", Id = "MyPackageC" });
        }

        public static void Test4()
        {
            Test4Async().Wait();
        }

        public static async Task Test5Async()
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(Primary);

            StorageFactory storageFactory = new AzureStorageFactory(account, "ownership");

            IRegistration registrationRecord = new StorageRegistration(storageFactory);

            if (await registrationRecord.Exists(new RegistrationId { Domain = "nuget.org", Id = "MyPackageC" }))
            {
                throw new Exception("shouldn't exist");
            }
            if (!await registrationRecord.Exists(new RegistrationId { Domain = "nuget.org", Id = "MyPackageB" }))
            {
                throw new Exception("should exist");
            }
            if (await registrationRecord.Exists(new PackageId { Domain = "nuget.org", Id = "MyPackageC", Version = NuGetVersion.Parse("1.0.0") }))
            {
                throw new Exception("shouldn't exist");
            }
            if (await registrationRecord.Exists(new PackageId { Domain = "nuget.org", Id = "MyPackageB", Version = NuGetVersion.Parse("3.0.0") }))
            {
                throw new Exception("shouldn't exist");
            }
            if (!await registrationRecord.Exists(new PackageId { Domain = "nuget.org", Id = "MyPackageB", Version = NuGetVersion.Parse("1.0.0") }))
            {
                throw new Exception("should exist");
            }

            Console.WriteLine("all ok");
        }

        public static void Test5()
        {
            Test5Async().Wait();
        }

        public static async Task Test6Async()
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(Primary);

            StorageFactory storageFactory = new AzureStorageFactory(account, "ownership");

            IRegistration registrationRecord = new StorageRegistration(storageFactory);

            for (int i = 0; i < 100; i++)
            {
                await registrationRecord.AddOwner(new RegistrationId { Domain = "nuget.org", Id = string.Format("MyPackage-{0}", i) }, "tom");
                await registrationRecord.AddOwner(new RegistrationId { Domain = "nuget.org", Id = string.Format("MyPackage-{0}", i) }, "dick");
                await registrationRecord.AddOwner(new RegistrationId { Domain = "nuget.org", Id = string.Format("MyPackage-{0}", i) }, "harry");
            }
        }

        public static void Test6()
        {
            Test6Async().Wait();
        }
    }
}
