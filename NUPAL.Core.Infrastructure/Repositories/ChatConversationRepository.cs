using MongoDB.Bson;
using MongoDB.Driver;
using Nupal.Domain.Entities;
using NUPAL.Core.Application.Interfaces;

namespace Nupal.Core.Infrastructure.Repositories
{
    public class ChatConversationRepository : IChatConversationRepository
    {
        private readonly IMongoCollection<ChatConversation> _col;

        public ChatConversationRepository(IMongoDatabase db)
        {
            _col = db.GetCollection<ChatConversation>("chat_conversations");

            var idx1 = new CreateIndexModel<ChatConversation>(
                Builders<ChatConversation>.IndexKeys.Ascending(x => x.StudentId).Descending(x => x.LastActivityAt));
            _col.Indexes.CreateOne(idx1);
        }

        public async Task<ChatConversation> CreateAsync(ChatConversation convo)
        {
            await _col.InsertOneAsync(convo);
            return convo;
        }

        public async Task<ChatConversation?> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            if (!ObjectId.TryParse(id, out var oid)) return null;
            return await _col.Find(x => x.Id == oid).FirstOrDefaultAsync();
        }

        public async Task TouchAsync(string id)
        {
            if (!ObjectId.TryParse(id, out var oid)) return;
            var update = Builders<ChatConversation>.Update.Set(x => x.LastActivityAt, DateTime.UtcNow);
            await _col.UpdateOneAsync(x => x.Id == oid, update);
        }

        public async Task UpdateAsync(ChatConversation convo)
        {
            await _col.ReplaceOneAsync(x => x.Id == convo.Id, convo);
        }

        public async Task DeleteAsync(string id)
        {
            if (!ObjectId.TryParse(id, out var oid)) return;
            await _col.DeleteOneAsync(x => x.Id == oid);
        }

        public async Task<List<ChatConversation>> GetLatestByStudentAsync(string studentId, int limit = 20)
        {
            return await _col.Find(x => x.StudentId == studentId)
                .SortByDescending(x => x.LastActivityAt)
                .Limit(limit)
                .ToListAsync();
        }
    }
}
