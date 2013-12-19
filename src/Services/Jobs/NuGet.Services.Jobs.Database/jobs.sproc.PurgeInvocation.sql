CREATE PROCEDURE [jobs].[PurgeInvocation]
	@Id uniqueidentifier
AS
	DELETE FROM [private].InvocationsStore WHERE Id = @Id
