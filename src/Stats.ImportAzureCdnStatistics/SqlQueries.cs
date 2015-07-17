// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Stats.ImportAzureCdnStatistics
{
    internal static class SqlQueries
    {
        private const string _sqlGetAllTimeDimensions = "SELECT [Id],[HourOfDay] FROM[dbo].[Dimension_Time]";
        private const string _sqlGetDateDimensions = "SELECT [Id],[Date] FROM[dbo].[Dimension_Date] WHERE [Date] >= '{0}' AND [Date] <= '{1}'";

        private const string _sqlGetOperationDimensionAndCreateIfNotExists = "IF NOT EXISTS (SELECT Id FROM [Dimension_Operation] WHERE [Operation] = '{0}') " +
                                                                             "INSERT INTO [Dimension_Operation] ([Operation]) VALUES('{0}');" +
                                                                             "SELECT [Id] FROM [Dimension_Operation] WHERE [Operation] = '{0}'";

        private const string _sqlGetProjectTypeDimensionAndCreateIfNotExists = "IF NOT EXISTS (SELECT Id FROM [Dimension_ProjectType] WHERE [ProjectType] = '{0}') " +
                                                                               "INSERT INTO [Dimension_ProjectType] ([ProjectType]) VALUES('{0}');" +
                                                                               "SELECT [Id] FROM [Dimension_ProjectType] WHERE [ProjectType] = '{0}'";

        private const string _sqlGetClientDimensionAndCreateIfNotExists = "IF NOT EXISTS (SELECT Id FROM [Dimension_Client] WHERE [ClientName] = '{0}' AND [Major] = '{1}' AND [Minor] = '{2}' AND [Patch] = '{3}') " +
                                                                               "INSERT INTO [Dimension_Client] ([ClientName], [Major], [Minor], [Patch]) VALUES('{0}', '{1}', '{2}', '{3}');" +
                                                                               "SELECT [Id] FROM [Dimension_Client] WHERE [ClientName] = '{0}' AND [Major] = '{1}' AND [Minor] = '{2}' AND [Patch] = '{3}'";

        private const string _sqlGetPlatformDimensionAndCreateIfNotExists = "IF NOT EXISTS (SELECT Id FROM [Dimension_Platform] WHERE [OSFamily] = '{0}' AND [Major] = '{1}' AND [Minor] = '{2}' AND [Patch] = '{3}' AND [PatchMinor] = '{4}') " +
                                                                               "INSERT INTO [Dimension_Platform] ([OSFamily], [Major], [Minor], [Patch], [PatchMinor]) VALUES('{0}', '{1}', '{2}', '{3}', '{4}');" +
                                                                               "SELECT [Id] FROM [Dimension_Platform] WHERE [OSFamily] = '{0}' AND [Major] = '{1}' AND [Minor] = '{2}' AND [Patch] = '{3}' AND [PatchMinor] = '{4}'";

        private const string _sqlGetPackageDimensionAndCreateIfNotExists = "IF NOT EXISTS (SELECT Id FROM [Dimension_Package] WHERE [PackageId] = '{0}' AND [PackageVersion] = '{1}') " +
                                                                               "INSERT INTO [Dimension_Package] ([PackageId], [PackageVersion]) VALUES('{0}', '{1}');" +
                                                                               "SELECT [Id] FROM [Dimension_Package] WHERE [PackageId] = '{0}' AND [PackageVersion] = '{1}'";

        public static string GetOperationDimensionAndCreateIfNotExists(string operation)
        {
            return string.Format(_sqlGetOperationDimensionAndCreateIfNotExists, operation);
        }

        public static string GetProjectTypeDimensionAndCreateIfNotExists(string projectType)
        {
            return string.Format(_sqlGetProjectTypeDimensionAndCreateIfNotExists, projectType);
        }

        public static string GetClientDimensionAndCreateIfNotExists(ClientDimension clientDimension)
        {
            return string.Format(_sqlGetClientDimensionAndCreateIfNotExists, clientDimension.ClientName, clientDimension.Major, clientDimension.Minor, clientDimension.Patch);
        }

        public static string GetPlatformDimensionAndCreateIfNotExists(PlatformDimension platformDimension)
        {
            return string.Format(_sqlGetPlatformDimensionAndCreateIfNotExists, platformDimension.OSFamily, platformDimension.Major, platformDimension.Minor, platformDimension.Patch, platformDimension.PatchMinor);
        }

        public static string GetAllTimeDimensions()
        {
            return _sqlGetAllTimeDimensions;
        }

        public static string GetDateDimensions(DateTime min, DateTime max)
        {
            return string.Format(_sqlGetDateDimensions, min.ToString("yyyy-MM-dd"), max.ToString("yyyy-MM-dd"));
        }

        public static string GetPackageDimensionAndCreateIfNotExists(PackageDimension package)
        {
            return string.Format(_sqlGetPackageDimensionAndCreateIfNotExists, package.PackageId, package.PackageVersion);
        }
    }
}