namespace FamilyBudget.Core.Entities;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid UserId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ReceiptUrl { get; set; }
    
    // Navigation properties
    public Account Account { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public User User { get; set; } = null!;
}

public enum TransactionType
{
    Expense = 1,
    Income = 2,
    Transfer = 3
}