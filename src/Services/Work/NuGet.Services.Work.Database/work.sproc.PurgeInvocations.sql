CREATE PROCEDURE [work].[PurgeInvocations]
	@Ids [work].IdList READONLY
AS
	DELETE [private].InvocationsStore
    FROM [private].InvocationsStore s
    INNER JOIN @Ids i ON s.Id = i.Id