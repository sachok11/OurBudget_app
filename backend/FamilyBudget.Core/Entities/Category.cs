namespace FamilyBudget.Core.Entities;

public class Category
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "📁";
    public string Color { get; set; } = "#000000";
    public CategoryType Type { get; set; }
    public bool IsDefault { get; set; }
    
    // Navigation properties
    public Family Family { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
}

public enum CategoryType
{
    Expense = 1,
    Income = 2
}