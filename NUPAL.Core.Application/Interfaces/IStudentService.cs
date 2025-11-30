using Nupal.Domain.Entities;

namespace NUPAL.Core.Application.Interfaces
{
    public interface IStudentService
    {
        Task UpsertStudentAsync(Student s);
        Task<Student> FindByEmailAsync(string email);
        Task<bool> VerifyPasswordAsync(Student s, string password);
    }
}
