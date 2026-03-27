//-----------------------------------------------------------------------
// <copyright file="DependencyInjectionTests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
//     Tests the dependency injection capabilities of Forge's action factory system.
// </summary>
//-----------------------------------------------------------------------

namespace Microsoft.Forge.TreeWalker.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Microsoft.Forge.DataContracts;
    using Microsoft.Forge.TreeWalker;
    using Microsoft.Forge.TreeWalker.ForgeExceptions;
    using Newtonsoft.Json;

    [TestClass]
    public class DependencyInjectionTests
    {
        #region Test Schemas

        private const string SingleDependencySchema = @"
            {
                ""Tree"": {
                    ""Root"": {
                        ""Type"": ""Action"",
                        ""Actions"": {
                            ""Root_SingleDependencyAction"": {
                                ""Action"": ""SingleDependencyAction""
                            }
                        }
                    }
                }
            }";

        private const string MultipleDependencySchema = @"
            {
                ""Tree"": {
                    ""Root"": {
                        ""Type"": ""Action"",
                        ""Actions"": {
                            ""Root_MultipleDependencyAction"": {
                                ""Action"": ""MultipleDependencyAction""
                            }
                        }
                    }
                }
            }";

        private const string ParallelDependencyActionsSchema = @"
            {
                ""Tree"": {
                    ""Root"": {
                        ""Type"": ""Action"",
                        ""Actions"": {
                            ""Root_MultipleDependencyAction_1"": {
                                ""Action"": ""MultipleDependencyAction""
                            },
                            ""Root_MultipleDependencyAction_2"": {
                                ""Action"": ""MultipleDependencyAction""
                            }
                        }
                    }
                }
            }";

        private const string DiActionWithInputTypeSchema = @"
            {
                ""Tree"": {
                    ""Root"": {
                        ""Type"": ""Action"",
                        ""Actions"": {
                            ""Root_DiActionWithInputType"": {
                                ""Action"": ""DiActionWithInputType"",
                                ""Input"": {
                                    ""MessageProperty"": ""Hello""
                                }
                            }
                        }
                    }
                }
            }";

        private const string DiActionWithChildSelectorSchema = @"
            {
                ""Tree"": {
                    ""Root"": {
                        ""Type"": ""Action"",
                        ""Actions"": {
                            ""Root_SingleDependencyAction"": {
                                ""Action"": ""SingleDependencyAction""
                            }
                        },
                        ""ChildSelector"": [
                            {
                                ""Label"": ""Success"",
                                ""ShouldSelect"": ""C#|Session.GetLastActionResponse().Status == \""Success\"""",
                                ""Child"": ""SuccessLeaf""
                            }
                        ]
                    },
                    ""SuccessLeaf"": {
                        ""Type"": ""Leaf""
                    }
                }
            }";

        private const string MultiNodeDiSchema = @"
            {
                ""Tree"": {
                    ""Root"": {
                        ""Type"": ""Action"",
                        ""Actions"": {
                            ""Root_SingleDependencyAction"": {
                                ""Action"": ""SingleDependencyAction""
                            }
                        },
                        ""ChildSelector"": [
                            {
                                ""Label"": ""Success"",
                                ""ShouldSelect"": ""C#|Session.GetLastActionResponse().Status == \""Success\"""",
                                ""Child"": ""SecondNode""
                            }
                        ]
                    },
                    ""SecondNode"": {
                        ""Type"": ""Action"",
                        ""Actions"": {
                            ""SecondNode_MultipleDependencyAction"": {
                                ""Action"": ""MultipleDependencyAction""
                            }
                        }
                    }
                }
            }";

        #endregion Test Schemas

        #region Helper Methods

        private static ServiceProvider BuildServiceProvider(string serviceValue = "Injected")
        {
            var services = new ServiceCollection();
            services.AddSingleton<IDiTestService>(new DiTestService(serviceValue));
            services.AddSingleton<IDiTestCounter, DiTestCounter>();
            return services.BuildServiceProvider();
        }

        private static TreeWalkerSession CreateSession(
            string jsonSchema,
            IServiceProvider serviceProvider = null,
            IForgeActionFactory actionFactory = null)
        {
            Guid sessionId = Guid.NewGuid();
            var forgeState = new ForgeDictionary(new Dictionary<string, object>(), sessionId, sessionId);
            var callbacks = new TreeWalkerCallbacksV2();
            var token = new CancellationTokenSource().Token;
            ForgeTree forgeTree = JsonConvert.DeserializeObject<ForgeTree>(jsonSchema);

            var parameters = new TreeWalkerParameters(
                sessionId,
                forgeTree,
                forgeState,
                callbacks,
                token)
            {
                UserContext = new ForgeUserContext(),
                ForgeActionsAssembly = typeof(SingleDependencyAction).Assembly,
                ServiceProvider = serviceProvider,
                ActionFactory = actionFactory
            };

            return new TreeWalkerSession(parameters);
        }

        #endregion Helper Methods

        #region DefaultForgeActionFactory Tests

        [TestMethod]
        public async Task TestDefaultFactory_IsUsedWhenNoServiceProviderOrFactorySet()
        {
            // Test - When neither ServiceProvider nor ActionFactory is set, DefaultForgeActionFactory is used.
            //        Actions requiring DI dependencies will fail because Activator.CreateInstance cannot resolve them.
            var session = CreateSession(SingleDependencySchema);

            await Assert.ThrowsExceptionAsync<ActionTimeoutException>(async () =>
            {
                await session.WalkTree("Root");
            }, "Expected WalkTree to fail because SingleDependencyAction requires constructor injection which DefaultForgeActionFactory cannot provide.");

            Assert.AreEqual("TimeoutOnAction", session.Status,
                "Expected TimeoutOnAction because DefaultForgeActionFactory uses Activator.CreateInstance which cannot resolve IDiTestService.");
        }

        [TestMethod]
        public void TestDefaultFactory_CreateAction_ParameterlessConstructor()
        {
            // Test - DefaultForgeActionFactory can create actions with parameterless constructors.
            var factory = new DefaultForgeActionFactory();
            var action = factory.CreateAction(typeof(TardigradeAction), null);

            Assert.IsNotNull(action, "Expected DefaultForgeActionFactory to create an action with a parameterless constructor.");
            Assert.IsInstanceOfType(action, typeof(TardigradeAction));
        }

        [TestMethod]
        public void TestDefaultFactory_CreateAction_SubroutineAction()
        {
            // Test - DefaultForgeActionFactory can create SubroutineAction with TreeWalkerParameters.
            Guid sessionId = Guid.NewGuid();
            var forgeState = new ForgeDictionary(new Dictionary<string, object>(), sessionId, sessionId);
            var callbacks = new TreeWalkerCallbacksV2();
            ForgeTree forgeTree = JsonConvert.DeserializeObject<ForgeTree>(SingleDependencySchema);
            var parameters = new TreeWalkerParameters(sessionId, forgeTree, forgeState, callbacks, CancellationToken.None);

            var factory = new DefaultForgeActionFactory();
            var action = factory.CreateAction(typeof(SubroutineAction), parameters);

            Assert.IsNotNull(action, "Expected DefaultForgeActionFactory to create SubroutineAction.");
            Assert.IsInstanceOfType(action, typeof(SubroutineAction));
        }

        #endregion DefaultForgeActionFactory Tests

        #region ServiceProviderActionFactory Tests

        [TestMethod]
        public void TestServiceProviderFactory_Constructor_NullServiceProvider_ThrowsArgumentNullException()
        {
            // Test - ServiceProviderActionFactory constructor throws on null serviceProvider.
            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                new ServiceProviderActionFactory(null);
            }, "Expected ArgumentNullException when creating ServiceProviderActionFactory with null serviceProvider.");
        }

        [TestMethod]
        public async Task TestServiceProviderFactory_SingleDependency_WalkTree_Success()
        {
            // Test - WalkTree with ServiceProvider set, action resolved via ActivatorUtilities with single dependency.
            using (var sp = BuildServiceProvider("InjectedValue"))
            {
                var session = CreateSession(SingleDependencySchema, serviceProvider: sp);
                string status = await session.WalkTree("Root");

                Assert.AreEqual("RanToCompletion", status,
                    "Expected WalkTree to complete successfully with ServiceProvider resolving SingleDependencyAction.");

                ActionResponse response = await session.GetLastActionResponseAsync();
                Assert.AreEqual("Success", response.Status);
                Assert.AreEqual("InjectedValue", response.Output,
                    "Expected the injected IDiTestService value to be returned as Output.");
            }
        }

        [TestMethod]
        public async Task TestServiceProviderFactory_MultipleDependencies_WalkTree_Success()
        {
            // Test - WalkTree with ServiceProvider set, action resolved via ActivatorUtilities with multiple dependencies.
            using (var sp = BuildServiceProvider("MultiDep"))
            {
                var session = CreateSession(MultipleDependencySchema, serviceProvider: sp);
                string status = await session.WalkTree("Root");

                Assert.AreEqual("RanToCompletion", status,
                    "Expected WalkTree to complete successfully with ServiceProvider resolving MultipleDependencyAction.");

                ActionResponse response = await session.GetLastActionResponseAsync();
                Assert.AreEqual("MultiDep", response.Status,
                    "Expected the injected IDiTestService value to be returned as Status.");
                Assert.AreEqual(1, response.StatusCode,
                    "Expected the IDiTestCounter to have been incremented to 1.");
                Assert.AreEqual("MultiDep_1", response.Output,
                    "Expected the combined output from both injected services.");
            }
        }

        [TestMethod]
        public async Task TestServiceProviderFactory_ParallelActions_WalkTree_Success()
        {
            // Test - WalkTree with two parallel actions on the same node, both resolved via ServiceProvider.
            using (var sp = BuildServiceProvider("Parallel"))
            {
                var session = CreateSession(ParallelDependencyActionsSchema, serviceProvider: sp);
                string status = await session.WalkTree("Root");

                Assert.AreEqual("RanToCompletion", status,
                    "Expected WalkTree to complete successfully with parallel DI-resolved actions.");

                ActionResponse response1 = await session.GetOutputAsync("Root_MultipleDependencyAction_1");
                ActionResponse response2 = await session.GetOutputAsync("Root_MultipleDependencyAction_2");

                Assert.IsNotNull(response1, "Expected ActionResponse for first parallel action.");
                Assert.IsNotNull(response2, "Expected ActionResponse for second parallel action.");
                Assert.AreEqual("Parallel", response1.Status);
                Assert.AreEqual("Parallel", response2.Status);
            }
        }

        [TestMethod]
        public async Task TestServiceProviderFactory_ActionWithInputType_WalkTree_Success()
        {
            // Test - WalkTree with a DI-resolved action that also uses a typed ActionInput.
            using (var sp = BuildServiceProvider("WithInput"))
            {
                var session = CreateSession(DiActionWithInputTypeSchema, serviceProvider: sp);
                string status = await session.WalkTree("Root");

                Assert.AreEqual("RanToCompletion", status,
                    "Expected WalkTree to complete successfully with DI action using typed ActionInput.");

                ActionResponse response = await session.GetLastActionResponseAsync();
                Assert.AreEqual("Success", response.Status);
                Assert.AreEqual("WithInput_Hello", response.Output,
                    "Expected output to combine injected service value with deserialized ActionInput.");
            }
        }

        [TestMethod]
        public async Task TestServiceProviderFactory_MultiNodeWalk_WalkTree_Success()
        {
            // Test - WalkTree across multiple nodes, each with DI-resolved actions.
            using (var sp = BuildServiceProvider("MultiNode"))
            {
                var session = CreateSession(MultiNodeDiSchema, serviceProvider: sp);
                string status = await session.WalkTree("Root");

                Assert.AreEqual("RanToCompletion", status,
                    "Expected WalkTree to complete successfully across multiple nodes with DI-resolved actions.");

                ActionResponse rootResponse = await session.GetOutputAsync("Root_SingleDependencyAction");
                Assert.IsNotNull(rootResponse, "Expected ActionResponse from Root node.");
                Assert.AreEqual("MultiNode", rootResponse.Output);

                ActionResponse secondResponse = await session.GetOutputAsync("SecondNode_MultipleDependencyAction");
                Assert.IsNotNull(secondResponse, "Expected ActionResponse from SecondNode.");
                Assert.AreEqual("MultiNode", secondResponse.Status);
            }
        }

        [TestMethod]
        public async Task TestServiceProviderFactory_ChildSelectorEvaluatesAfterDiAction_WalkTree_Success()
        {
            // Test - WalkTree where a DI-resolved action's response drives ChildSelector evaluation.
            using (var sp = BuildServiceProvider("Selector"))
            {
                var session = CreateSession(DiActionWithChildSelectorSchema, serviceProvider: sp);
                string status = await session.WalkTree("Root");

                Assert.AreEqual("RanToCompletion", status,
                    "Expected WalkTree to complete and select the SuccessLeaf child based on DI action response.");

                string currentNode = await session.GetCurrentTreeNode();
                Assert.AreEqual("SuccessLeaf", currentNode,
                    "Expected tree walker to visit SuccessLeaf after DI action returned Status=Success.");
            }
        }

        [TestMethod]
        public void TestServiceProviderFactory_CreateAction_DirectlyRegisteredAction()
        {
            // Test - When an action type is registered directly in the DI container, ServiceProviderActionFactory resolves it.
            var services = new ServiceCollection();
            var expectedService = new DiTestService("Direct");
            services.AddSingleton<IDiTestService>(expectedService);
            services.AddTransient<SingleDependencyAction>();

            using (var sp = services.BuildServiceProvider())
            {
                var factory = new ServiceProviderActionFactory(sp);
                var action = factory.CreateAction(typeof(SingleDependencyAction), null);

                Assert.IsNotNull(action, "Expected factory to resolve directly registered action.");
                Assert.IsInstanceOfType(action, typeof(SingleDependencyAction));
            }
        }

        [TestMethod]
        public void TestServiceProviderFactory_CreateAction_FallsBackToActivatorUtilities()
        {
            // Test - When an action type is NOT registered but its dependencies are, ActivatorUtilities resolves it.
            var services = new ServiceCollection();
            services.AddSingleton<IDiTestService>(new DiTestService("Fallback"));
            services.AddSingleton<IDiTestCounter, DiTestCounter>();
            // MultipleDependencyAction is NOT registered - only its dependencies are.

            using (var sp = services.BuildServiceProvider())
            {
                var factory = new ServiceProviderActionFactory(sp);
                var action = factory.CreateAction(typeof(MultipleDependencyAction), null);

                Assert.IsNotNull(action, "Expected factory to fall back to ActivatorUtilities and resolve the action.");
                Assert.IsInstanceOfType(action, typeof(MultipleDependencyAction));
            }
        }

        [TestMethod]
        public void TestServiceProviderFactory_CreateAction_SubroutineAction()
        {
            // Test - ServiceProviderActionFactory creates SubroutineAction with TreeWalkerParameters.
            Guid sessionId = Guid.NewGuid();
            var forgeState = new ForgeDictionary(new Dictionary<string, object>(), sessionId, sessionId);
            var callbacks = new TreeWalkerCallbacksV2();
            ForgeTree forgeTree = JsonConvert.DeserializeObject<ForgeTree>(SingleDependencySchema);
            var parameters = new TreeWalkerParameters(sessionId, forgeTree, forgeState, callbacks, CancellationToken.None);

            using (var sp = BuildServiceProvider())
            {
                var factory = new ServiceProviderActionFactory(sp);
                var action = factory.CreateAction(typeof(SubroutineAction), parameters);

                Assert.IsNotNull(action, "Expected factory to create SubroutineAction via ActivatorUtilities.");
                Assert.IsInstanceOfType(action, typeof(SubroutineAction));
            }
        }

        #endregion ServiceProviderActionFactory Tests

        #region Custom ActionFactory Tests

        [TestMethod]
        public async Task TestCustomFactory_WalkTree_Success()
        {
            // Test - WalkTree with a custom IForgeActionFactory that manually wires dependencies.
            var testService = new DiTestService("Custom");
            var customFactory = new TestCustomActionFactory(testService);

            var session = CreateSession(SingleDependencySchema, actionFactory: customFactory);
            string status = await session.WalkTree("Root");

            Assert.AreEqual("RanToCompletion", status,
                "Expected WalkTree to complete successfully with custom ActionFactory.");

            ActionResponse response = await session.GetLastActionResponseAsync();
            Assert.AreEqual("Success", response.Status);
            Assert.AreEqual("Custom", response.Output,
                "Expected the custom factory to provide the injected IDiTestService value.");
        }

        [TestMethod]
        public async Task TestCustomFactory_CreateActionCalled_WalkTree_Success()
        {
            // Test - Verify that the custom factory's CreateAction was actually invoked during WalkTree.
            var testService = new DiTestService("Tracked");
            var customFactory = new TestCustomActionFactory(testService);

            var session = CreateSession(SingleDependencySchema, actionFactory: customFactory);
            string status = await session.WalkTree("Root");

            Assert.AreEqual("RanToCompletion", status);
            Assert.IsTrue(customFactory.CreateActionCallCount > 0,
                "Expected custom ActionFactory.CreateAction to have been called at least once during WalkTree.");
        }

        [TestMethod]
        public async Task TestCustomFactory_TakesPrecedenceOverServiceProvider()
        {
            // Test - When both ActionFactory and ServiceProvider are set, ActionFactory takes precedence.
            var testService = new DiTestService("FactoryWins");
            var customFactory = new TestCustomActionFactory(testService);

            using (var sp = BuildServiceProvider("ServiceProviderValue"))
            {
                var session = CreateSession(SingleDependencySchema, serviceProvider: sp, actionFactory: customFactory);
                string status = await session.WalkTree("Root");

                Assert.AreEqual("RanToCompletion", status);

                ActionResponse response = await session.GetLastActionResponseAsync();
                Assert.AreEqual("FactoryWins", response.Output,
                    "Expected ActionFactory to take precedence over ServiceProvider. Output should come from the custom factory's service, not the ServiceProvider's.");
            }
        }

        [TestMethod]
        public async Task TestCustomFactory_MultipleDependencies_WalkTree_Success()
        {
            // Test - Custom factory resolves an action with multiple dependencies.
            var testService = new DiTestService("CustomMulti");
            var customFactory = new TestCustomActionFactory(testService);

            var session = CreateSession(MultipleDependencySchema, actionFactory: customFactory);
            string status = await session.WalkTree("Root");

            Assert.AreEqual("RanToCompletion", status,
                "Expected WalkTree to complete successfully with custom factory resolving MultipleDependencyAction.");

            ActionResponse response = await session.GetLastActionResponseAsync();
            Assert.AreEqual("CustomMulti", response.Status);
            Assert.AreEqual(1, response.StatusCode);
        }

        #endregion Custom ActionFactory Tests

        #region Factory Priority / Fallback Tests

        [TestMethod]
        public async Task TestFactoryPriority_ServiceProviderOnly_CreatesServiceProviderActionFactory()
        {
            // Test - When only ServiceProvider is set, Forge internally creates a ServiceProviderActionFactory.
            using (var sp = BuildServiceProvider("SPOnly"))
            {
                var session = CreateSession(SingleDependencySchema, serviceProvider: sp);
                string status = await session.WalkTree("Root");

                Assert.AreEqual("RanToCompletion", status,
                    "Expected Forge to auto-create ServiceProviderActionFactory from ServiceProvider and resolve actions.");

                ActionResponse response = await session.GetLastActionResponseAsync();
                Assert.AreEqual("SPOnly", response.Output);
            }
        }

        [TestMethod]
        public async Task TestFactoryPriority_NeitherSet_UsesDefaultFactory()
        {
            // Test - When neither ActionFactory nor ServiceProvider is set, the DefaultForgeActionFactory is used.
            //        TardigradeAction has a parameterless constructor so it should work.
            string tardigradeOnlySchema = @"
                {
                    ""Tree"": {
                        ""Root"": {
                            ""Type"": ""Action"",
                            ""Actions"": {
                                ""Root_TardigradeAction"": {
                                    ""Action"": ""TardigradeAction""
                                }
                            }
                        }
                    }
                }";

            var session = CreateSession(tardigradeOnlySchema);
            string status = await session.WalkTree("Root");

            Assert.AreEqual("RanToCompletion", status,
                "Expected DefaultForgeActionFactory to resolve TardigradeAction via Activator.CreateInstance.");
        }

        #endregion Factory Priority / Fallback Tests
    }
}
