﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hyperledger.Indy.AnonCredsApi;
using Hyperledger.Indy.PoolApi;
using Hyperledger.Indy.WalletApi;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Streetcred.Sdk.Contracts;
using Streetcred.Sdk.Messages;
using Streetcred.Sdk.Messages.Proofs;
using Streetcred.Sdk.Models.Credentials;
using Streetcred.Sdk.Models.Proofs;
using Streetcred.Sdk.Models.Records;
using Streetcred.Sdk.Models.Records.Search;
using Streetcred.Sdk.Utils;

namespace Streetcred.Sdk.Runtime
{
    public class DefaultProofService : IProofService
    {
        protected readonly IRouterService RouterService;
        protected readonly IConnectionService ConnectionService;
        protected readonly IMessageSerializer MessageSerializer;
        protected readonly IWalletRecordService RecordService;
        protected readonly IProvisioningService ProvisioningService;
        protected readonly ILedgerService LedgerService;
        protected readonly ILogger<DefaultProofService> Logger;
        protected readonly ITailsService TailsService;

        public DefaultProofService(
            IConnectionService connectionService,
            IRouterService routerService,
            IMessageSerializer messageSerializer,
            IWalletRecordService recordService,
            IProvisioningService provisioningService,
            ILedgerService ledgerService,
            ITailsService tailsService,
            ILogger<DefaultProofService> logger)
        {
            TailsService = tailsService;
            ConnectionService = connectionService;
            RouterService = routerService;
            MessageSerializer = messageSerializer;
            RecordService = recordService;
            ProvisioningService = provisioningService;
            LedgerService = ledgerService;
            Logger = logger;
        }

        /// <inheritdoc />
        public virtual async Task SendProofRequestAsync(Wallet wallet, string connectionId, ProofRequest proofRequest)
        {
            Logger.LogInformation(LoggingEvents.SendProofRequest, "ConnectionId {0}", connectionId);

            var connection = await ConnectionService.GetAsync(wallet, connectionId);
            var request = await CreateProofRequestAsync(wallet, connectionId, proofRequest);

            await RouterService.ForwardAsync(new ForwardEnvelopeMessage
            {
                Content = request.ToJson(),
                Type = MessageUtils.FormatDidMessageType(connection.TheirDid, MessageTypes.Forward)
            }, connection.Endpoint);
        }

        /// <inheritdoc />
        public virtual async Task SendProofRequestAsync(Wallet wallet, string connectionId, string proofRequestJson)
        {
            Logger.LogInformation(LoggingEvents.SendProofRequest, "ConnectionId {0}", connectionId);

            var connection = await ConnectionService.GetAsync(wallet, connectionId);
            var request = await CreateProofRequestAsync(wallet, connectionId, proofRequestJson);

            await RouterService.ForwardAsync(new ForwardEnvelopeMessage
            {
                Content = request.ToJson(),
                Type = MessageUtils.FormatDidMessageType(connection.TheirDid, MessageTypes.Forward)
            }, connection.Endpoint);
        }

        /// <inheritdoc />
        public virtual async Task<ProofRequestMessage> CreateProofRequestAsync(Wallet wallet, string connectionId,
            ProofRequest proofRequest)
        {
            if (string.IsNullOrWhiteSpace(proofRequest.Nonce))
                throw new ArgumentNullException(nameof(proofRequest.Nonce), "Nonce must be set.");

            return await CreateProofRequestAsync(wallet, connectionId, proofRequest.ToJson());
        }

        /// <inheritdoc />
        public virtual async Task<ProofRequestMessage> CreateProofRequestAsync(Wallet wallet, string connectionId,
            string proofRequestJson)
        {
            Logger.LogInformation(LoggingEvents.CreateProofRequest, "ConnectionId {0}", connectionId);

            var connection = await ConnectionService.GetAsync(wallet, connectionId);
            var proofJobj = JObject.Parse(proofRequestJson);

            var proofRecord = new ProofRecord
            {
                Id = Guid.NewGuid().ToString(),
                ConnectionId = connection.ConnectionId,
                RequestJson = proofRequestJson
            };
            proofRecord.Tags[TagConstants.Nonce] = proofJobj["nonce"].ToObject<string>();
            proofRecord.Tags[TagConstants.ConnectionId] = connection.GetId();
            proofRecord.Tags[TagConstants.Role] = TagConstants.Requestor;

            await RecordService.AddAsync(wallet, proofRecord);

            var proofRequest = await MessageSerializer.PackSealedAsync<ProofRequestMessage>(
                new ProofRequestDetails {ProofRequestJson = proofRequestJson},
                wallet,
                connection.MyVk,
                connection.TheirVk);
            proofRequest.Type = MessageUtils.FormatDidMessageType(connection.TheirDid, MessageTypes.ProofRequest);

            return proofRequest;
        }

