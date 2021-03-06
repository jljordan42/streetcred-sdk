﻿namespace Streetcred.Sdk.Models
{
    /// <summary>
    /// An object for containing agent endpoint information.
    /// </summary>
    public class AgentEndpoint
    {
        /// <summary>
        /// Gets or sets the did of the agent.
        /// </summary>
        /// <value>
        /// The did of the agent.
        /// </value>
        public string Did { get; set; }

        /// <summary>
        /// Gets or sets the verkey of the agent.
        /// </summary>
        /// <value>
        /// The verkey of the agent.
        /// </value>
        public string Verkey { get; set; }

        /// <summary>
        /// Gets or sets the uri of the agent.
        /// </summary>
        /// <value>
        /// The uri of the agent.
        /// </value>
        public string Uri { get; set; }
    }
}