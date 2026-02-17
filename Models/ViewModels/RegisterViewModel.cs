using System.ComponentModel.DataAnnotations;

namespace SquadInternal.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required, MaxLength(200), EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;
    }
}