        /// <inheritdoc />
        public virtual async Task<string> ProcessProofAsync(Wallet wallet, ProofMessage proof)
        {
            var (didOrKey, _) = MessageUtils.ParseMessageType(proof.Type);

            var connectionSearch =
                await ConnectionService.ListAsync(wallet, new SearchRecordQuery {{TagConstants.MyDid, didOrKey}});
            if (!connectionSearch.Any())
                throw new Exception($"Can't find connection record for type {proof.Type}");
            var connection = connectionSearch.First();

            var (requestDetails, _) = await MessageSerializer.UnpackSealedAsync<ProofDetails>(
                proof.Content, wallet, connection.MyVk);
            var proofJson = requestDetails.ProofJson;

            var proofRecordSearch =
                await RecordService.SearchAsync<ProofRecord>(wallet,
                    new SearchRecordQuery {{ TagConstants.Nonce, requestDetails.RequestNonce}}, null, 1);
            if (!proofRecordSearch.Any())
                throw new Exception($"Can't find proof record");
            var proofRecord = proofRecordSearch.Single();

            proofRecord.ProofJson = proofJson;
            await proofRecord.TriggerAsync(ProofTrigger.Accept);
            await RecordService.UpdateAsync(wallet, proofRecord);

            return proofRecord.GetId();
        }

        /// <inheritdoc />
        public virtual async Task<string> ProcessProofRequestAsync(Wallet wallet, ProofRequestMessage proofRequest)
        {
            var (didOrKey, _) = MessageUtils.ParseMessageType(proofRequest.Type);

            var connectionSearch =
                await ConnectionService.ListAsync(wallet, new SearchRecordQuery {{TagConstants.MyDid, didOrKey}});
            if (!connectionSearch.Any())
                throw new Exception($"Can't find connection record for type {proofRequest.Type}");
            var connection = connectionSearch.First();

            var (requestDetails, _) =
                await MessageSerializer.UnpackSealedAsync<ProofRequestDetails>(proofRequest.Content, wallet,
                    connection.MyVk);
            var requestJson = requestDetails.ProofRequestJson;

            var offer = JObject.Parse(requestJson);
            var nonce = offer["nonce"].ToObject<string>();

            // Write offer record to local wallet
            var proofRecord = new ProofRecord
            {
                Id = Guid.NewGuid().ToString(),
                RequestJson = requestJson,
                ConnectionId = connection.GetId(),
                State = ProofState.Requested
            };
            proofRecord.Tags[TagConstants.ConnectionId] = connection.GetId();
            proofRecord.Tags[TagConstants.Nonce] = nonce;
            proofRecord.Tags[TagConstants.Role] = TagConstants.Holder;

            await RecordService.AddAsync(wallet, proofRecord);

            return proofRecord.GetId();
        }

        /// <inheritdoc />
        public virtual async Task<ProofMessage> CreateProofAsync(Wallet wallet, Pool pool, string proofRequestId,
            RequestedCredentials requestedCredentials)
        {
            var record = await RecordService.GetAsync<ProofRecord>(wallet, proofRequestId);
            var connection = await ConnectionService.GetAsync(wallet, record.ConnectionId);

            var provisioningRecord = await ProvisioningService.GetProvisioningAsync(wallet);

            var credentialObjects = new List<CredentialInfo>();
            foreach (var credId in requestedCredentials.GetCredentialIdentifiers())
            {
                credentialObjects.Add(
                    JsonConvert.DeserializeObject<CredentialInfo>(
                        await AnonCreds.ProverGetCredentialAsync(wallet, credId)));
            }

            var schemas = await BuildSchemasAsync(pool,
                credentialObjects
                    .Select(x => x.SchemaId)
                    .Distinct());

            var definitions = await BuildCredentialDefinitionsAsync(pool,
                credentialObjects
                    .Select(x => x.CredentialDefinitionId)
                    .Distinct());

            var revocationStates = await BuildRevocationStatesAsync(pool,
                credentialObjects,
                requestedCredentials);

            var proofJson = await AnonCreds.ProverCreateProofAsync(wallet, record.RequestJson,
                requestedCredentials.ToJson(), provisioningRecord.MasterSecretId, schemas, definitions,
                revocationStates);

            record.ProofJson = proofJson;
            await record.TriggerAsync(ProofTrigger.Accept);
            await RecordService.UpdateAsync(wallet, record);

            var proof = await MessageSerializer.PackSealedAsync<ProofMessage>(
                new ProofDetails
                {
                    ProofJson = proofJson,
                    RequestNonce = JsonConvert.DeserializeObject<ProofRequest>(record.RequestJson).Nonce
                }, wallet, connection.MyVk, connection.TheirVk);
            proof.Type = MessageUtils.FormatDidMessageType(connection.TheirDid, MessageTypes.DisclosedProof);

            return proof;
        }

