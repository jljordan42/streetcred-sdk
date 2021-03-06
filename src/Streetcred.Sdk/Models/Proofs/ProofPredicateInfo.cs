﻿using Newtonsoft.Json;

namespace Streetcred.Sdk.Models.Proofs
{
    /// <inheritdoc />
    public class ProofPredicateInfo : ProofAttributeInfo
    {
        /// <summary>
        /// Gets or sets the type of the predicate.
        /// </summary>
        /// <value>The type of the predicate.</value>
        [JsonProperty("p_type")]
        public string PredicateType { get; set; }

        /// <summary>
        /// Gets or sets the predicate value.
        /// </summary>
        /// <value>The predicate value.</value>
        [JsonProperty("p_value")]
        public string PredicateValue { get; set; }
    }
}