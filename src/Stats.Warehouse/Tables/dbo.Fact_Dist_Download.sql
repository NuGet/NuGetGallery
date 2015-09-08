CREATE TABLE [dbo].[Fact_Dist_Download]
(
	[Id]							UNIQUEIDENTIFIER NOT NULL DEFAULT newid(),
	[Dimension_Date_Id]				INT NOT NULL,
    [Dimension_Time_Id]				INT NOT NULL,
    [Dimension_Tool_Id]				INT NOT NULL,
	[Dimension_Client_Id]			INT NOT NULL,
    [Dimension_Platform_Id]			INT NOT NULL,
	[DownloadCount]					INT NULL,
    [LogFileName]					NVARCHAR(255) NULL,
	[UserAgent]						NVARCHAR(500) NULL,
    [Timestamp] DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT [PK_Fact_Dist_Download] PRIMARY KEY CLUSTERED ([Id]) WITH (STATISTICS_NORECOMPUTE = ON)
);
GO
CREATE NONCLUSTERED INDEX [Fact_Dist_Download_NCI_TimestampDesc]
    ON [dbo].[Fact_Dist_Download]([Timestamp] DESC)
    INCLUDE([Dimension_Date_Id], [Dimension_Tool_Id], [DownloadCount]) WITH (STATISTICS_NORECOMPUTE = ON);
GO
CREATE NONCLUSTERED INDEX [Fact_Dist_Download_NCI_DownloadCount]
    ON [dbo].[Fact_Dist_Download]([DownloadCount] ASC) WITH (STATISTICS_NORECOMPUTE = ON);
GO
CREATE NONCLUSTERED INDEX [Fact_Dist_Download_NCI_Tool_Id]
    ON [dbo].[Fact_Dist_Download]([Dimension_Tool_Id] ASC)
    INCLUDE([Dimension_Client_Id], [Dimension_Date_Id], [DownloadCount]) WITH (STATISTICS_NORECOMPUTE = ON);
GO
CREATE NONCLUSTERED INDEX [Fact_Dist_Download_NCI_Date_Id]
    ON [dbo].[Fact_Dist_Download]([Dimension_Date_Id] ASC)
    INCLUDE([Dimension_Tool_Id], [DownloadCount]) WITH (STATISTICS_NORECOMPUTE = ON);
GO
CREATE NONCLUSTERED INDEX [Fact_Dist_Download_NCI_Client_Id]
    ON [dbo].[Fact_Dist_Download] ([Dimension_Date_Id])
	INCLUDE ([Dimension_Client_Id], [DownloadCount]) WITH (ONLINE = ON)
GO
CREATE NONCLUSTERED INDEX [Fact_Dist_Download_NCI_UserAgent]
    ON [dbo].[Fact_Dist_Download] ([UserAgent])
	INCLUDE ([Dimension_Client_Id]) WITH (ONLINE = ON)
GO