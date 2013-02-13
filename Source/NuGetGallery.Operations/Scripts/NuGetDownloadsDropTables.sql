
IF OBJECT_ID('[dbo].[Dimension_UserAgent]') IS NOT NULL
	DROP TABLE [dbo].[Dimension_UserAgent]
GO

IF OBJECT_ID('[dbo].[Dimension_Package]') IS NOT NULL
	DROP TABLE [dbo].[Dimension_Package]
GO

IF OBJECT_ID('[dbo].[Dimension_Date]') IS NOT NULL
	DROP TABLE [dbo].[Dimension_Date]
GO

IF OBJECT_ID('[dbo].[Dimension_Time]') IS NOT NULL
	DROP TABLE [dbo].[Dimension_Time]
GO

IF OBJECT_ID('[dbo].[Dimension_Operation]') IS NOT NULL
	DROP TABLE [dbo].[Dimension_Operation]
GO

IF OBJECT_ID('[dbo].[Fact_Download]') IS NOT NULL
	DROP TABLE [dbo].[Fact_Download]
GO

IF OBJECT_ID('[dbo].[ReplicationMarker]') IS NOT NULL
	DROP TABLE [dbo].[ReplicationMarker]
GO

IF OBJECT_ID('[dbo].[PackageReportDirty]') IS NOT NULL
	DROP TABLE [dbo].[PackageReportDirty]
GO
