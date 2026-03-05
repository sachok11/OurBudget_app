namespace FamilyBudget.Core.Entities;

public class Budget
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public Guid CategoryId { get; set; }
    public decimal Amount { get; set; }
    public BudgetPeriod Period { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public Family Family { get; set; } = null!;
    public Category Category { get; set; } = null!;
}

public enum BudgetPeriod
{
    Weekly = 1,
    Monthly = 2,
    Yearly = 3
}