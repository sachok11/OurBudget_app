using Microsoft.AspNetCore.Mvc;
using FamilyBudget.Infrastructure.Services;
using FamilyBudget.Infrastructure.Data;
using FamilyBudget.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(
        ApplicationDbContext context,
        ICacheService cacheService,
        ILogger<TransactionsController> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequest request)
    {
        var userId = GetUserId();
        
        // ... create transaction logic ...
        
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = request.AccountId,
            CategoryId = request.CategoryId,
            UserId = userId,
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description,
            TransactionDate = request.TransactionDate ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        // Update account balance
        var account = await _context.Accounts
            .Include(a => a.Family)
            .FirstOrDefaultAsync(a => a.Id == request.AccountId);

        if (request.Type == TransactionType.Expense)
            account.Balance -= request.Amount;
        else if (request.Type == TransactionType.Income)
            account.Balance += request.Amount;

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // ============================================
        // IMPORTANT: Invalidate Cache After Data Change
        // ============================================
        
        // Clear the family summary cache since data changed
        var cacheKey = $"family:{account.FamilyId}:summary";
        await _cacheService.RemoveAsync(cacheKey);
        
        _logger.LogInformation("🗑️ Invalidated cache after transaction creation for family {FamilyId}", 
            account.FamilyId);

        return CreatedAtAction(nameof(GetTransaction), 
            new { id = transaction.Id }, 
            transaction);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTransaction(Guid id)
    {
        var transaction = await _context.Transactions
            .Include(t => t.Account)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transaction == null)
            return NotFound();

        // ... authorization checks ...

        // Reverse balance change
        if (transaction.Type == TransactionType.Expense)
            transaction.Account.Balance += transaction.Amount;
        else if (transaction.Type == TransactionType.Income)
            transaction.Account.Balance -= transaction.Amount;

        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync();

        // ============================================
        // Invalidate Cache After Deletion
        // ============================================
        
        var cacheKey = $"family:{transaction.Account.FamilyId}:summary";
        await _cacheService.RemoveAsync(cacheKey);
        
        _logger.LogInformation("🗑️ Invalidated cache after transaction deletion for family {FamilyId}", 
            transaction.Account.FamilyId);

        return NoContent();
    }


    private Guid GetUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId == null)
            throw new UnauthorizedAccessException("User not authenticated");

        return Guid.Parse(userId);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTransaction(Guid id)
    {
        var transaction = await _context.Transactions
            .Include(t => t.Account)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transaction == null)
            return NotFound();

        return Ok(transaction);
    }
}
