CREATE PROCEDURE [jobs].[SuspendInvocation]
	@Id uniqueidentifier,
	@Version int,
	@InstanceName nvarchar(100),
	@Payload nvarchar(MAX),
	@SuspendUntil datetimeoffset
AS
	-- Add a new row for the specified Invocation indicating it has been suspended
	INSERT INTO [private].InvocationsStore(
			[Id],
			[Job],
			[Source],
			[Payload],
			[Status],
			[Result],
			[UpdatedBy],
			[IsContinuation],
			[DequeueCount],
			[Complete],
			[Dequeued],
			[QueuedAt], 
			[NextVisibleAt],
			[UpdatedAt])
	OUTPUT	inserted.*
	SELECT	Id,
			Job, 
			Source, 
			@Payload AS Payload, 
			[Status],
			'Suspended' AS [Result],
			@InstanceName AS [UpdatedBy],
			1 AS IsContinuation,
			DequeueCount,
			[Complete],
			0 AS Dequeued,
			QueuedAt,
			@SuspendUntil AS [NextVisibleAt],
			SYSDATETIMEOFFSET() AS [UpdatedAt]
	FROM	[jobs].ActiveInvocations
	WHERE	[Id] = @Id AND [Version] = @Version
