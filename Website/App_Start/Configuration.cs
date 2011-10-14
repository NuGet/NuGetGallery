using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Mail;
using System.Web;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGetGallery
{
    public class Configuration : IConfiguration
    {
        static readonly Dictionary<string, Lazy<object>> configThunks = new Dictionary<string, Lazy<object>>();

        public static string ReadAppSetting(string key)
        {
            var value = ConfigurationManager.AppSettings[key];
            return value;
        }

        public static string ReadAzureSetting(string key)
        {
            if (!RoleEnvironment.IsAvailable)
                return null;

            var value = RoleEnvironment.GetConfigurationSettingValue(key);
            return value;
        }

        public static string ReadConfiguration(string key)
        {
            return ReadConfiguration<string>(
                key,
                value => value);
        }

        public static T ReadConfiguration<T>(
            string key,
            Func<string, T> valueThunk)
        {
            if (!configThunks.ContainsKey(key))
                configThunks.Add(key, new Lazy<object>(() =>
                {
                    string value = null;

                    try
                    {
                        value = ReadAzureSetting(key);
                    }
                    catch (RoleEnvironmentException) { }

                    if (value == null)
                        value = ReadAppSetting(key);
                    return valueThunk(value);
                }));

            return (T)configThunks[key].Value;
        }

        public string AzureStorageAccessKey
        {
            get
            {
                return ReadConfiguration("AzureStorageAccessKey");
            }
        }

        public string AzureStorageAccountName
        {
            get
            {
                return ReadConfiguration("AzureStorageAccountName");
            }
        }

        public string AzureStorageBlobUrl
        {
            get
            {
                return ReadConfiguration("AzureStorageBlobUrl");
            }
        }

        public bool ConfirmEmailAddresses
        {
            get
            {
                return ReadConfiguration<bool>(
                    "ConfirmEmailAddresses",
                    (value) => bool.Parse(value ?? bool.TrueString));
            }
        }

        public MailAddress GalleryOwnerEmailAddress
        {
            get
            {
                return ReadConfiguration<MailAddress>(
                    "GalleryOwnerEmail",
                    (value) => new MailAddress(value));
            }
        }

        public string PackageFileDirectory
        {
            get
            {
                return ReadConfiguration<string>(
                    "PackageFileDirectory",
                    (value) => value ?? HttpContext.Current.Server.MapPath("~/App_Data/Packages"));
            }
        }

        public PackageStoreType PackageStoreType
        {
            get
            {
                return ReadConfiguration<PackageStoreType>(
                    "PackageStoreType",
                    (value) => (PackageStoreType)Enum.Parse(typeof(PackageStoreType), value ?? PackageStoreType.NotSpecified.ToString()));
            }
        }

        public string SmtpHost
        {
            get
            {
                return ReadConfiguration("SmtpHost");
            }
        }

        public string SmtpPassword
        {
            get
            {
                return ReadConfiguration("SmtpPassword");
            }
        }

        public int SmtpPort
        {
            get
            {
                return ReadConfiguration<int>(
                    "SmtpPort",
                    (value) => int.Parse(value));
            }
        }

        public string SmtpUsername
        {
            get
            {
                return ReadConfiguration("SmtpUsername");
            }
        }

        public bool UseSmtp
        {
            get
            {
                return ReadConfiguration<bool>(
                    "UseSmtp",
                    (value) => bool.Parse(value ?? bool.FalseString));
            }
        }
    }
}