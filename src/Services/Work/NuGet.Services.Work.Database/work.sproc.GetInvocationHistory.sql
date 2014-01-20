CREATE PROCEDURE [work].[GetInvocationHistory]
	@Ids [work].IdList READONLY
AS
	SELECT s.* 
    FROM [private].InvocationsStore s
    INNER JOIN @Ids i ON s.Id = i.Id