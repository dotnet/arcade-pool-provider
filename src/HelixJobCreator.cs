// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Hosting;
using Microsoft.DotNet.Helix.Client;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.HelixPoolProvider.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HelixPoolProvider
{
    /// <summary>
    /// Creates a job on a specific helix queue
    /// </summary>
    public abstract class HelixJobCreator
    {
        protected AgentAcquireItem _agentRequestItem;
        protected QueueInfo _queueInfo;
        protected IHelixApi _api;
        protected ILogger _logger;
        protected Config _configuration;
        protected IHostingEnvironment _hostingEnvironment;
        protected string _orchestrationId;
        protected string _jobName;

        protected HelixJobCreator(AgentAcquireItem agentRequestItem, QueueInfo queueInfo, IHelixApi api,
            ILoggerFactory loggerFactory, IHostingEnvironment hostingEnvironment,
            Config configuration, string orchestrationId, string jobName)
        {
            _agentRequestItem = agentRequestItem;
            _queueInfo = queueInfo;
            _api = api;
            _logger = loggerFactory.CreateLogger<HelixJobCreator>();
            _configuration = configuration;
            _hostingEnvironment = hostingEnvironment;
            _orchestrationId = orchestrationId;
            _jobName = jobName;
        }

        public abstract string ConstructCommand();

        /// <summary>
        /// Construct the payload script file.  Right now this file is written to temporary and then read in WithFiles
        /// </summary>
        /// <returns></returns>
        public string CreateAgentSettingsPayload()
        {
            return SerializeAndWrite(_agentRequestItem.agentConfiguration.agentSettings, ".agent");
        }

        public string CreateAgentCredentialsPayload()
        {
            return SerializeAndWrite(_agentRequestItem.agentConfiguration.agentCredentials, ".credentials");
        }

        private string SerializeAndWrite(object jsonNode, string fileName)
        {
            string agentSettingsNode = JsonConvert.SerializeObject(jsonNode);

            string tempPath = Path.Combine(System.IO.Path.GetTempPath(), _agentRequestItem.agentId);
            Directory.CreateDirectory(tempPath);
            string fullFilePath = Path.Combine(tempPath, fileName);

            using (StreamWriter writer = new StreamWriter(fullFilePath))
            {
                writer.Write(agentSettingsNode);
            }
            return fullFilePath;
        }

        public abstract Uri AgentPayloadUri { get; }

        public abstract string StartupScriptName { get; }

        public string StartupScriptPath => _hostingEnvironment.WebRootFileProvider.GetFileInfo(Path.Combine("startupscripts", StartupScriptName)).PhysicalPath;

        public async Task<AgentInfoItem> CreateJob(CancellationToken cancellationToken)
        {
            string credentialsPath = null;
            string agentSettingsPath = null;
            ISentJob job = null;

            try
            {
                _logger.LogInformation($"Creating payloads for agent id {_agentRequestItem.agentId}");

                credentialsPath = CreateAgentCredentialsPayload();
                agentSettingsPath = CreateAgentSettingsPayload();

                // Now that we have a valid queue, construct the Helix job on that queue

                /// Notes: if we timeout, it's going to be in the subsequent call.  
                /// SendAsync() causes both the storage account container creation / uploads and sends to Helix API, which both can stall.
                /// We have to do this this way (and non-ideal workarounds like trying to destroy the object won't likely solve this) because today
                /// the job, if started, will still contain valid Azure DevOps tokens to be a build agent and we can't easily guarantee it doesn't get sent.
                /// ********************************
                /// The right long-term fix is for Azure DevOps to not have the tokens passed be usable until they've received an "accepted = true" response from our provider.
                /// ********************************
                cancellationToken.ThrowIfCancellationRequested();
                job = await _api.Job.Define()
                    .WithType($"byoc/{_configuration.HelixCreator}/")
                    .WithTargetQueue(_queueInfo.QueueId)
                    .WithContainerName(_configuration.ContainerName)
                    .WithCorrelationPayloadUris(AgentPayloadUri)
                    .WithSource($"agent/{_agentRequestItem.accountId}/{_orchestrationId}/{_jobName}/")
                    .DefineWorkItem(_agentRequestItem.agentId)
                    .WithCommand(ConstructCommand())
                    .WithFiles(credentialsPath, agentSettingsPath, StartupScriptPath)
                    .WithTimeout(TimeSpan.FromMinutes((double)_configuration.TimeoutInMinutes))
                    .AttachToJob()
                    .SendAsync(null, cancellationToken);
                _logger.LogInformation($"Successfully submitted new Helix job {job.CorrelationId} (Agent id {_agentRequestItem.agentId}) to queue { _queueInfo.QueueId}");

                // TODO Add extra info into the agent info item blob
                return new AgentInfoItem()
                {
                    accepted = true,
                    agentData = new AgentDataItem() { correlationId = job.CorrelationId,
                                                      queueId = _queueInfo.QueueId,
                                                      workItemId = _agentRequestItem.agentId,
                                                      isPublicQueue = !_queueInfo.IsInternalOnly.Value }
                };
            }
            catch (HttpOperationException e)
            {
                _logger.LogError(e, $"Failed to submit new Helix job to queue {_queueInfo.QueueId} for agent id {_agentRequestItem.agentId}: {e.Response.Content}");

                return new AgentInfoItem() { accepted = false };
            }
            catch (OperationCanceledException ranOutOfTime) when (ranOutOfTime.CancellationToken == cancellationToken)
            {
                _logger.LogError($"Unable to complete request to create Helix job in specified timeout, attempting to cancel it.");
                if (job != null && !string.IsNullOrEmpty(job.CorrelationId))
                {
                    await _api.Job.CancelAsync(job.CorrelationId);
                    _logger.LogError($"Possible race condition: cancelled Helix Job '{job.CorrelationId}' may still run.");
                }
                return new AgentInfoItem() { accepted = false };
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to submit new Helix job to queue {_queueInfo.QueueId} for agent id {_agentRequestItem.agentId}");

                // TODO Add extra info into the agent info item blob
                return new AgentInfoItem() { accepted = false };
            }
            finally
            {
                if (credentialsPath != null)
                {
                    // Delete the temporary files containing the credentials and agent config
                    System.IO.File.Delete(credentialsPath);
                }
                if (agentSettingsPath != null)
                {
                    System.IO.File.Delete(agentSettingsPath);
                }
            }
        }
    }
}
