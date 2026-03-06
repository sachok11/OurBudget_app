using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using FamilyBudget.Core.Entities;
using FamilyBudget.Infrastructure.Data;
using FamilyBudget.Infrastructure.Services;
using FamilyBudget.Application.DTOs;

namespace FamilyBudget.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FamiliesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<FamiliesController> _logger;

    public FamiliesController(
        ApplicationDbContext context,
        ICacheService cacheService,
        ILogger<FamiliesController> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
    }

    // Helper method to get current user ID from JWT token
    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    /// <summary>
    /// Get comprehensive family financial summary with caching
    /// This endpoint is cached for 5 minutes to reduce database load
    /// </summary>
    /// <param name="familyId">ID of the family</param>
    /// <returns>Family summary including balances, transactions, and budgets</returns>
    [HttpGet("{familyId}/summary")]
    [ProducesResponseType(typeof(FamilySummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFamilySummary(Guid familyId)
    {
        var userId = GetUserId();
        
        // ============================================
        // STEP 1: Verify User Has Access to Family
        // ============================================
        
        var isMember = await _context.FamilyMembers
            .AnyAsync(fm => fm.FamilyId == familyId && fm.UserId == userId);

        if (!isMember)
        {
            _logger.LogWarning("User {UserId} attempted to access family {FamilyId} without permission", 
                userId, familyId);
            return Forbid();
        }

        // ============================================
        // STEP 2: Create Cache Key
        // ============================================
        
        /*
         * Cache Key Strategy:
         * Format: "family:{familyId}:summary"
         * Example: "family:123e4567-e89b-12d3-a456-426614174000:summary"
         * 
         * This allows us to:
         * - Easily find and invalidate family-specific data
         * - Use pattern matching to clear related caches
         * - Organize cache by entity type
         */
        
        var cacheKey = $"family:{familyId}:summary";

        // ============================================
        // STEP 3: Try to Get from Cache First
        // ============================================
        
        var cachedSummary = await _cacheService.GetAsync<FamilySummaryDto>(cacheKey);
        
        if (cachedSummary != null)
        {
            _logger.LogInformation("✅ Cache HIT: Returning cached summary for family {FamilyId}", familyId);
            
            // Add header to indicate this was served from cache
            Response.Headers.Add("X-Cache-Status", "HIT");
            
            return Ok(cachedSummary);
        }
        
        _logger.LogInformation("❌ Cache MISS: Calculating summary for family {FamilyId}", familyId);
        Response.Headers.Add("X-Cache-Status", "MISS");

        // ============================================
        // STEP 4: Calculate Summary from Database
        // ============================================
        
        var startTime = DateTime.UtcNow;
        var currentMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        // Get all accounts for this family
        var accounts = await _context.Accounts
            .Where(a => a.FamilyId == familyId && a.IsActive)
            .ToListAsync();

        // Calculate total balance
        var totalBalance = accounts.Sum(a => a.Balance);

        // Get current month transactions
        var monthlyTransactions = await _context.Transactions
            .Include(t => t.Category)
            .Include(t => t.User)
            .Where(t => t.Account.FamilyId == familyId 
                && t.TransactionDate >= currentMonthStart)
            .ToListAsync();

        // Calculate monthly income and expenses
        var monthlyIncome = monthlyTransactions
            .Where(t => t.Type == TransactionType.Income)
            .Sum(t => t.Amount);

        var monthlyExpenses = monthlyTransactions
            .Where(t => t.Type == TransactionType.Expense)
            .Sum(t => t.Amount);

        // Group expenses by category
        var expensesByCategory = monthlyTransactions
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => new { t.Category.Id, t.Category.Name, t.Category.Icon, t.Category.Color })
            .Select(g => new CategorySummary
            {
                CategoryId = g.Key.Id,
                CategoryName = g.Key.Name,
                Icon = g.Key.Icon,
                Color = g.Key.Color,
                TotalAmount = g.Sum(t => t.Amount),
                TransactionCount = g.Count(),
                Percentage = monthlyExpenses > 0 ? (g.Sum(t => t.Amount) / monthlyExpenses) * 100 : 0
            })
            .OrderByDescending(c => c.TotalAmount)
            .ToList();

        // Get recent transactions (last 5)
        var recentTransactions = await _context.Transactions
            .Include(t => t.Category)
            .Include(t => t.User)
            .Where(t => t.Account.FamilyId == familyId)
            .OrderByDescending(t => t.TransactionDate)
            .Take(5)
            .Select(t => new RecentTransaction
            {
                Id = t.Id,
                Description = t.Description ?? "No description",
                Amount = t.Amount,
                Type = t.Type.ToString(),
                CategoryName = t.Category.Name,
                UserName = $"{t.User.FirstName} {t.User.LastName}",
                Date = t.TransactionDate
            })
            .ToListAsync();

        // Get budget statuses
        var budgets = await _context.Budgets
            .Include(b => b.Category)
            .Where(b => b.FamilyId == familyId 
                && b.IsActive
                && b.Period == BudgetPeriod.Monthly
                && b.StartDate <= DateTime.UtcNow
                && (b.EndDate == null || b.EndDate >= DateTime.UtcNow))
            .ToListAsync();

        var budgetStatuses = budgets.Select(budget =>
        {
            var spent = monthlyTransactions
                .Where(t => t.CategoryId == budget.CategoryId && t.Type == TransactionType.Expense)
                .Sum(t => t.Amount);

            return new BudgetStatus
            {
                CategoryId = budget.CategoryId,
                CategoryName = budget.Category.Name,
                BudgetAmount = budget.Amount,
                SpentAmount = spent,
                RemainingAmount = budget.Amount - spent,
                PercentageUsed = budget.Amount > 0 ? (spent / budget.Amount) * 100 : 0,
                IsOverBudget = spent > budget.Amount
            };
        }).ToList();

        // ============================================
        // STEP 5: Create Summary DTO
        // ============================================
        
        var summary = new FamilySummaryDto
        {
            TotalBalance = totalBalance,
            MonthlyIncome = monthlyIncome,
            MonthlyExpenses = monthlyExpenses,
            MonthlySavings = monthlyIncome - monthlyExpenses,
            TransactionCount = monthlyTransactions.Count,
            ExpensesByCategory = expensesByCategory,
            RecentTransactions = recentTransactions,
            BudgetStatuses = budgetStatuses,
            CalculatedAt = DateTime.UtcNow
        };

        // ============================================
        // STEP 6: Cache the Result
        // ============================================
        
        /*
         * Why 5 minutes?
         * - Balance between freshness and performance
         * - Family budgets don't change every second
         * - Reduces database load significantly
         * - You can adjust based on your needs:
         *   - Real-time app: 1-2 minutes
         *   - Less frequent updates: 10-15 minutes
         */
        
        await _cacheService.SetAsync(cacheKey, summary, TimeSpan.FromMinutes(5));
        
        var calculationTime = DateTime.UtcNow - startTime;
        _logger.LogInformation("✅ Summary calculated and cached for family {FamilyId} in {Ms}ms", 
            familyId, calculationTime.TotalMilliseconds);

        return Ok(summary);
    }

    /// <summary>
    /// Invalidate (clear) family summary cache
    /// Call this after creating/updating/deleting transactions or budgets
    /// </summary>
    [HttpPost("{familyId}/cache/invalidate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> InvalidateFamilyCache(Guid familyId)
    {
        var cacheKey = $"family:{familyId}:summary";
        await _cacheService.RemoveAsync(cacheKey);
        
        _logger.LogInformation("🗑️ Invalidated cache for family {FamilyId}", familyId);
        
        return Ok(new { message = "Cache invalidated successfully" });
    }

    /// <summary>
    /// Invalidate all caches related to a family
    /// Uses pattern matching to remove all family-related cache entries
    /// </summary>
    [HttpPost("{familyId}/cache/clear-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ClearAllFamilyCache(Guid familyId)
    {
        // Remove all keys starting with "family:{familyId}:"
        await _cacheService.RemoveByPatternAsync($"family:{familyId}:*");
        
        _logger.LogInformation("🗑️ Cleared all cache for family {FamilyId}", familyId);
        
        return Ok(new { message = "All family cache cleared successfully" });
    }
}