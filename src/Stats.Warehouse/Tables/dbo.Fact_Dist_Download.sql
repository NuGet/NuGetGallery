CREATE TABLE [dbo].[Fact_Dist_Download]
(
	[Id]							UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
	[Dimension_Date_Id]				INT NOT NULL,
    [Dimension_Time_Id]				INT NOT NULL,
    [Dimension_Tool_Id]				INT NOT NULL,
	[Dimension_Client_Id]			INT NOT NULL,
    [Dimension_Platform_Id]			INT NOT NULL,
	[DownloadCount]					INT NULL,
    [Fact_UserAgent_Id]				INT NOT NULL,
    [Fact_LogFileName_Id]			INT NOT NULL,
    [Fact_EdgeServer_IpAddress_Id]	INT NOT NULL,
    [Timestamp] DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_Fact_Dist_Download] PRIMARY KEY CLUSTERED ([Id]) WITH (STATISTICS_NORECOMPUTE = OFF)
);
GO
CREATE NONCLUSTERED INDEX [Fact_Dist_Download_NCI_TimestampDesc]
    ON [dbo].[Fact_Dist_Download]([Timestamp] DESC)
    INCLUDE([Dimension_Date_Id], [Dimension_Tool_Id], [Dimension_Client_Id], [DownloadCount]) WITH (STATISTICS_NORECOMPUTE = OFF);
GO
CREATE NONCLUSTERED INDEX [Fact_Dist_Download_NCI_DownloadCount]
    ON [dbo].[Fact_Dist_Download]([DownloadCount] ASC) WITH (STATISTICS_NORECOMPUTE = OFF);
GO
CREATE NONCLUSTERED INDEX [Fact_Dist_Download_NCI_Tool_Id]
    ON [dbo].[Fact_Dist_Download]([Dimension_Tool_Id] ASC)
    INCLUDE([Dimension_Client_Id], [Dimension_Date_Id], [Dimension_Time_Id], [DownloadCount], [Timestamp]) WITH (STATISTICS_NORECOMPUTE = OFF);
GO
CREATE NONCLUSTERED INDEX [Fact_Dist_Download_NCI_Date_Time]
    ON [dbo].[Fact_Dist_Download]([Dimension_Date_Id] ASC, [Timestamp])
    INCLUDE([Dimension_Tool_Id], [Dimension_Client_Id], [DownloadCount]) WITH (STATISTICS_NORECOMPUTE = OFF);
GO
CREATE NONCLUSTERED INDEX [Fact_Dist_Download_NCI_UserAgent]
    ON [dbo].[Fact_Dist_Download] ([Fact_UserAgent_Id])
	INCLUDE ([Dimension_Client_Id], [DownloadCount]) WITH (ONLINE = ON)
GO
CREATE NONCLUSTERED INDEX [Fact_Dist_Download_NCI_LogFileName]
    ON [dbo].[Fact_Dist_Download] ([Fact_LogFileName_Id])
	INCLUDE ([DownloadCount]) WITH (ONLINE = ON)
GO
CREATE NONCLUSTERED INDEX [Fact_Dist_Download_NCI_EdgeServer_IpAddress]
    ON [dbo].[Fact_Dist_Download] ([Fact_EdgeServer_IpAddress_Id])
	INCLUDE ([DownloadCount]) WITH (ONLINE = ON)
GO
CREATE NONCLUSTERED INDEX [Fact_Dist_Download_NCI_Client]
    ON [dbo].[Fact_Dist_Download] ([Dimension_Client_Id])
	INCLUDE ([Fact_UserAgent_Id]) WITH (ONLINE = ON)
GO
CREATE NONCLUSTERED INDEX [Fact_Dist_Download_NCI_Client_Time]
    ON [dbo].[Fact_Dist_Download] ([Dimension_Client_Id] ASC, [Timestamp] ASC)
    INCLUDE ([Dimension_Tool_Id], [DownloadCount])
GO