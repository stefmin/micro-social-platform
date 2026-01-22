using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace MicroSocialPlatform.Models
{
    public enum UserStatus
    {
        Active,
        Suspended,
        Banned
    }

    public class ApplicationUser : IdentityUser
    {
        public UserStatus Status { get; set; } = UserStatus.Active;
        [Required(ErrorMessage = "Numele este obligatoriu")]
        [StringLength(100, ErrorMessage = "Numele nu poate avea mai mult de 100 de caractere")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Descrierea profilului este obligatorie")]
        [StringLength(300, ErrorMessage = "Descrierea nu poate depasi 300 de caractere")]
        public string Bio { get; set; }

        [Required(ErrorMessage = "Imaginea de profil este obligatorie")]
        public string ProfileImage { get; set; }

        public bool IsPrivate { get; set; } = false; // public profile by default

        public virtual ICollection<UserFollow> Following { get; set; } = [];
        public virtual ICollection<UserFollow> Followers { get; set; } = [];

        public virtual ICollection<Notification> Notifications { get; set; } = [];
    }
}
