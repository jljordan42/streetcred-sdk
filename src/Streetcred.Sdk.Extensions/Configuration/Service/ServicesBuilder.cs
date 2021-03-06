﻿using System;
using Microsoft.Extensions.DependencyInjection;

namespace Streetcred.Sdk.Extensions.Configuration.Service
{
    public class AgentServicesBuilder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AgentServicesBuilder"/> class.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <exception cref="System.ArgumentNullException">services</exception>
        public AgentServicesBuilder(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <summary>
        /// Services collection 
        /// </summary>
        public IServiceCollection Services { get; }
    }
}
