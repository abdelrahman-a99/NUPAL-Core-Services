using Nupal.Domain.Entities;

namespace NUPAL.Core.Application.Interfaces
{
    public interface IChatConversationRepository
    {
        Task<ChatConversation> CreateAsync(ChatConversation convo);
        Task<ChatConversation?> GetByIdAsync(string id);
        Task TouchAsync(string id);
        Task UpdateAsync(ChatConversation convo);
        Task DeleteAsync(string id);
        Task<List<ChatConversation>> GetLatestByStudentAsync(string studentId, int limit = 20);
    }
}
