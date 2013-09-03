

IF OBJECT_ID('[dbo].[Dimension_UserAgent]') IS NULL
    CREATE TABLE [dbo].[Dimension_UserAgent]
    (
        [Id]					INT IDENTITY,
        [Value]					VARCHAR(900),
        [Client]				VARCHAR(128),
        [ClientMajorVersion]	INT,
        [ClientMinorVersion]	INT,
        [ClientCategory]		VARCHAR(64),
        CONSTRAINT [PK_Dimension_UserAgent] PRIMARY KEY CLUSTERED ( [Id] )
    )
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'Dimension_UserAgent_NCI_Value')
    CREATE UNIQUE NONCLUSTERED INDEX  [Dimension_UserAgent_NCI_Value] ON [dbo].[Dimension_UserAgent] ( [Value] )
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'Dimension_UserAgent_NCI_Client')
    CREATE NONCLUSTERED INDEX  [Dimension_UserAgent_NCI_Client] ON [dbo].[Dimension_UserAgent] ( [Client] )
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'Dimension_UserAgent_NCI_ClientMajorVersion_ClientMinorVersion')
    CREATE NONCLUSTERED INDEX  [Dimension_UserAgent_NCI_ClientMajorVersion_ClientMinorVersion] ON [dbo].[Dimension_UserAgent] ( [ClientMajorVersion], [ClientMinorVersion] )
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'Dimension_UserAgent_NCI_ClientCategory')
    CREATE NONCLUSTERED INDEX  [Dimension_UserAgent_NCI_ClientCategory] ON [dbo].[Dimension_UserAgent] ( [ClientCategory] )
GO

IF OBJECT_ID('[dbo].[Dimension_Package]') IS NULL
    CREATE TABLE [dbo].[Dimension_Package]
    (
        [Id]				 INT IDENTITY,
        [PackageId]			 NVARCHAR(128),
        [PackageVersion]	 NVARCHAR(64),
        [PackageListed]      BIT,
        [PackageTitle]       NVARCHAR(256),
        [PackageDescription] NVARCHAR(MAX),
        [PackageIconUrl]     NVARCHAR(MAX),
        CONSTRAINT [PK_Dimension_Package] PRIMARY KEY CLUSTERED ([Id])
    )
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'Dimension_Package_NCI_PackageId_PackageVersion')
    CREATE UNIQUE NONCLUSTERED INDEX [Dimension_Package_NCI_PackageId_PackageVersion] ON [dbo].[Dimension_Package] ( [PackageId], [PackageVersion] )
GO

IF OBJECT_ID('[dbo].[Dimension_Date]') IS NULL
    CREATE TABLE [dbo].[Dimension_Date] (
        [Id]                          INT            IDENTITY NOT NULL,
        [Date]                        DATE			 NULL,
        [DateName]                    NVARCHAR (150) NOT NULL,
        [DayOfWeek]                   INT            NULL,
        [DayOfWeekName]               NVARCHAR (30)  NOT NULL,
        [MonthName]                   NVARCHAR (30)  NOT NULL,
        [WeekdayIndicator]            NVARCHAR (10)  NOT NULL,
        [DayOfYear]                   INT            NULL,
        [WeekOfYear]                  INT            NULL,
        [WeekOfYearName]              NVARCHAR (10)  NOT NULL,
        [WeekOfYearNameInYear]        NVARCHAR (50)  NOT NULL,
        [MonthOfYear]                 INT            NULL,
        [MonthOfYearName]             NVARCHAR (10)  NOT NULL,
        [MonthOfYearNameInYear]       NVARCHAR (50)  NOT NULL,
        [Quarter]                     INT            NULL,
        [QuarterName]                 NVARCHAR (10)  NOT NULL,
        [QuarterNameInYear]           NVARCHAR (50)  NOT NULL,
        [HalfYear]                    INT            NULL,
        [HalfYearName]                NVARCHAR (10)  NOT NULL,
        [HalfYearNameInYear]          NVARCHAR (50)  NOT NULL,
        [Year]                        INT            NULL,
        [YearName]                    NVARCHAR (50)  NOT NULL,
        [FiscalDayOfYear]             INT            NULL,
        [FiscalWeekOfYear]            INT            NULL,
        [FiscalWeekOfYearName]        NVARCHAR (10)  NOT NULL,
        [FiscalWeekOfYearNameInYear]  NVARCHAR (20)  NOT NULL,
        [FiscalMonthOfYear]           INT            NULL,
        [FiscalMonthOfYearName]       NVARCHAR (10)  NOT NULL,
        [FiscalMonthOfYearNameInYear] NVARCHAR (10)  NOT NULL,
        [FiscalQuarter]               INT            NULL,
        [FiscalQuarterName]           NVARCHAR (10)  NOT NULL,
        [FiscalQuarterNameInYear]     NVARCHAR (20)  NOT NULL,
        [FiscalHalfYear]              INT            NULL,
        [FiscalHalfYearName]          NVARCHAR (10)  NOT NULL,
        [FiscalHalfYearNameInYear]    NVARCHAR (10)  NOT NULL,
        [FiscalYear]                  INT            NULL,
        [FiscalYearName]              NVARCHAR (10)  NOT NULL,
        CONSTRAINT [PK_Dimension_Date] PRIMARY KEY CLUSTERED ( [Id] )
    );
