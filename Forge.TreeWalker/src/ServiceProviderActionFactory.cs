// <copyright file="ServiceProviderActionFactory.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
//     The ServiceProviderActionFactory class.
// </summary>
//-----------------------------------------------------------------------

namespace Microsoft.Forge.TreeWalker
{
    using System;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// An <see cref="IForgeActionFactory"/> implementation that resolves ForgeAction instances
    /// from an <see cref="IServiceProvider"/> backed by Microsoft.Extensions.DependencyInjection.
    /// 
    /// Usage:
    ///   1. Register your ForgeAction types in the DI container (e.g. services.AddTransient&lt;MyAction&gt;()).
    ///   2. Set <see cref="TreeWalkerParameters.ServiceProvider"/> to your <see cref="IServiceProvider"/> instance,
    ///      or set <see cref="TreeWalkerParameters.ActionFactory"/> to a new instance of this class.
    /// 
    /// Native actions such as <see cref="SubroutineAction"/> are handled by the TreeWalkerSession
    /// before this factory is called, so custom factories do not need to know about them.
    /// </summary>
    public class ServiceProviderActionFactory : IForgeActionFactory
    {
        private readonly IServiceProvider serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceProviderActionFactory"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve ForgeAction instances.</param>
        public ServiceProviderActionFactory(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Creates an instance of the specified ForgeAction type by resolving it from the service provider.
        /// Falls back to <see cref="ActivatorUtilities.CreateInstance"/> if the type is not registered,
        /// which allows constructor injection of registered services even for unregistered action types.
        /// Native actions such as <see cref="SubroutineAction"/> are handled by the TreeWalkerSession
        /// before this factory is called.
        /// </summary>
        /// <param name="actionType">The Type of the ForgeAction class to instantiate.</param>
        /// <param name="parameters">The TreeWalkerParameters for the current session.</param>
        /// <returns>An instance of the specified action type.</returns>
        public BaseAction CreateAction(Type actionType, TreeWalkerParameters parameters)
        {
            // Try to resolve from the container first.
            object action = this.serviceProvider.GetService(actionType);
            if (action != null)
            {
                return (BaseAction)action;
            }

            // Not registered directly – use ActivatorUtilities which supports constructor injection
            // from the container while also accepting extra parameters.
            if (actionType == typeof(SubroutineAction))
            {
                return (SubroutineAction)ActivatorUtilities.CreateInstance(this.serviceProvider, actionType, parameters);
            }
            return (BaseAction)ActivatorUtilities.CreateInstance(this.serviceProvider, actionType);
        }
    }
}
