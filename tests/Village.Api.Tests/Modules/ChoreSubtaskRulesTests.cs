using Village.Domain;

namespace Village.Api.Tests.Modules;

public class ChoreSubtaskRulesTests
{
    [Fact]
    public void NullParent_TopLevel_IsValid()
    {
        var error = ChoreSubtaskRules.ValidateParentAssignment(null, Guid.NewGuid(), null);

        Assert.Null(error);
    }

    [Fact]
    public void Parent_WithoutParent_IsValid()
    {
        var parentId = Guid.NewGuid();

        var error = ChoreSubtaskRules.ValidateParentAssignment(parentId, Guid.NewGuid(), null);

        Assert.Null(error);
    }

    [Fact]
    public void Chore_CannotBeItsOwnParent()
    {
        var id = Guid.NewGuid();

        var error = ChoreSubtaskRules.ValidateParentAssignment(id, id, null);

        Assert.NotNull(error);
        Assert.Contains("own parent", error);
    }

    [Fact]
    public void Subtask_CannotHaveItsOwnSubtask()
    {
        var parentId = Guid.NewGuid();

        var error = ChoreSubtaskRules.ValidateParentAssignment(
            parentId,
            Guid.NewGuid(),
            parentParentChoreId: Guid.NewGuid()); // parent is itself a subtask

        Assert.NotNull(error);
        Assert.Contains("Subtasks", error);
    }
}