        /// <inheritdoc />
        public virtual async Task AcceptProofRequestAsync(Wallet wallet, Pool pool, string proofRequestId,
            RequestedCredentials requestedCredentials)
        {
            var request = await RecordService.GetAsync<ProofRecord>(wallet, proofRequestId);
            var connection = await ConnectionService.GetAsync(wallet, request.ConnectionId);

            var proof = await CreateProofAsync(wallet, pool, proofRequestId, requestedCredentials);

            await RouterService.ForwardAsync(new ForwardEnvelopeMessage
            {
                Content = proof.ToJson(),
                Type = MessageUtils.FormatDidMessageType(connection.TheirDid, MessageTypes.Forward)
            }, connection.Endpoint);
        }

        /// <inheritdoc />
        public virtual async Task RejectProofRequestAsync(Wallet wallet, string proofRequestId)
        {
            var request = await RecordService.GetAsync<ProofRecord>(wallet, proofRequestId);
            await request.TriggerAsync(ProofTrigger.Reject);
            await RecordService.UpdateAsync(wallet, request);
        }

        /// <inheritdoc />
        public virtual async Task<bool> VerifyProofAsync(Wallet wallet, Pool pool, string proofRecId)
        {
            var proofRecord = await GetAsync(wallet, proofRecId);
            var proofObject = JsonConvert.DeserializeObject<Proof>(proofRecord.ProofJson);

            var schemas = await BuildSchemasAsync(pool,
                proofObject.Identifiers
                    .Select(x => x.SchemaId)
                    .Where(x => x != null)
                    .Distinct());

            var definitions = await BuildCredentialDefinitionsAsync(pool,
                proofObject.Identifiers
                    .Select(x => x.CredentialDefintionId)
                    .Where(x => x != null)
                    .Distinct());

            var revocationDefinitions = await BuildRevocationRegistryDefinitionsAsync(pool,
                proofObject.Identifiers
                    .Select(x => x.RevocationRegistryId)
                    .Where(x => x != null)
                    .Distinct());

            var revocationRegistries = await BuildRevocationRegistryDetlasAsync(pool,
                proofObject.Identifiers
                    .Where(x => x.RevocationRegistryId != null));

            return await AnonCreds.VerifierVerifyProofAsync(proofRecord.RequestJson, proofRecord.ProofJson, schemas,
                definitions, revocationDefinitions, revocationRegistries);
        }

        /// <inheritdoc />
        public virtual Task<List<ProofRecord>> ListAsync(Wallet wallet, SearchRecordQuery query = null, int count = 100) =>
            RecordService.SearchAsync<ProofRecord>(wallet, query, null, count);

        /// <inheritdoc />
        public virtual Task<ProofRecord> GetAsync(Wallet wallet, string proofRecId) =>
            RecordService.GetAsync<ProofRecord>(wallet, proofRecId);

        /// <inheritdoc />
        public virtual async Task<List<Credential>> ListCredentialsForProofRequestAsync(Wallet wallet,
            ProofRequest proofRequest, string attributeReferent)
        {
            using (var search =
                await AnonCreds.ProverSearchCredentialsForProofRequestAsync(wallet, proofRequest.ToJson()))
            {
                var searchResult = await search.NextAsync(attributeReferent, 100);
                return JsonConvert.DeserializeObject<List<Credential>>(searchResult);
            }
        }

