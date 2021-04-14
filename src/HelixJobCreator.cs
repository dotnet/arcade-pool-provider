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
using System.Collections.Generic;
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

        public async Task<AgentInfoItem> CreateJob(
            CancellationToken cancellationToken,
            string buildBranchName,
            Dictionary<string, string> properties)
        {
            string credentialsPath = null;
            string agentSettingsPath = null;
            ISentJob sentJob = null;

            try
            {
                _logger.LogInformation($"Creating payloads for agent id {_agentRequestItem.agentId}");

                credentialsPath = CreateAgentCredentialsPayload();
                agentSettingsPath = CreateAgentSettingsPayload();

                // Now that we have a valid queue, construct the Helix job on that queue

                // Notes: if we timeout, it's going to be in the subsequent call.  
                // SendAsync() causes both the storage account container creation / uploads and sends to Helix API, which both can stall.
                // We have to do this this way (and non-ideal workarounds like trying to destroy the object won't likely solve this) because today
                // the job, if started, will still contain valid Azure DevOps tokens to be a build agent and we can't easily guarantee it doesn't get sent.
                // ********************************
                // The right long-term fix is for Azure DevOps to not have the tokens passed be usable until they've received an "accepted = true" response from our provider.
                // ********************************
                cancellationToken.ThrowIfCancellationRequested();
                IJobDefinition preparedJob = _api.Job.Define()
                    .WithType($"byoc/{_configuration.HelixCreator}/")
                    .WithTargetQueue(_queueInfo.QueueId);

                if (properties != null)
                {
                    foreach ((string key, string value) in properties)
                    {
                        preparedJob.WithProperty(key, value);
                    }
                }

                var source = $"agent/{_agentRequestItem.accountId}/{_orchestrationId}/{_jobName}/";
                if (!string.IsNullOrEmpty(buildBranchName))
                    source += $"/{buildBranchName}";

                preparedJob = preparedJob.WithContainerName(_configuration.ContainerName)
                    .WithCorrelationPayloadUris(AgentPayloadUri)
                    .WithSource(source);

                IWorkItemDefinition workitem = preparedJob.DefineWorkItem(_agentRequestItem.agentId)
                    .WithCommand(ConstructCommand())
                    .WithFiles(credentialsPath, agentSettingsPath, StartupScriptPath)
                    .WithTimeout(TimeSpan.FromMinutes(_configuration.TimeoutInMinutes));

                preparedJob = workitem.AttachToJob();

                sentJob = await preparedJob.SendAsync(l => _logger.LogInformation(l), cancellationToken);
                _logger.LogInformation($"Successfully submitted new Helix job {sentJob.CorrelationId} (Agent id {_agentRequestItem.agentId}) to queue { _queueInfo.QueueId}");

                // In case the cancellation token got signalled between above and here, let's try to cancel the Helix Job.
                cancellationToken.ThrowIfCancellationRequested();

                return new AgentInfoItem
                {
                    accepted = true,
                    agentData = new AgentDataItem
                    {
                        correlationId = sentJob.CorrelationId,
                        queueId = _queueInfo.QueueId,
                        workItemId = _agentRequestItem.agentId,
                        isPublicQueue = !_queueInfo.IsInternalOnly.GetValueOrDefault(true)
                    }
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
                if (sentJob != null && !string.IsNullOrEmpty(sentJob.CorrelationId))
                {
                    await _api.Job.CancelAsync(sentJob.CorrelationId);
                    _logger.LogError($"Possible race condition: cancelled Helix Job '{sentJob.CorrelationId}' may still run.");
                }
                return new AgentInfoItem() { accepted = false };
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to submit new Helix job to queue {_queueInfo.QueueId} for agent id {_agentRequestItem.agentId}");
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
