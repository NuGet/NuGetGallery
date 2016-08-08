// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data;
using System.Data.SqlClient;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;

namespace Search.GenerateAuxiliaryData
{
    class RankingsExporter
        : SqlExporter
    {
        private string _rankingCountParameterName = "@RankingCount";
        private int _rankingCount = 250;

        private string _projectGuidParameter = "@ProjectGuid";

        private string _colPackageId = "PackageId";
        private string _colProjectType = "ProjectType";

        public string RankingsTotalScript { get; }
        public string RankingsProjectTypesScript { get; }
        public string RankingsDistinctProjectTypesScript { get; }

        public RankingsExporter(string defaultConnectionString, CloudBlobContainer defaultDestinationContainer, string defaultRankingsScript, string defaultRankingProjectTypesScript, string defaultRankingsDistinctProjectTypesScript, string defaultName)
            : base(defaultConnectionString, defaultDestinationContainer, defaultName)
        {
            RankingsTotalScript = defaultRankingsScript;
            RankingsProjectTypesScript = defaultRankingProjectTypesScript;
            RankingsDistinctProjectTypesScript = defaultRankingsDistinctProjectTypesScript;
        }

        override protected JContainer GetResultOfQuery(SqlConnection connection)
        {
            JObject result = new JObject();

            var rankingsTotalCommand = new SqlCommand(GetEmbeddedSqlScript(RankingsTotalScript), connection);
            rankingsTotalCommand.CommandType = CommandType.Text;
            rankingsTotalCommand.Parameters.Add(new SqlParameter(_rankingCountParameterName, _rankingCount));
            rankingsTotalCommand.CommandTimeout = 60;

            var rankingsTotal = SqlDataReaderToJArray(rankingsTotalCommand.ExecuteReader(), _colPackageId);
            result.Add("Rank", rankingsTotal);

            var distinctProjectTypesCommand = new SqlCommand(GetEmbeddedSqlScript(RankingsDistinctProjectTypesScript), connection);
            distinctProjectTypesCommand.CommandType = CommandType.Text;

            var distinctProjectTypes = SqlDataReaderToJArray(distinctProjectTypesCommand.ExecuteReader(), _colProjectType);

            foreach (string projectGuid in distinctProjectTypes.Children())
            {
                var rankingsProjectTypeCommand = new SqlCommand(GetEmbeddedSqlScript(RankingsProjectTypesScript), connection);
                rankingsProjectTypeCommand.CommandType = CommandType.Text;
                rankingsProjectTypeCommand.Parameters.Add(new SqlParameter(_rankingCountParameterName, _rankingCount));
                rankingsProjectTypeCommand.Parameters.Add(new SqlParameter(_projectGuidParameter, projectGuid));

                var rankingsProjectType = SqlDataReaderToJArray(rankingsProjectTypeCommand.ExecuteReader(), _colPackageId);
                result.Add(projectGuid, rankingsProjectType);
            }

            return result;
        }

        private static JArray SqlDataReaderToJArray(SqlDataReader reader, string column)
        {
            var colNames = GetColMappingFromSqlDataReader(reader);

            var result = new JArray();

            while (reader.Read())
            {
                result.Add(reader.GetString(colNames[column]));
            }

            reader.Close();

            return result;
        }

    }
}
