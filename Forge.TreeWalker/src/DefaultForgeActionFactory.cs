//-----------------------------------------------------------------------
// <copyright file="DefaultForgeActionFactory.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
//     The DefaultForgeActionFactory class.
// </summary>
//-----------------------------------------------------------------------

namespace Microsoft.Forge.TreeWalker
{
    using System;

    /// <summary>
    /// The default IForgeActionFactory implementation that uses Activator.CreateInstance to instantiate ForgeAction classes.
    /// This preserves the original behavior of Forge when no custom factory is provided.
    /// </summary>
    public class DefaultForgeActionFactory : IForgeActionFactory
    {
        /// <summary>
        /// Creates an instance of the specified ForgeAction type using Activator.CreateInstance.
        /// Native actions such as SubroutineAction are handled by the TreeWalkerSession before this factory is called.
        /// </summary>
        /// <param name="actionType">The Type of the ForgeAction class to instantiate.</param>
        /// <param name="parameters">The TreeWalkerParameters for the current session.</param>
        /// <returns>An instance of the specified action type.</returns>
        public BaseAction CreateAction(Type actionType, TreeWalkerParameters parameters)
        {
            if (actionType == typeof(SubroutineAction))
            {
                return (SubroutineAction)Activator.CreateInstance(actionType, parameters);
            }
            return (BaseAction)Activator.CreateInstance(actionType);
        }
    }
}
