CREATE TABLE [dbo].[Dimension_Time] (
    [Id]        INT IDENTITY (1, 1) NOT NULL,
    [HourOfDay] INT NULL,
    CONSTRAINT [PK_Dimension_Time] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [Dimension_Time_NCI_HourOfDay]
    ON [dbo].[Dimension_Time]([HourOfDay] ASC);