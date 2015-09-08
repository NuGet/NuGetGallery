CREATE TABLE [dbo].[Fact_LogFileName]
(
    [Id]                INT				IDENTITY (1, 1) NOT NULL,
    [LogFileName]         NVARCHAR(255)	NULL
    CONSTRAINT [LogFileName] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = ON)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Dimension_LogFileName_UniqueIndex] ON [dbo].[Fact_LogFileName] ([LogFileName] ASC) INCLUDE ([Id])
GO