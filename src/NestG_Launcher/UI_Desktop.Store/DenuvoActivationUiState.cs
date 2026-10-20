using System;
using System.Collections.Generic;

namespace UI_Desktop.Store;

internal sealed class DenuvoActivationUiState
{
	private sealed class Operation(long generation)
	{
		internal long Generation { get; } = generation;

		internal Guid? RequestId { get; set; }

		internal bool Terminal { get; set; }

		internal bool AwaitingRequestIdentity { get; set; }
	}

	private readonly Dictionary<Guid, Operation> operations = new Dictionary<Guid, Operation>();

	private readonly Dictionary<Guid, (Guid LibraryId, long Generation)> requestOwners = new Dictionary<Guid, (Guid, long)>();

	private readonly HashSet<Guid> terminalRequests = new HashSet<Guid>();

	private long nextGeneration;

	internal long Begin(Guid libraryId)
	{
		Operation operation = new Operation(++nextGeneration)
		{
			AwaitingRequestIdentity = true
		};
		operations[libraryId] = operation;
		return operation.Generation;
	}

	internal long Capture(Guid libraryId)
	{
		return Get(libraryId).Generation;
	}

	internal bool IsAwaitingRequest(Guid libraryId)
	{
		if (operations.TryGetValue(libraryId, out Operation value) && value.AwaitingRequestIdentity)
		{
			return !value.Terminal;
		}
		return false;
	}

	internal bool BindRequest(Guid libraryId, long generation, Guid requestId)
	{
		if (requestId == Guid.Empty || terminalRequests.Contains(requestId) || !operations.TryGetValue(libraryId, out Operation value) || value.Generation != generation || value.Terminal)
		{
			return false;
		}
		if (value.RequestId.HasValue && value.RequestId != requestId)
		{
			return false;
		}
		if (requestOwners.TryGetValue(requestId, out (Guid, long) value2) && value2.Item1 != libraryId)
		{
			return false;
		}
		requestOwners[requestId] = (libraryId, generation);
		value.RequestId = requestId;
		value.AwaitingRequestIdentity = false;
		return true;
	}

	internal bool CanShowLoading(Guid libraryId, long generation)
	{
		if (operations.TryGetValue(libraryId, out Operation value) && value.Generation == generation)
		{
			return !value.Terminal;
		}
		return false;
	}

	internal bool TryShowWaiting(Guid libraryId, Guid? requestId, long? generation = null)
	{
		Operation operation = Get(libraryId);
		if (generation.HasValue && generation != operation.Generation)
		{
			return false;
		}
		if (operation.Terminal)
		{
			return false;
		}
		if (requestId.HasValue)
		{
			Guid valueOrDefault = requestId.GetValueOrDefault();
			if (valueOrDefault != Guid.Empty)
			{
				if (terminalRequests.Contains(valueOrDefault))
				{
					return false;
				}
				if (!operation.AwaitingRequestIdentity)
				{
					Guid? requestId2 = operation.RequestId;
					Guid guid = valueOrDefault;
					if (requestId2.HasValue && !(requestId2.GetValueOrDefault() != guid) && requestOwners.TryGetValue(valueOrDefault, out (Guid, long) value))
					{
						(Guid, long) tuple = value;
						guid = libraryId;
						long generation2 = operation.Generation;
						if (!(tuple.Item1 != guid) && tuple.Item2 == generation2)
						{
							goto IL_00ec;
						}
					}
				}
				return false;
			}
		}
		if (!operation.RequestId.HasValue)
		{
			operation.AwaitingRequestIdentity = false;
		}
		goto IL_00ec;
		IL_00ec:
		return true;
	}

	internal bool CanComplete(Guid libraryId, Guid requestId)
	{
		if (requestId == Guid.Empty || terminalRequests.Contains(requestId))
		{
			return false;
		}
		if (!operations.TryGetValue(libraryId, out Operation value))
		{
			return !requestOwners.ContainsKey(requestId);
		}
		if (value.AwaitingRequestIdentity || value.Terminal || (value.RequestId.HasValue && value.RequestId != requestId))
		{
			return false;
		}
		if (requestOwners.TryGetValue(requestId, out (Guid, long) value2))
		{
			(Guid, long) tuple = value2;
			Guid guid = libraryId;
			long generation = value.Generation;
			if (tuple.Item1 != guid || tuple.Item2 != generation)
			{
				return false;
			}
		}
		return true;
	}

	internal bool TryComplete(Guid libraryId, Guid requestId)
	{
		if (!CanComplete(libraryId, requestId))
		{
			return false;
		}
		Operation operation = Get(libraryId);
		terminalRequests.Add(requestId);
		operation.RequestId = requestId;
		operation.Terminal = true;
		operation.AwaitingRequestIdentity = false;
		requestOwners[requestId] = (libraryId, operation.Generation);
		return true;
	}

	internal bool IsCurrentTerminal(Guid libraryId, long generation, Guid requestId)
	{
		if (operations.TryGetValue(libraryId, out Operation value) && value.Generation == generation && value.Terminal)
		{
			return value.RequestId == requestId;
		}
		return false;
	}

	internal bool TryFail(Guid libraryId, long generation)
	{
		if (!CanShowLoading(libraryId, generation))
		{
			return false;
		}
		operations[libraryId].Terminal = true;
		operations[libraryId].AwaitingRequestIdentity = false;
		return true;
	}

	private Operation Get(Guid libraryId)
	{
		if (!operations.TryGetValue(libraryId, out Operation value))
		{
			value = (operations[libraryId] = new Operation(++nextGeneration));
		}
		return value;
	}
}
