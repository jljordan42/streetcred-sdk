﻿using Newtonsoft.Json;

namespace Sovrin.Agents.Model.Credentials
{
    public class Credential : IContentMessage
    {

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        [JsonProperty("@type")]
        public string Type { get; set; } = MessageTypes.Credential;

        public string Content { get; set; }
    }
}