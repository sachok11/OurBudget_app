namespace FamilyBudget.Core.Messages;

public class TransactionCreatedEvent
{
    public Guid TransactionId { get; set; }
    public Guid FamilyId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class BudgetExceededEvent
{
    public Guid FamilyId { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal BudgetAmount { get; set; }
    public decimal CurrentSpent { get; set; }
    public DateTime DetectedAt { get; set; }
}