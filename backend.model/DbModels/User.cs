using System.ComponentModel.DataAnnotations.Schema;

namespace backend.model.DbModels;

[Table("Users")]
public partial class User
{
    public int UserID { get; set; }

    public string UserSID { get; set; } = null!;

    public string UserName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string? MiddleName { get; set; }

    public string LastName { get; set; } = null!;

    public string? CreatedDateTime { get; set; }

    public string? LastModifiedDateTime { get; set; }

    public int Status { get; set; }
}
