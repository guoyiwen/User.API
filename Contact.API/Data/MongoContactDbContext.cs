using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contact.API.Common;
using Contact.API.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Contact.API.Data
{
    public class MongoContactDbContext
    {

        private readonly IMongoDatabase _mongoDatabase;
          
        private readonly IMongoClient _mongoClient;

        private readonly AppSettings _appSettings;


        public MongoContactDbContext(IOptionsSnapshot<AppSettings> optionsSnapshot)
        {
            _appSettings = optionsSnapshot.Value;
            if (_mongoClient == null)
            {
                _mongoClient = new MongoClient(optionsSnapshot.Value.MongoContactConnectionString);
            }
            if (_mongoDatabase == null)
            {
                _mongoDatabase = _mongoClient.GetDatabase(optionsSnapshot.Value.MongoDbDatabase);
            }

        }
        private void CheckOrCreateCollection(string name)
        {
            var list = _mongoDatabase.ListCollections().ToList().Select(x => x["name"].AsString);
            if (!list.Contains(name))
            {
                _mongoDatabase.CreateCollection(name);
            }
        }
        /// <summary>
        /// 用户通讯录
        /// </summary>
        public IMongoCollection<ContactBook> ContactBooks
        {
            get
            {
                CheckOrCreateCollection("ContactBooks");
                return _mongoDatabase.GetCollection<ContactBook>("ContactBooks");
            }
        }
        /// <summary>
        /// 好友申请请求记录
        /// </summary>
        public IMongoCollection<ContactApplyRequest> ContactApplyRequests
        {
            get
            {
                CheckOrCreateCollection("ContactApplyRequest");
                return _mongoDatabase.GetCollection<ContactApplyRequest>("ContactApplyRequest");
            }
        }

    }
}