        #region Private Methods
        private async Task<string> BuildSchemasAsync(Pool pool, IEnumerable<string> schemaIds)
        {
            var result = new Dictionary<string, JObject>();

            foreach (var schemaId in schemaIds)
            {
                var ledgerSchema = await LedgerService.LookupSchemaAsync(pool, null, schemaId);
                result.Add(schemaId, JObject.Parse(ledgerSchema.ObjectJson));
            }

            return result.ToJson();
        }

        private async Task<string> BuildCredentialDefinitionsAsync(Pool pool, IEnumerable<string> credentialDefIds)
        {
            var result = new Dictionary<string, JObject>();

            foreach (var schemaId in credentialDefIds)
            {
                var ledgerDefinition = await LedgerService.LookupDefinitionAsync(pool, null, schemaId);
                result.Add(schemaId, JObject.Parse(ledgerDefinition.ObjectJson));
            }

            return result.ToJson();
        }

        private async Task<string> BuildRevocationStatesAsync(Pool pool,
            IEnumerable<CredentialInfo> credentialObjects,
            RequestedCredentials requestedCredentials)
        {
            var allCredentials = new List<RequestedAttribute>();
            allCredentials.AddRange(requestedCredentials.RequestedAttributes.Values);
            allCredentials.AddRange(requestedCredentials.RequestedPredicates.Values);

            var result = new Dictionary<string, Dictionary<string, JObject>>();
            foreach (var requestedCredential in allCredentials)
            {
                // ReSharper disable once PossibleMultipleEnumeration
                var credential = credentialObjects.First(x => x.Referent == requestedCredential.CredentialId);
                if (credential.RevocationRegistryId == null)
                    continue;

                var timestamp = requestedCredential.Timestamp ??
                                throw new Exception(
                                    "Timestamp must be provided for credential that supports revocation");

                if (result.ContainsKey(credential.RevocationRegistryId) &&
                    result[credential.RevocationRegistryId].ContainsKey($"{timestamp}"))
                {
                    continue;
                }

                var registryDefinition =
                    await LedgerService.LookupRevocationRegistryDefinitionAsync(pool, null,
                        credential.RevocationRegistryId);

                var delta = await LedgerService.LookupRevocationRegistryDeltaAsync(pool,
                    credential.RevocationRegistryId, -1, timestamp);

                var tailsfile = await TailsService.EnsureTailsExistsAsync(pool, credential.RevocationRegistryId);
                var tailsReader = await TailsService.OpenTailsAsync(tailsfile);

                var state = await AnonCreds.CreateRevocationStateAsync(tailsReader, registryDefinition.ObjectJson,
                    delta.ObjectJson, (long) delta.Timestamp, credential.CredentialRevocationId);

                if (!result.ContainsKey(credential.RevocationRegistryId))
                    result.Add(credential.RevocationRegistryId, new Dictionary<string, JObject>());

                result[credential.RevocationRegistryId].Add($"{timestamp}", JObject.Parse(state));

                // TODO: Revocation state should provide the state between a certain period
                // that can be requested in the proof request in the 'non_revocation' field.
            }

            return result.ToJson();
        }

        private async Task<string> BuildRevocationRegistryDetlasAsync(Pool pool,
            IEnumerable<ProofIdentifier> proofIdentifiers)
        {
            var result = new Dictionary<string, Dictionary<string, JObject>>();

            foreach (var identifier in proofIdentifiers)
            {
                var delta = await LedgerService.LookupRevocationRegistryDeltaAsync(pool,
                    identifier.RevocationRegistryId,
                    -1,
                    long.Parse(identifier.Timestamp));

                result.Add(identifier.RevocationRegistryId,
                    new Dictionary<string, JObject>
                    {
                        {identifier.Timestamp, JObject.Parse(delta.ObjectJson)}
                    });
            }

            return result.ToJson();
        }

        private async Task<string> BuildRevocationRegistryDefinitionsAsync(Pool pool,
            IEnumerable<string> revocationRegistryIds)
        {
            var result = new Dictionary<string, JObject>();

            foreach (var revocationRegistryId in revocationRegistryIds)
            {
                var ledgerSchema =
                    await LedgerService.LookupRevocationRegistryDefinitionAsync(pool, null, revocationRegistryId);
                result.Add(revocationRegistryId, JObject.Parse(ledgerSchema.ObjectJson));
            }

            return result.ToJson();
        }
        #endregion
    }
}