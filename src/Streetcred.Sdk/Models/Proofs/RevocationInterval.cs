﻿using Newtonsoft.Json;

namespace Streetcred.Sdk.Models.Proofs
{
    /// <summary>
    /// Proof revokation interval.
    /// </summary>
    public class RevocationInterval
    {
        /// <summary>
        /// Gets or sets from.
        /// </summary>
        /// <value>From.</value>
        [JsonProperty("from")]
        public uint From { get; set; }

        /// <summary>
        /// Gets or sets to.
        /// </summary>
        /// <value>To.</value>
        [JsonProperty("to")]
        public uint To { get; set; }
    }
}