CREATE PROCEDURE [jobs].[PurgeInvocation]
	@InvocationId uniqueidentifier
AS
	DELETE FROM [private].InvocationsStore WHERE InvocationId = @InvocationId
