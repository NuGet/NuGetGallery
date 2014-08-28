using System;
using System.Data.SqlClient;
using Microsoft.WindowsAzure.Storage;

namespace NuGet.Jobs.Common
{
    /// <summary>
    /// Keys to environment variables common across all jobs
    /// </summary>
    public static class EnvironmentVariableKeys
    {
        public const string SqlGallery = "NUGETJOBS_SQL_GALLERY";
        public const string StorageGallery = "NUGETJOBS_STORAGE_GALLERY";
    }
    /// <summary>
    /// This class is used to retrieve and expose the known azure configuration settings 
    /// from Environment Variables
    /// </summary>
    public class Configuration
    {
        private SqlConnectionStringBuilder _SqlGallery;
        private CloudStorageAccount _StorageGallery;
        public Configuration() { }

        public static string[] GetJobArgs(string[] args, string jobName)
        {
            if (args.Length == 0)
            {
                var argsEnvVariable = "NUGETJOBS_" + jobName + "_ARGS";
                Console.WriteLine("No command-line arguments provided. Picking it from Environment variable: " + argsEnvVariable);
                var argsArray = Environment.GetEnvironmentVariable(argsEnvVariable);
                if (String.IsNullOrEmpty(argsArray))
                {
                    throw new ArgumentException("Command-line parameters are not provided. And, the following env variable is not set either: " + argsEnvVariable);
                }

                args = argsArray.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }
            Console.WriteLine("Number of arguments : " + args.Length);
            return args;
        }

        public static string GetValueOrThrow(string environmentVariableKey)
        {
            var envValue = Environment.GetEnvironmentVariable(environmentVariableKey);
            if (String.IsNullOrEmpty(envValue))
            {
                throw new ArgumentNullException(environmentVariableKey + " cannot be null or empty");
            }

            return envValue;
        }
        
        public SqlConnectionStringBuilder SqlGallery
        {
            get
            {
                if(_SqlGallery == null)
                {
                    _SqlGallery = new SqlConnectionStringBuilder(GetValueOrThrow(EnvironmentVariableKeys.SqlGallery));
                }
                return _SqlGallery;
            }
        }
        
        public CloudStorageAccount StorageGallery
        {
            get
            {
                if(_StorageGallery == null)
                {
                    _StorageGallery = CloudStorageAccount.Parse(GetValueOrThrow(EnvironmentVariableKeys.StorageGallery));
                }
                return _StorageGallery;
            }
        }
    }
}
