using Nupal.Domain.Entities;

namespace NUPAL.Core.Application.Interfaces
{
    public interface IChatMessageRepository
    {
        Task CreateAsync(ChatMessage message);
        Task DeleteByConversationIdAsync(string conversationId);
        Task<List<ChatMessage>> GetRecentByConversationAsync(string conversationId, int limit = 30);
    }
}
