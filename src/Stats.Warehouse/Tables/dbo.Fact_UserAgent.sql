CREATE TABLE [dbo].[Fact_UserAgent]
(
	[Id]                INT				IDENTITY (1, 1) NOT NULL,
    [UserAgent]         NVARCHAR(500)	NULL
    CONSTRAINT [UserAgent] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = ON)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Dimension_UserAgent_UniqueIndex] ON [dbo].[Fact_UserAgent] ([UserAgent] ASC) INCLUDE ([Id])
GO