using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AgentRequestJobData
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Container { get; set; }
        public IList<JObject> Steps { get; set; }
        public IDictionary<string, string> SidecarContainers { get; set; }
        public IList<AgentRequestJobVariable> Variables { get; set; }

        public AgentRequestJobVariable FindVariableByName(string name)
            => Variables.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
