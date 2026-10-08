using uSync.BackOffice;

namespace uSync.AI.Tools.Models;

/// <summary>
/// What a uSync run tool hands back to the model. Counts cover everything that was processed;
/// <see cref="Changes"/> lists only the items that changed or failed, capped, because the model
/// re-reads a tool result on every later turn.
/// </summary>
public sealed record uSyncToolResult
{
    public required string Operation { get; init; }
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public int ItemCount { get; init; }
    public int ChangeCount { get; init; }
    public int ErrorCount { get; init; }
    public IReadOnlyList<uSyncToolHandlerSummary> Handlers { get; init; } = [];
    public IReadOnlyList<uSyncToolChange> Changes { get; init; } = [];
    public bool ChangesTruncated { get; init; }

    public static uSyncToolResult Failed(string operation, string message)
        => new() { Operation = operation, Success = false, Message = message };

    public static uSyncToolResult From(string operation, IReadOnlyCollection<uSyncAction> actions, int maxChanges)
    {
        var notable = actions.Where(x => x.Change > Core.ChangeType.NoChange || !x.Success).ToList();
        var listed = notable.Take(Math.Max(0, maxChanges)).Select(uSyncToolChange.From).ToList();

        return new uSyncToolResult
        {
            Operation = operation,
            Success = !actions.ContainsErrors(),
            ItemCount = actions.Count,
            ChangeCount = actions.CountChanges(),
            ErrorCount = actions.CountErrors(),
            Handlers =
            [
                .. actions.GroupBy(x => x.HandlerAlias ?? string.Empty).Select(g => new uSyncToolHandlerSummary(
                    g.Key, g.Count(), g.CountChanges(), g.CountErrors())),
            ],
            Changes = listed,
            ChangesTruncated = notable.Count > listed.Count,
        };
    }
}

public sealed record uSyncToolHandlerSummary(string Handler, int ItemCount, int ChangeCount, int ErrorCount);

public sealed record uSyncToolChange(string? Handler, string ItemType, string Name, string Change, bool Success, string? Message)
{
    public static uSyncToolChange From(uSyncAction action)
        => new(action.HandlerAlias, action.ItemType, action.Name, action.Change.ToString(), action.Success, action.Message);
}

public sealed record uSyncToolHandlerList(bool Success, string? Message, IReadOnlyList<uSyncToolHandler> Handlers);

public sealed record uSyncToolHandler(string Alias, string Name, string Group, bool Enabled);
