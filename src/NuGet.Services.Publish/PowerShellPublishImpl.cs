using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using NuGet.Services.Metadata.Catalog;
using VDS.RDF.Parsing.Contexts;

namespace NuGet.Services.Publish
{
    public class PowerShellPublishImpl : PublishImpl
    {
        private bool _wasPsd1Present = false;
        private List<string> _missingRequiredFields = new List<string>();
        private Exception _exception;

        public PowerShellPublishImpl(IRegistrationOwnership registrationOwnership) : base(registrationOwnership)
        {
        }

        /// <summary>
        /// Checks whether the file is a top-level PowerShell module manifest from which we will extract metadata
        /// </summary>
        /// <param name="fullName">File name</param>
        /// <returns></returns>
        protected override bool IsMetadataFile(string fullName)
        {
            var isMetadataFile = (fullName.EndsWith(".psd1", StringComparison.OrdinalIgnoreCase) && String.Equals(Path.GetFileName(fullName), fullName, StringComparison.OrdinalIgnoreCase));
            _wasPsd1Present = _wasPsd1Present || isMetadataFile;

            return isMetadataFile;
        }

        /// <summary>
        /// Extracts PowerShell metadata from psd1 stream
        /// </summary>
        /// <param name="fullname">File name</param>
        /// <param name="stream">Contents of module manifest</param>
        /// <returns>JSON metadata collection</returns>
        protected override JObject CreateMetadataObject(string fullname, Stream stream)
        {
            try
            {
                StreamReader reader = new StreamReader(stream);

                dynamic result = new JObject();

                Token[] tokens;
                ParseError[] errors;
                var scriptBlockAst = Parser.ParseInput(reader.ReadToEnd(), out tokens, out errors);

                HashtableAst hashtableAst = null;

                if (scriptBlockAst != null)
                {
                    var endBlock = scriptBlockAst.EndBlock;
                    if (endBlock != null)
                    {
                        var statements = endBlock.Statements;
                        if (statements != null && statements.Count > 0)
                        {
                            var pipelineAst = statements[0] as PipelineAst;
                            if (pipelineAst != null)
                            {
                                var pipelineElements = pipelineAst.PipelineElements;
                                if (pipelineElements != null && pipelineElements.Count > 0)
                                {
                                    var commandExpressionAst = pipelineElements[0] as CommandExpressionAst;
                                    if (commandExpressionAst != null)
                                    {
                                        hashtableAst = commandExpressionAst.Expression as HashtableAst;
                                    }
                                }
                            }
                        }
                    }
                }

                var hashtable = PowerShellHashtableVisitor.GetHashtable(hashtableAst);

                VerifyRequiredFields(hashtable);

                if (hashtable != null)
                {
                    result.authors = ConvertObjectToJArray(hashtable["Author"]);
                    result.ModuleVersion = hashtable["ModuleVersion"];
                    result.CompanyName = hashtable["CompanyName"];
                    result.GUID = hashtable["GUID"];
                    result.PowerShellHostVersion = hashtable["PowerShellHostVersion"];
                    result.DotNetFrameworkVersion = hashtable["DotNetFrameworkVersion"];
                    result.CLRVersion = hashtable["CLRVersion"];
                    result.ProcessorArchitecture = hashtable["ProcessorArchitecture"];
                    result.CmdletsToExport = ConvertObjectToJArray(hashtable["CmdletsToExport"]);
                    result.FunctionsToExport = ConvertObjectToJArray(hashtable["FunctionsToExport"]);
                    result.DscResourcesToExport = ConvertObjectToJArray(hashtable["DscResourcesToExport"]);

                    var privateData = hashtable["PrivateData"] as Hashtable;
                    result.licenseUrl = null;
                    result.iconUrl = null;
                    result.tags = ConvertObjectToJArray(null);
                    result.projectUrl = null;
                    result.releaseNotes = null;

                    if (privateData != null)
                    {
                        var psData = privateData["PSData"] as Hashtable;
                        if (psData != null)
                        {
                            result.licenseUrl = psData["LicenseUri"];
                            result.iconUrl = psData["IconUri"];
                            result.tags = ConvertObjectToJArray(psData["Tags"]);
                            result.projectUrl = psData["ProjectUri"];
                            result.releaseNotes = psData["ReleaseNotes"];
                        }
                    }
                }

                return result;
            }
            catch (UnexpectedElementException ex)
            {
                _exception = ex;
                return null;
            }
        }

        private void VerifyRequiredFields(Hashtable hashtable)
        {
            if (hashtable == null || String.IsNullOrEmpty(hashtable["ModuleVersion"] as string))
            {
                _missingRequiredFields.Add("ModuleVersion");
            }

            if (hashtable == null || String.IsNullOrEmpty(hashtable["Author"] as string))
            {
                _missingRequiredFields.Add("Author");
            }

            if (hashtable == null || String.IsNullOrEmpty(hashtable["GUID"] as string))
            {
                _missingRequiredFields.Add("GUID");
            }
        }

        protected override Uri GetItemType()
        {
            return Schema.DataTypes.PowerShellPackage;
        }

        /// <summary>
        /// Creates a JArray from an object, if null creates an empty JArray
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        private object ConvertObjectToJArray(object o)
        {
            if (o != null)
            {
                return new JArray(o);
            }
            else
            {
                return new JArray();
            }
        }

        /// <summary>
        /// Rejects the module if no PowerShell module manifest was present
        /// </summary>
        /// <param name="nupkgStream"></param>
        /// <returns></returns>
        protected override IList<string> Validate(Stream nupkgStream)
        {
            if (!_wasPsd1Present)
            {
                return new List<string>() { "A psd1 PowerShell module manifest was missing." };
            }

            if (_missingRequiredFields.Count > 0)
            {
                return new List<string>() { String.Format("The psd1 PowerShell module manifest is missing required fields: {0}.", String.Join(", ", _missingRequiredFields)) };
            }

            if (_exception != null)
            {
                return new List<string>() { _exception.Message };
            }

            return null;
        }
    }
}