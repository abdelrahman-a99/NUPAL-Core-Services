using NUPAL.Core.Application.DTOs;

namespace NUPAL.Core.Application.Interfaces
{
    public interface IStudentService
    {
        Task UpsertStudentAsync(ImportStudentDto dto);
        Task<StudentDto> GetStudentByEmailAsync(string email);
        Task<AuthResponseDto> AuthenticateAsync(LoginDto loginDto, string jwtKey, string jwtIssuer, string jwtAudience);
    }
}
