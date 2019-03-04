using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ReCommand.API.EFData.Entities
{
    public class ProjectReCommand
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int FromUserId { get; set; }
        public string FromUserName { get; set; }
        public string FromUserAvator { get; set; }
        public EnumReCommandType EnumReCommandType { get; set;  }
        public int ProjectId { get; set; }

        /// <summary>
        /// 项目logo
        /// </summary>
        public string ProjectAvator { get; set; }

        /// <summary>
        /// 公司名称
        /// </summary>
        public string Company { get; set; }

        /// <summary>
        /// 项目介绍
        /// </summary>
        public string Introduction { get; set; }

        /// <summary>
        /// 项目标签
        /// </summary>
        public string Tags { get; set; }
        /// <summary>
        /// 融资阶段
        /// </summary>
        public string FinStage { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime ReCommandTime { get; set; }
        public List<ProjectReferenceUser> ProjectReferenceUsers { get; set; }
    }
}
