using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contact.API.Models;
using MongoDB.Driver;

namespace Contact.API.Data
{
    public class MongoContactApplyRequestRepository : IContactApplyRequestRepository
    {
        private readonly MongoContactDbContext _mongoContactDbContext;

        public MongoContactApplyRequestRepository(MongoContactDbContext mongoContactDbContext)
        {
            _mongoContactDbContext = mongoContactDbContext;
        }

        public async Task<bool> AddReqeustAsync(ContactApplyRequest request, CancellationToken cancellationToken)
        {
            var filter = Builders<ContactApplyRequest>.Filter.Where(r => r.UserId == request.UserId
                                                                         && r.ApplierId == request.ApplierId);
            if ((await _mongoContactDbContext.ContactApplyRequests.FindSync(filter, cancellationToken: cancellationToken).ToListAsync(cancellationToken: cancellationToken)).Any())
            {
                var update = Builders<ContactApplyRequest>.Update.Set(r => r.ApplyTime, DateTime.Now);

                var result =
                    await _mongoContactDbContext.ContactApplyRequests.UpdateOneAsync(filter, update, null,
                        cancellationToken);
                return result.MatchedCount == result.ModifiedCount && result.MatchedCount == 1;
            }

            await _mongoContactDbContext.ContactApplyRequests.InsertOneAsync(request, null, cancellationToken);
            return true;
        }

        public async Task<bool> ApprovalAsync(int userId, int applierId, CancellationToken cancellationToken)
        {
            var filter = Builders<ContactApplyRequest>.Filter.Where(r => r.UserId == userId
                                                                         && r.ApplierId == applierId);

            var update = Builders<ContactApplyRequest>.Update
                .Set(r => r.HandledTime, DateTime.Now)
                .Set(r => r.Approvaled, 1);

            var result =
                await _mongoContactDbContext.ContactApplyRequests.UpdateOneAsync(filter, update, null,
                    cancellationToken);
            return result.MatchedCount == result.ModifiedCount && result.MatchedCount == 1;


        }

        public async Task<List<ContactApplyRequest>> GetRequsetListAsync(int userId, CancellationToken cancellationToken)
        {
            return (await _mongoContactDbContext.ContactApplyRequests.FindAsync(t => t.UserId == userId, cancellationToken: cancellationToken)).ToList(cancellationToken);
        }
    }
}
