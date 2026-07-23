namespace Village.Domain.Entities;

public class ShoppingList
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Name { get; set; } = "Shopping List";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Family Family { get; set; } = null!;
    public ICollection<ShoppingListItem> Items { get; set; } = new List<ShoppingListItem>();
}

public class ShoppingListItem
{
    public Guid Id { get; set; }
    public Guid ShoppingListId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Unit { get; set; }
    public bool IsChecked { get; set; }
    public Guid? CheckedByUserId { get; set; }
    public DateTime? CheckedAt { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ShoppingList ShoppingList { get; set; } = null!;
}
