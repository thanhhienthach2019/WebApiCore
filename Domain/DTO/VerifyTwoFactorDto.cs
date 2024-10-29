namespace Domain.DTO
{
    public class VerifyTwoFactorDto
    {
        public string Email { get; set; }
        public string TwoFactorCode { get; set; }        
    }
}
