using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;

namespace Contact.API.Models
{
    //忽略其他的字段
    [BsonIgnoreExtraElements]
    public class ContactBook
    {
        public ContactBook()
        {
            Contacts = new List<Contact>();
        }

        public int UserId { get; set; }

        /// <summary>
        /// 联系人列表
        /// </summary>
        public List<Contact> Contacts { get; set; }


    }
}
