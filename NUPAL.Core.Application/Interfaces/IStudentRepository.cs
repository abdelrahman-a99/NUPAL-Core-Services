using Nupal.Domain.Entities;

namespace NUPAL.Core.Application.Interfaces
{
    public interface IStudentRepository
    {
        Task UpsertAsync(Student s);
        Task<Student> FindByEmailAsync(string email);
        Task<Student> GetByIdAsync(string id);
    }
}
