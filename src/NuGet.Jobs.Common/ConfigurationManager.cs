using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
        public const string Once = "Once";
        public const string Sleep = "Sleep";

        // Database argument names
        public const string SourceDatabase = "SourceDatabase";
        public const string DestinationDatabase = "DestinationDatabase";
        public const string PackageDatabase = "PackageDatabase";

        // Storage Argument names
        public const string TargetStorageAccount = "TargetStorageAccount";
        public const string TargetStoragePath = "TargetStoragePath";

        // Catalog argument names
        public const string CatalogStorage = "CatalogStorage";
        public const string CatalogPath = "CatalogPath";
        public const string CatalogPageSize = "CatalogPageSize";
        public const string CatalogIndexUrl = "CatalogIndexUrl";
        public const string CatalogIndexPath = "CatalogIndexPath";
        public const string DontStoreCursor = "DontStoreCursor";

        // Catalog Collector argument names
        public const string ChecksumCollectorBatchSize = "ChecksumCollectorBatchSize";

        // Target Argument names
        public const string TargetBaseAddress = "TargetBaseAddress";
        public const string TargetLocalDirectory = "TargetLocalDirectory";

        // Other Argument names
        public const string CdnBaseAddress = "CdnBaseAddress";
        public const string GalleryBaseAddress = "GalleryBaseAddress";
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
        /// <param name="argsList">Arguments passed to the job via commandline or environment variable settings</param>
        /// <param name="jobName">Jobname to be used to infer environment variable settings</param>
        /// <returns>Returns a dictionary of arguments</returns>
        public static IDictionary<string, string> GetJobArgsDictionary(JobTraceLogger logger, string[] commandLineArgs, string jobName)
        {
            var allArgsList = commandLineArgs.ToList();
            if (allArgsList.Count == 0)
            {
                Trace.TraceInformation("No command-line arguments provided. Trying to pick up from environment variable for the job...");
            }

            string argsEnvVariable = "NUGETJOBS_ARGS_" + jobName;
            string envArgString = Environment.GetEnvironmentVariable(argsEnvVariable);
            if (String.IsNullOrEmpty(envArgString))
            {
                Trace.TraceWarning("No environment variable for the job arguments was provided");
            }
            else
            {
                allArgsList.AddRange(envArgString.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            }
            Trace.TraceInformation("Total number of arguments : " + allArgsList.Count);

            // Arguments are expected to be a set of pairs, where each pair is of the form '-<argName> <argValue>'
            // Or, in singles as a switch '-<switch>'

            IDictionary<string, string> argsDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for(int i = 0; i < allArgsList.Count; i++)
            {
                if(!allArgsList[i].StartsWith("-"))
                {
                    throw new ArgumentException("Argument Name does not start with a hyphen ('-')");
                }

                var argName = allArgsList[i].Substring(1);
                if(String.IsNullOrEmpty(argName))
                {
                    throw new ArgumentException("Argument Name is null or empty");
                }

                var nextString = allArgsList.Count > i + 1 ? allArgsList[i + 1] : null;
                if(String.IsNullOrEmpty(nextString) || nextString.StartsWith("-"))
                {
                    // If the key already exists, don't add. This means that first added value is preferred
                    // Since command line args are added before args from environment variable, this is the desired behavior
                    if (!argsDictionary.ContainsKey(argName))
                    {
                        // nextString startWith hyphen, the current one is a switch
                        argsDictionary.Add(argName, Boolean.TrueString);
                    }
                }
                else
                {
                    var argValue = nextString;
                    if (String.IsNullOrEmpty(argValue))
                    {
                        throw new ArgumentException("Argument Value is null or empty");
                    }

                    // If the key already exists, don't add. This means that first added value is preferred
                    // Since command line args are added before args from environment variable, this is the desired behavior
                    if (!argsDictionary.ContainsKey(argName))
                    {
                        argsDictionary.Add(argName, argValue);
                    }
                    i++; // skip next string since it was added as an argument value
                }
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

        /// <summary>
        /// Just calls TryGetArgument, but returns an bool, if parsable, otherwise, false
        /// </summary>
        /// <param name="jobArgsDictionary">This is the dictionary of commandline args passed to the exe</param>
        /// <param name="argName">Name of the argument for which value is needed</param>
        /// <param name="fallbackEnvVariable">Name of the environment variable to be used when the argName was not found in the dictionary</param>
        /// <returns>Returns the argument value as a string</returns>
        public static bool TryGetBoolArgument(IDictionary<string, string> jobArgsDictionary, string argName, string fallbackEnvVariable = null)
        {
            bool switchValue;
            string argumentString = TryGetArgument(jobArgsDictionary, argName, fallbackEnvVariable);
            if (!String.IsNullOrEmpty(argumentString) && Boolean.TryParse(argumentString, out switchValue))
            {
                return switchValue;
            }
            return false;
        }
    }
}
