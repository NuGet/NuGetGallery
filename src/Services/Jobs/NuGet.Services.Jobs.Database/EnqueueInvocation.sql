CREATE PROCEDURE [dbo].[EnqueueInvocation]
	@Job nvarchar(50),
	@Source nvarchar(50),
	@Payload nvarchar(MAX) = NULL,
	@InstanceName nvarchar(100),
	@VisibilityDelayInMilliseconds int = 0
AS

	INSERT INTO Invocations(
		[Job], 
		[Source], 
		[Payload], 
		[Status], 
		[Result], 
		[DequeueCount], 
		[LastInstanceName], 
		[IsContinuation], 
		[QueuedAt], 
		[NextVisibleAt], 
		[Complete],
		[LastUpdatedAt],
		[Dequeued])
		OUTPUT INSERTED.[Id]
	VALUES(
		@Job, 
		@Source, 
		@Payload, 
		'Queued', 
		'Incomplete', 
		0, 
		@InstanceName, 
		0, 
		SYSDATETIMEOFFSET(), 
		DATEADD(ms, @VisibilityDelayInMilliseconds, SYSDATETIMEOFFSET()), 
		0,
		SYSDATETIMEOFFSET(),
		0)