﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Hyperledger.Indy.WalletApi;
using Moq;
using Newtonsoft.Json;
using Sovrin.Agents.Model;
using Sovrin.Agents.Model.Connections;
using Streetcred.Sdk.Contracts;
using Streetcred.Sdk.Model.Records;
using Streetcred.Sdk.Runtime;
using Xunit;

namespace Streetcred.Sdk.Tests
{
    public class ConnectionTests : IDisposable
    {
        private const string IssuerConfig = "{\"id\":\"issuer_test_wallet\"}";
        private const string HolderConfig = "{\"id\":\"holder_test_wallet\"}";
        private const string Credentials = "{\"key\":\"test_wallet_key\"}";

        private readonly Wallet _issuerWallet;
        private readonly Wallet _holderWallet;

        private readonly IConnectionService _connectionService;

        private readonly ConcurrentBag<IEnvelopeMessage> _messages;

        public ConnectionTests()
        {
            _messages = new ConcurrentBag<IEnvelopeMessage>();
            var messageSerializer = new MessageSerializer();

            var createIssuerTask = Wallet.CreateWalletAsync(IssuerConfig, Credentials);
            var openIssuerTask = Wallet.OpenWalletAsync(IssuerConfig, Credentials);

            var createHolderTask = Wallet.CreateWalletAsync(HolderConfig, Credentials);
            var openHolderTask = Wallet.OpenWalletAsync(HolderConfig, Credentials);

            Task.WaitAll(createIssuerTask
                .ContinueWith(async _ => await openIssuerTask)
                .ContinueWith(async _ => await createHolderTask)
                .ContinueWith(async _ => await openHolderTask));

            _issuerWallet = openIssuerTask.Result;
            _holderWallet = openHolderTask.Result;

            var routingMock = new Mock<IRouterService>();
            routingMock.Setup(x => x.ForwardAsync(It.IsNotNull<IEnvelopeMessage>(), It.IsAny<AgentEndpoint>()))
                .Callback((IEnvelopeMessage content, AgentEndpoint endpoint) => { _messages.Add(content); })
                .Returns(Task.CompletedTask);

            var endpointMock = new Mock<IEndpointService>();
            endpointMock.Setup(x => x.GetEndpointAsync(It.IsAny<Wallet>()))
                .Returns(Task.FromResult(new AgentEndpoint()));

            _connectionService = new ConnectionService(new WalletRecordService(), routingMock.Object,
                new Mock<IEndpointService>().Object, messageSerializer);
        }

        [Fact]
        public async Task CanCreateInvitationAsync()
        {
            var connectionId = Guid.NewGuid().ToString();

            var invitation = await _connectionService.CreateInvitationAsync(_issuerWallet, connectionId);

            var connection = await _connectionService.GetAsync(_issuerWallet, connectionId);

            Assert.Equal(ConnectionState.Negotiating, connection.State);
            Assert.Equal(connectionId, connection.GetId());
        }

        [Fact]
        public async Task CanEstablishConnectionAsync()
        {
            // Create invitation by the issuer
            var issuerConnectionId = Guid.NewGuid().ToString();
            var invitation = await _connectionService.CreateInvitationAsync(_issuerWallet, issuerConnectionId);
            var connectionIssuer = await _connectionService.GetAsync(_issuerWallet, issuerConnectionId);

            // Holder accepts invitation and sends a message request
            var holderConnectionId = await _connectionService.AcceptInvitationAsync(_holderWallet, invitation);
            var connectionHolder = await _connectionService.GetAsync(_holderWallet, holderConnectionId);

            // Issuer processes incoming message
            var issuerMessage = _messages.OfType<ForwardToKeyEnvelopeMessage>()
                .First(x => x.Key == connectionIssuer.Tags.Single(item => item.Key == "connectionKey").Value);

            var requestMessage = GetContentMessage(issuerMessage) as ConnectionRequest;
            Assert.NotNull(requestMessage);

            // Issuer stores and accepts the request
            await _connectionService.StoreRequestAsync(_issuerWallet, requestMessage);
            await _connectionService.AcceptRequestAsync(_issuerWallet, issuerConnectionId);

            // Holder processes incoming message
            var holderMessage = _messages.OfType<ForwardEnvelopeMessage>()
                .First(x => x.To == connectionHolder.MyDid);

            var responseMessage = GetContentMessage(holderMessage) as ConnectionResponse;
            Assert.NotNull(responseMessage);

            // Holder accepts response message
            await _connectionService.AcceptResponseAsync(_holderWallet, responseMessage);

            // Retrieve updated connection state for both issuer and holder
            connectionIssuer = await _connectionService.GetAsync(_issuerWallet, issuerConnectionId);
            connectionHolder = await _connectionService.GetAsync(_holderWallet, holderConnectionId);

            Assert.Equal(ConnectionState.Connected, connectionIssuer.State);
            Assert.Equal(ConnectionState.Connected, connectionHolder.State);

            Assert.Equal(connectionIssuer.MyDid, connectionHolder.TheirDid);
            Assert.Equal(connectionIssuer.TheirDid, connectionHolder.MyDid);
        }

        private IContentMessage GetContentMessage(IEnvelopeMessage message)
            => JsonConvert.DeserializeObject<IContentMessage>(message.Content);

        public void Dispose()
        {
            _issuerWallet.CloseAsync().Wait();
            _holderWallet.CloseAsync().Wait();
            Wallet.DeleteWalletAsync(IssuerConfig, Credentials).Wait();
            Wallet.DeleteWalletAsync(HolderConfig, Credentials).Wait();
        }
    }
}