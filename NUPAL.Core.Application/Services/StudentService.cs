using NUPAL.Core.Application.Interfaces;
using Nupal.Domain.Entities;

namespace NUPAL.Core.Application.Services
{
    public class StudentService : IStudentService
    {
        private readonly IStudentRepository _repo;

        public StudentService(IStudentRepository repo)
        {
            _repo = repo;
        }

        public async Task UpsertStudentAsync(Student s)
        {
            await _repo.UpsertAsync(s);
        }

        public async Task<Student> FindByEmailAsync(string email)
        {
            return await _repo.FindByEmailAsync(email.ToLower());
        }

        public Task<bool> VerifyPasswordAsync(Student s, string password)
        {
            return Task.FromResult(BCrypt.Net.BCrypt.Verify(password, s.Account.PasswordHash));
        }
    }
}
