CREATE PROCEDURE [jobs].[GetInvocationHistory]
	@Id uniqueidentifier
AS
	SELECT * FROM [private].InvocationsStore WHERE Id = @Id