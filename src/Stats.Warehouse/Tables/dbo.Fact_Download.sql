CREATE TABLE [dbo].[Fact_Download] (
	[Id]							UNIQUEIDENTIFIER NOT NULL DEFAULT newid(),
    [Dimension_Package_Id]			INT NOT NULL,
    [Dimension_Date_Id]				INT NOT NULL,
    [Dimension_Time_Id]				INT NOT NULL,
    [Dimension_Operation_Id]		INT NOT NULL,
    [Dimension_ProjectType_Id]		INT NOT NULL,
    [Dimension_Client_Id]			INT NOT NULL,
    [Dimension_Platform_Id]			INT NOT NULL,
    [DownloadCount] INT NULL,
    CONSTRAINT [PK_Fact_Download] PRIMARY KEY CLUSTERED ([Id]) WITH (STATISTICS_NORECOMPUTE = ON)
);
GO
CREATE NONCLUSTERED INDEX [Fact_Download_NCI_DownloadCount]
    ON [dbo].[Fact_Download]([DownloadCount] ASC) WITH (STATISTICS_NORECOMPUTE = ON);
GO
CREATE NONCLUSTERED INDEX [Fact_Download_NCI_Package_Id]
    ON [dbo].[Fact_Download]([Dimension_Package_Id] ASC)
    INCLUDE([Dimension_Client_Id], [Dimension_Date_Id], [Dimension_Operation_Id], [DownloadCount]) WITH (STATISTICS_NORECOMPUTE = ON);
GO
CREATE NONCLUSTERED INDEX [Fact_Download_NCI_Date_Id]
    ON [dbo].[Fact_Download]([Dimension_Date_Id] ASC)
    INCLUDE([Dimension_Package_Id], [DownloadCount]) WITH (STATISTICS_NORECOMPUTE = ON);
GO