go

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'Dimension_Date_NCI_Date')
    CREATE UNIQUE NONCLUSTERED INDEX [Dimension_Date_NCI_Date] ON [dbo].[Dimension_Date] ( [Date] )
GO

IF OBJECT_ID('[dbo].[Dimension_Time]') IS NULL
    CREATE TABLE [dbo].[Dimension_Time]
    (
        [Id] INT IDENTITY,
        [HourOfDay] INT
        CONSTRAINT [PK_Dimension_Time] PRIMARY KEY CLUSTERED ( [Id] )
    )
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'Dimension_Time_NCI_HourOfDay')
    CREATE UNIQUE NONCLUSTERED INDEX [Dimension_Time_NCI_HourOfDay] ON [Dimension_Time] ( [HourOfDay] )
GO

IF OBJECT_ID('[dbo].[Dimension_Operation]') IS NULL
    CREATE TABLE [dbo].[Dimension_Operation]
    (
        [Id] INT IDENTITY,
        [Operation] NVARCHAR(18)
        CONSTRAINT [PK_Dimension_Operation] PRIMARY KEY CLUSTERED ( [Id] )
    )
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'Dimension_Operation_NCI_Operation')
    CREATE UNIQUE NONCLUSTERED INDEX [Dimension_Operation_NCI_Operation] ON [Dimension_Operation] ( [Operation] )
GO

IF OBJECT_ID('[dbo].[Dimension_Project]') IS NULL
    CREATE TABLE [dbo].[Dimension_Project]
    (
        [Id] INT IDENTITY,
        [ProjectTypes] NVARCHAR(450)
        CONSTRAINT [PK_Dimension_Project] PRIMARY KEY CLUSTERED ( [Id] )
    )
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'Dimension_Project_NCI_ProjectTypes')
    CREATE UNIQUE NONCLUSTERED INDEX [Dimension_Project_NCI_ProjectTypes] ON [Dimension_Project] ( [ProjectTypes] )
GO

IF OBJECT_ID('[dbo].[Fact_Download]') IS NULL
    CREATE TABLE [dbo].[Fact_Download]
    (
        [Dimension_UserAgent_Id]	INT,
        [Dimension_Package_Id]		INT,
        [Dimension_Date_Id]			INT,
        [Dimension_Time_Id]			INT,
        [Dimension_Operation_Id]	INT,
        [Dimension_Project_Id]		INT,
        [DownloadCount]				INT
        CONSTRAINT [PK_Fact_Download] PRIMARY KEY CLUSTERED ( [Dimension_UserAgent_Id], [Dimension_Package_Id], [Dimension_Date_Id], [Dimension_Time_Id], [Dimension_Operation_Id], [Dimension_Project_Id] )
    )
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'Fact_Download_NCI_DownloadCount')
    CREATE NONCLUSTERED INDEX [Fact_Download_NCI_DownloadCount] ON [dbo].[Fact_Download] ( [DownloadCount] )
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'Fact_Download_NCI_Package_Id')
	CREATE NONCLUSTERED INDEX [Fact_Download_NCI_Package_Id] ON [dbo].[Fact_Download] ( [Dimension_Package_Id] ) INCLUDE ( [Dimension_UserAgent_Id], [Dimension_Date_Id], [Dimension_Operation_Id], [DownloadCount] )
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'Fact_Download_NCI_Date_Id')
	CREATE NONCLUSTERED INDEX [Fact_Download_NCI_Date_Id] ON [dbo].[Fact_Download] ( [Dimension_Date_Id] ) INCLUDE ( [Dimension_Package_Id], [DownloadCount] )
GO

IF OBJECT_ID('[dbo].[ReplicationMarker]') IS NULL
    CREATE TABLE [dbo].[ReplicationMarker]
    (
        [LastOriginalKey]	INT
        CONSTRAINT [PK_ReplicationMarker] PRIMARY KEY CLUSTERED ( [LastOriginalKey] )
    )
GO

IF OBJECT_ID('[dbo].[PackageReportDirty]') IS NULL
    CREATE TABLE [dbo].[PackageReportDirty]
    (
        [PackageId]	 NVARCHAR(128),
        [DirtyCount] INT
        CONSTRAINT [PK_PackageReportDirty] PRIMARY KEY CLUSTERED ( [PackageId] )
    )
GO
