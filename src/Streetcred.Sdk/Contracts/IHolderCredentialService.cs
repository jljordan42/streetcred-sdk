﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Hyperledger.Indy.PoolApi;
using Hyperledger.Indy.WalletApi;
using Sovrin.Agents.Model.Credentials;

namespace Streetcred.Sdk.Contracts
{
    public interface IHolderCredentialService
    {

        /// <summary>
        /// Stores the offer asynchronous.
        /// </summary>
        /// <param name="wallet">The wallet.</param>
        /// <param name="credentialOffer">The credential offer.</param>
        /// <param name="connectionId">The connection identifier.</param>
        /// <returns>The credential identifier of the stored record</returns>
        Task<string> StoreOfferAsync(Wallet wallet, CredentialOffer credentialOffer, string connectionId);

        /// <summary>
        /// Accepts the offer asynchronous.
        /// </summary>
        /// <param name="wallet">The wallet.</param>
        /// <param name="pool">The pool.</param>
        /// <param name="credentialId">The credential identifier.</param>
        /// <param name="values">The values.</param>
        /// <returns></returns>
        Task AcceptOfferAsync(Wallet wallet, Pool pool, string credentialId, Dictionary<string, string> values);


        /// <summary>
        /// Stores the issued credential in the designated wallet.
        /// </summary>
        /// <param name="pool">The pool.</param>
        /// <param name="wallet">The wallet.</param>
        /// <param name="credential">The credential.</param>
        /// <param name="connectionId">The connection identifier.</param>
        /// <returns></returns>
        Task StoreCredentialAsync(Pool pool, Wallet wallet, Credential credential, string connectionId);
    }
}