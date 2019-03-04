using Project.API.Applications.IntegrationEvents;
namespace Project.API.Applications.IntegrationEvents
{
    public class ProjectCreatedIntergrationEvent : IntegrationEvent
    {
        public int UserId { get; set; }
        public string Company { get; set; }
        public int ProjectId { get; set; }
        public string Introduction { get; set; }
    }
}
