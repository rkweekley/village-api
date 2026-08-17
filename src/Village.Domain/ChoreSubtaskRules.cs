namespace Village.Domain;

/// <summary>
/// Pure validation rules for the chore subtask hierarchy. Kept free of any
/// data-access so they can be unit-tested in isolation. Existence of the parent
/// (same family + active) is enforced by the caller's DB query; these methods
/// cover only the deterministic structural rules.
/// </summary>
public static class ChoreSubtaskRules
{
    /// <summary>
    /// Returns an error message if the requested parent assignment is invalid,
    /// otherwise <c>null</c> (valid).
    /// </summary>
    /// <param name="parentChoreId">The requested parent id (null = top-level).</param>
    /// <param name="choreId">The id of the chore being assigned a parent.</param>
    /// <param name="parentParentChoreId">The requested parent's own parent id, if any.</param>
    public static string? ValidateParentAssignment(
        Guid? parentChoreId,
        Guid choreId,
        Guid? parentParentChoreId)
    {
        if (parentChoreId == null)
        {
            return null; // top-level chores are always valid
        }

        if (parentChoreId.Value == choreId)
        {
            return "A chore cannot be its own parent.";
        }

        if (parentParentChoreId != null)
        {
            return "Subtasks cannot have their own subtasks.";
        }

        return null;
    }
}
