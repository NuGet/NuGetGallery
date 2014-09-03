using System;
using System.Data.SqlClient;
using Microsoft.WindowsAzure.Storage;
using System.Collections.Generic;
using System.Diagnostics;

namespace NuGet.Jobs.Common
{
    /// <summary>
    /// Keys to environment variables common across all jobs
    /// </summary>
    public static class EnvironmentVariableKeys
    {
        public const string SqlGallery = "NUGETJOBS_SQL_GALLERY";
        public const string SqlWarehouse = "NUGETJOBS_SQL_WAREHOUSE";
        public const string StorageGallery = "NUGETJOBS_STORAGE_GALLERY";
        public const string StoragePrimary = "NUGETJOBS_STORAGE_PRIMARY";
    }

    /// <summary>
    /// Keep the argument names as lower case for simple string match
    /// </summary>
    public static class JobArgumentNames
    {
        // Job argument names
        public const string Sleep = "Sleep";

        // Database argument names
        public const string SourceDatabase = "SourceDatabase";
        public const string DestinationDatabase = "DestinationDatabase";

        // Catalog argument names
        public const string CatalogStorage = "CatalogStorage";
        public const string CatalogPath = "CatalogPath";
        public const string CatalogPageSize = "CatalogPageSize";

        // Catalog Collector argument names
        public const string ChecksumCollectorBatchSize = "ChecksumCollectorBatchSize";
    }
    /// <summary>
    /// This class is used to retrieve and expose the known azure configuration settings 
    /// from Environment Variables
    /// </summary>
    public static class JobConfigManager
    {
        /// <summary>
        /// Parses the string[] of <c>args</c> passed into the job into a dictionary of string, string.
        /// Expects the string[] to be set of pairs of argumentName and argumentValue, where, argumentName start with a hyphen
        /// </summary>
        /// <param name="args">Arguments passed to the job via commandline or environment variable settings</param>
        /// <param name="jobName">Jobname to be used to infer environment variable settings</param>
        /// <returns>Returns a dictionary of arguments</returns>
        public static IDictionary<string, string> GetJobArgsDictionary(JobTraceLogger logger, string[] args, string jobName)
        {
            if (args.Length == 0)
            {
                var argsEnvVariable = "NUGETJOBS_ARGS_" + jobName;
                logger.Log(TraceLevel.Warning, "No command-line arguments provided. Picking it from Environment variable: " + argsEnvVariable);
                var argsArray = Environment.GetEnvironmentVariable(argsEnvVariable);
                if (String.IsNullOrEmpty(argsArray))
                {
                    logger.Log(TraceLevel.Warning, "No environment variable arguments provided either");
                }
                else
                {
                    args = argsArray.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                }
            }
            logger.Log(TraceLevel.Warning, "Number of arguments : " + args.Length);
            
            // For simplicity, there is strict limitation on how the job args can be
            // Arguments are expected to be a set of pairs, where each pair is of the form '-<argName> <argValue>'
            // So, Number of arguments should be even. And, every odd numbered argument which is an argName must start with a '-'
            if(args.Length % 2 != 0)
            {
                throw new ArgumentException("Number of arguments is not a multiple of 2. Arguments are expected to be a set of pairs, where each pair is of the form '-<argName> <argValue>'");
            }

            IDictionary<string, string> argsDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for(int i = 0; i < args.Length; i+=2)
            {
                if(!args[i].StartsWith("-"))
                {
                    throw new ArgumentException("Argument Name does not start with a hyphen ('-')");
                }

                var argName = args[i].Substring(1);
                var argValue = args[i+1];

                if(String.IsNullOrEmpty(argName))
                {
                    throw new ArgumentException("Argument Name is null or empty");
                }

                if(String.IsNullOrEmpty(argValue))
                {
                    throw new ArgumentException("Argument Value is null or empty");
                }

                argsDictionary.Add(argName, argValue);
            }

            return argsDictionary;
        }

        /// <summary>
        /// Get the argument from the dictionary <c>jobArgsDictionary</c> corresponding to <c>argName</c>.
        /// If not found, tries to get the value of environment variable for <c>envVariableKey</c>, if provided.
        /// If not found, throws ArgumentNullException
        /// </summary>
        /// <param name="jobArgsDictionary">This is the dictionary of commandline args passed to the exe</param>
        /// <param name="argName">Name of the argument for which value is needed</param>
        /// <param name="fallbackEnvVariable">Name of the environment variable to be used when the argName was not found in the dictionary</param>
        /// <returns>Returns the argument value as a string</returns>
        public static string GetArgument(IDictionary<string, string> jobArgsDictionary, string argName, string fallbackEnvVariable = null)
        {
            string argValue;
            if(!jobArgsDictionary.TryGetValue(argName, out argValue) && !String.IsNullOrEmpty(fallbackEnvVariable))
            {
                argValue = Environment.GetEnvironmentVariable(fallbackEnvVariable);
            }

            if (String.IsNullOrEmpty(argValue))
            {
                if (String.IsNullOrEmpty(fallbackEnvVariable))
                {
                    throw new ArgumentNullException(String.Format("Argument '{0}' was not passed", argName));
                }
                else
                {
                    throw new ArgumentNullException(String.Format("Argument '{0}' was not passed. And, environment variable '{1}' was not set", argName, fallbackEnvVariable));
                }
            }

            return argValue;
        }

        /// <summary>
        /// Just calls GetArgsOrEnvVariable, but does not throw, instead returns null
        /// </summary>
        /// <param name="jobArgsDictionary">This is the dictionary of commandline args passed to the exe</param>
        /// <param name="argName">Name of the argument for which value is needed</param>
        /// <param name="fallbackEnvVariable">Name of the environment variable to be used when the argName was not found in the dictionary</param>
        /// <returns>Returns the argument value as a string</returns>
        public static string TryGetArgument(IDictionary<string, string> jobArgsDictionary, string argName, string fallbackEnvVariable = null)
        {
            try
            {
                return GetArgument(jobArgsDictionary, argName, fallbackEnvVariable);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Just calls TryGetArgument, but returns an int, if parsable, otherwise, null
        /// </summary>
        /// <param name="jobArgsDictionary">This is the dictionary of commandline args passed to the exe</param>
        /// <param name="argName">Name of the argument for which value is needed</param>
        /// <param name="fallbackEnvVariable">Name of the environment variable to be used when the argName was not found in the dictionary</param>
        /// <returns>Returns the argument value as a string</returns>
        public static int? TryGetIntArgument(IDictionary<string, string> jobArgsDictionary, string argName, string fallbackEnvVariable = null)
        {
            int intArgument;
            string argumentString = TryGetArgument(jobArgsDictionary, argName, fallbackEnvVariable);
            if(!String.IsNullOrEmpty(argumentString) && Int32.TryParse(argumentString, out intArgument))
            {
                return intArgument;
            }
            return null;
        }
    }
}
