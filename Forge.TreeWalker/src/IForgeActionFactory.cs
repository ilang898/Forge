//-----------------------------------------------------------------------
// <copyright file="IForgeActionFactory.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
//     The IForgeActionFactory interface.
// </summary>
//-----------------------------------------------------------------------

namespace Microsoft.Forge.TreeWalker
{
    using System;

    /// <summary>
    /// The IForgeActionFactory interface defines a factory for creating ForgeAction instances.
    /// Implement this interface to integrate a dependency injection container of your choice.
    /// </summary>
    public interface IForgeActionFactory
    {
        /// <summary>
        /// Creates an instance of the specified ForgeAction type.
        /// </summary>
        /// <param name="actionType">The Type of the ForgeAction class to instantiate. This type derives from <see cref="BaseAction"/>.</param>
        /// <param name="parameters">The TreeWalkerParameters for the current session. Provided for native actions that require it (e.g. SubroutineAction).</param>
        /// <returns>An instance of the specified action type.</returns>
        BaseAction CreateAction(Type actionType, TreeWalkerParameters parameters);
    }
}
