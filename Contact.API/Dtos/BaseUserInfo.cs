using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Contact.API.Dtos
{
    public class BaseUserInfo
    {
        public int UserId { get; set; }

        public string Name { get; set; }

        public string Company { get; set; }

        public string Title { get; set; }
        /// <summary>
        /// 头像
        /// </summary>
        public string Avatar { get; set; }

        public string Phone { get; set; }
    }
}
