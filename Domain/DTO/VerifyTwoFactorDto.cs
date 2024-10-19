namespace Domain.DTO
{
    public class VerifyTwoFactorDto
    {
        public string Username { get; set; }
        public string TwoFactorCode { get; set; }
    }
}
