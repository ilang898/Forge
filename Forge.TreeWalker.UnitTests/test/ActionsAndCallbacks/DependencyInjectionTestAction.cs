//-----------------------------------------------------------------------
// <copyright file="DependencyInjectionTestAction.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
//     ForgeActions used to test dependency injection capabilities.
// </summary>
//-----------------------------------------------------------------------

namespace Microsoft.Forge.TreeWalker.UnitTests
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.Forge.Attributes;
    using Microsoft.Forge.TreeWalker;

    /// <summary>
    /// A simple service interface used to verify constructor injection in ForgeActions.
    /// </summary>
    public interface IDiTestService
    {
        string GetValue();
    }

    /// <summary>
    /// Default implementation of IDiTestService.
    /// </summary>
    public class DiTestService : IDiTestService
    {
        private readonly string value;

        public DiTestService(string value)
        {
            this.value = value;
        }

        public string GetValue()
        {
            return this.value;
        }
    }

    /// <summary>
    /// A second service interface used to verify multi-dependency constructor injection.
    /// </summary>
    public interface IDiTestCounter
    {
        int Increment();
        int GetCount();
    }

    /// <summary>
    /// Default implementation of IDiTestCounter.
    /// </summary>
    public class DiTestCounter : IDiTestCounter
    {
        private int count;

        public int Increment()
        {
            return ++this.count;
        }

        public int GetCount()
        {
            return this.count;
        }
    }

    /// <summary>
    /// A ForgeAction that requires a single IDiTestService dependency via constructor injection.
    /// Used to verify that ServiceProviderActionFactory resolves actions with injected services.
    /// </summary>
    [ForgeAction]
    public class SingleDependencyAction : BaseAction
    {
        private readonly IDiTestService testService;

        public SingleDependencyAction(IDiTestService testService)
        {
            this.testService = testService ?? throw new ArgumentNullException(nameof(testService));
        }

        public override Task<ActionResponse> RunAction(ActionContext actionContext)
        {
            return Task.FromResult(new ActionResponse
            {
                Status = "Success",
                Output = this.testService.GetValue()
            });
        }
    }

    /// <summary>
    /// A ForgeAction that requires multiple dependencies via constructor injection.
    /// Used to verify that ServiceProviderActionFactory resolves actions with multiple injected services.
    /// </summary>
    [ForgeAction]
    public class MultipleDependencyAction : BaseAction
    {
        private readonly IDiTestService testService;
        private readonly IDiTestCounter testCounter;

        public MultipleDependencyAction(IDiTestService testService, IDiTestCounter testCounter)
        {
            this.testService = testService ?? throw new ArgumentNullException(nameof(testService));
            this.testCounter = testCounter ?? throw new ArgumentNullException(nameof(testCounter));
        }

        public override Task<ActionResponse> RunAction(ActionContext actionContext)
        {
            int count = this.testCounter.Increment();

            return Task.FromResult(new ActionResponse
            {
                Status = this.testService.GetValue(),
                StatusCode = count,
                Output = string.Format("{0}_{1}", this.testService.GetValue(), count)
            });
        }
    }

    /// <summary>
    /// A ForgeAction with a typed input and constructor-injected dependency.
    /// Used to verify that DI works alongside Forge's ActionInput deserialization.
    /// </summary>
    [ForgeAction(InputType: typeof(DiActionWithInputTypeInput))]
    public class DiActionWithInputType : BaseAction
    {
        private readonly IDiTestService testService;

        public DiActionWithInputType(IDiTestService testService)
        {
            this.testService = testService ?? throw new ArgumentNullException(nameof(testService));
        }

        public override Task<ActionResponse> RunAction(ActionContext actionContext)
        {
            var input = (DiActionWithInputTypeInput)actionContext.ActionInput;

            return Task.FromResult(new ActionResponse
            {
                Status = "Success",
                Output = string.Format("{0}_{1}", this.testService.GetValue(), input.MessageProperty)
            });
        }
    }

    public class DiActionWithInputTypeInput
    {
        public string MessageProperty { get; set; }
    }

    /// <summary>
    /// A custom IForgeActionFactory implementation for testing.
    /// Tracks how many times CreateAction was called.
    /// </summary>
    public class TestCustomActionFactory : IForgeActionFactory
    {
        private readonly IDiTestService testService;
        public int CreateActionCallCount { get; private set; }

        public TestCustomActionFactory(IDiTestService testService)
        {
            this.testService = testService;
        }

        public BaseAction CreateAction(Type actionType, TreeWalkerParameters parameters)
        {
            this.CreateActionCallCount++;

            if (actionType == typeof(SubroutineAction))
            {
                return new SubroutineAction(parameters);
            }

            if (actionType == typeof(SingleDependencyAction))
            {
                return new SingleDependencyAction(this.testService);
            }

            if (actionType == typeof(MultipleDependencyAction))
            {
                return new MultipleDependencyAction(this.testService, new DiTestCounter());
            }

            if (actionType == typeof(DiActionWithInputType))
            {
                return new DiActionWithInputType(this.testService);
            }

            // Fall back to parameterless constructor for other test actions.
            return (BaseAction)Activator.CreateInstance(actionType);
        }
    }
}
