using System;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AgentRequestJob
    {
        public int PoolId { get; set; }
        public Guid ProjectId { get; set; }
        public Guid PlanId { get; set; }
        public string PlanType { get; set; }
        public AgentRequestJobOwnerReference Run { get; set; }
        public AgentRequestJobOwnerReference Definition { get; set; }
        public AgentRequestJobData Job { get; set; }
    }
}
