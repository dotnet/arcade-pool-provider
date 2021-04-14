using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AgentRequestJobStep
    {
        public enum TaskAgentJobStepType
        {
            Task = 1,
            Action = 2,
            Script = 3,
        }

        public TaskAgentJobStepType Type { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public string Condition { get; set; }
        public bool ContinueOnError { get; set; }
        public int TimeoutInMinutes { get; set; }
        public AgentRequestJobTask Task { get; set; }
        public IDictionary<string, string> Env { get; set; }
        public IDictionary<string, string> Inputs { get; set; }
    }
}
