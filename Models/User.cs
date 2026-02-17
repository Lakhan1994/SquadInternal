using System.ComponentModel.DataAnnotations;

namespace SquadInternal.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public int RoleId { get; set; }
        public Role? Role { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;




    }
}
