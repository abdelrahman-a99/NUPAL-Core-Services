using Nupal.Domain.Entities;

namespace NUPAL.Core.Application.Interfaces
{
    public interface IRlRecommendationRepository
    {
        Task CreateAsync(RlRecommendation recommendation);
        Task<RlRecommendation?> GetByIdAsync(string id);
        Task<RlRecommendation?> GetLatestByStudentIdAsync(string studentId, string? targetTrack = null, string? objectiveProfile = null);
    }
}
