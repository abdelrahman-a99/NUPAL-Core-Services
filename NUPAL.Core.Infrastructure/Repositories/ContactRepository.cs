using MongoDB.Driver;
using NUPAL.Core.Application.Interfaces;
using Nupal.Domain.Entities;

namespace Nupal.Core.Infrastructure.Repositories
{
    public class ContactRepository : IContactRepository
    {
        private readonly IMongoCollection<ContactMessage> _collection;

        public ContactRepository(IMongoDatabase database)
        {
            _collection = database.GetCollection<ContactMessage>("ContactMessages");
        }

        public async Task AddAsync(ContactMessage message)
        {
            await _collection.InsertOneAsync(message);
        }
    }
}
