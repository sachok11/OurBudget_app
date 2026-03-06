namespace FamilyBudget.Application.DTOs;

/// <summary>
/// Data Transfer Object for family financial summary
/// This represents the cached data structure
/// </summary>
public class FamilySummaryDto
{
    /// <summary>
    /// Total balance across all family accounts
    /// </summary>
    public decimal TotalBalance { get; set; }
    
    /// <summary>
    /// Total income for the current month
    /// </summary>
    public decimal MonthlyIncome { get; set; }
    
    /// <summary>
    /// Total expenses for the current month
    /// </summary>
    public decimal MonthlyExpenses { get; set; }
    
    /// <summary>
    /// Net savings for the current month (income - expenses)
    /// </summary>
    public decimal MonthlySavings { get; set; }
    
    /// <summary>
    /// Number of transactions in the current month
    /// </summary>
    public int TransactionCount { get; set; }
    
    /// <summary>
    /// Breakdown of expenses by category
    /// </summary>
    public List<CategorySummary> ExpensesByCategory { get; set; } = new();
    
    /// <summary>
    /// List of recent transactions (last 5)
    /// </summary>
    public List<RecentTransaction> RecentTransactions { get; set; } = new();
    
    /// <summary>
    /// When this summary was calculated
    /// </summary>
    public DateTime CalculatedAt { get; set; }
    
    /// <summary>
    /// Budget status for each category
    /// </summary>
    public List<BudgetStatus> BudgetStatuses { get; set; } = new();
}

public class CategorySummary
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int TransactionCount { get; set; }
    public decimal Percentage { get; set; } // Percentage of total expenses
}

public class RecentTransaction
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}

public class BudgetStatus
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal BudgetAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal PercentageUsed { get; set; }
    public bool IsOverBudget { get; set; }
}