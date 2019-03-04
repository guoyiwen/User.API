using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contact.API.Dtos;
using Contact.API.Models;
using MongoDB.Driver;
using Contact = Contact.API.Models.Contact;

namespace Contact.API.Data
{
    public class MongoContactRepository : IContactRepository
    {

        private readonly MongoContactDbContext _mongoContactDbContext;

        public MongoContactRepository(MongoContactDbContext mongoContactDbContext)
        {
            _mongoContactDbContext = mongoContactDbContext;
        }

        public async Task<bool> AddContactAsync(int userId, BaseUserInfo contact, CancellationToken cancellationToken)
        {
            if (!(await _mongoContactDbContext.ContactBooks.FindAsync(c=>c.UserId==userId, cancellationToken: cancellationToken)).Any())
            {
               await _mongoContactDbContext.ContactBooks.InsertOneAsync(new ContactBook{UserId = userId}, cancellationToken: cancellationToken);
            }


            var filter = Builders<ContactBook>.Filter.Eq(c => c.UserId, userId);
            var update = Builders<ContactBook>.Update.AddToSet(c => c.Contacts, new Models.Contact
            {
                UserId = contact.UserId,
                Avatar = contact.Avatar,
                Company = contact.Company,
                Name = contact.Name,
                Title = contact.Title
            });

            var result = await _mongoContactDbContext.ContactBooks.UpdateOneAsync(filter, update, null, cancellationToken);

            return result.MatchedCount == result.ModifiedCount && result.ModifiedCount == 1;

        }

        public async Task<List<Models.Contact>> GetContactsAsync(int userId, CancellationToken cancellationToken)
        {
            var contackBook = (await _mongoContactDbContext.ContactBooks.FindAsync(c => c.UserId == userId, cancellationToken: cancellationToken))
                .FirstOrDefault();

            if (contackBook!=null)
            {
                return  contackBook.Contacts; 
            }

            {
                //Log Tdb
                return new List<Models.Contact>();
            }

        }

        public async Task<bool> TagContactAsync(int userId, int contactId, List<string> tags, CancellationToken cancellationToken)
        {
            var filter = Builders<ContactBook>.Filter.And(
                Builders<ContactBook>.Filter.Eq(c => c.UserId, userId),
                Builders<ContactBook>.Filter.Eq("Contacts.$.UserId", contactId));

            var update = Builders<ContactBook>.Update
                .Set("Contacts.$.Tags", tags);

            var result = await _mongoContactDbContext.ContactBooks.UpdateOneAsync(filter, update, null, cancellationToken);

            return result.MatchedCount == result.ModifiedCount && result.MatchedCount == 1;


        }

        public async Task<bool> UpdateContactInfoAsync(BaseUserInfo userInfo)
        {
            var contactBook =
                (await _mongoContactDbContext.ContactBooks.FindAsync(c => c.UserId == userInfo.UserId)).FirstOrDefault();

            if (contactBook == null)
            {
                return true;
            }
            var contactIds = contactBook.Contacts.Select(c => c.UserId);

            var filter = Builders<ContactBook>.Filter.And(
                Builders<ContactBook>.Filter.In(c => c.UserId, contactIds),
                Builders<ContactBook>.Filter.ElemMatch(c => c.Contacts, contact => contact.UserId == userInfo.UserId));


            var update = Builders<ContactBook>.Update
                .Set("Contacts.$.Name", userInfo.Name)
                .Set("Contacts.$.Avatar", userInfo.Avatar)
                .Set("Contacts.$.Company", userInfo.Company)
                .Set("Contacts.$.Phone", userInfo.Phone)
                .Set("Contacts.$.Title", userInfo.Title);

            var updateResult = _mongoContactDbContext.ContactBooks.UpdateMany(filter, update);

            return updateResult.MatchedCount == updateResult.ModifiedCount;

        }
    }
}
