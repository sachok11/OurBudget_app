namespace FamilyBudget.Core.Entities;

public class FamilyMember
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public Guid UserId { get; set; }
    public FamilyRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
    
    // Navigation properties
    public Family Family { get; set; } = null!;
    public User User { get; set; } = null!;
}

public enum FamilyRole
{
    Admin = 1,
    Member = 2,
    Viewer = 3
}