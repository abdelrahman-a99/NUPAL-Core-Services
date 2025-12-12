namespace NUPAL.Core.Application.DTOs
{
    public class LoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class AuthResponseDto
    {
        public string Token { get; set; }
        public StudentDto Student { get; set; }
    }
}
