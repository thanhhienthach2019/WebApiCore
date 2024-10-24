using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class User
    {
        [Key]
        [Required]
        public Guid Id { get; set; }
        public string? Username { get; set; }
        [Required]
        public string PasswordHash { get; set; }
        [EmailAddress]
        [Required]
        public string Email { get; set; }
        public string? TwoFactorCodeRegister { get; set; }
        public string? TwoFactorCodeLogin { get; set; }
        public string? DeviceFingerprint { get; set; }
        public DateTime? TwoFactorRegisterExpiryTime { get; set; }
        public DateTime? TwoFactorLoginExpiryTime { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public bool? IsActiveToken { get; set; }
        public virtual ICollection<Token> Tokens { get; set; } = new List<Token>();
    }
}
