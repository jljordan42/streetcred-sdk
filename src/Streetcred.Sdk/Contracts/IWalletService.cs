﻿using System.Threading.Tasks;
using Hyperledger.Indy.WalletApi;
using Streetcred.Sdk.Model.Wallets;

namespace Streetcred.Sdk.Contracts
{
    /// <summary>
    /// Wallet service.
    /// </summary>
    public interface IWalletService
    {
        /// <summary>
        /// Gets the wallet async.
        /// </summary>
        /// <returns>The wallet async.</returns>
        /// <param name="configuration">Configuration.</param>
        /// <param name="credentials">Credentials.</param>
        Task<Wallet> GetWalletAsync(WalletConfiguration configuration, WalletCredentials credentials);

        /// <summary>
        /// Creates the wallet async.
        /// </summary>
        /// <returns>The wallet async.</returns>
        /// <param name="configuration">Configuration.</param>
        /// <param name="credentials">Credentials.</param>
        Task CreateWalletAsync(WalletConfiguration configuration, WalletCredentials credentials);
    }
}