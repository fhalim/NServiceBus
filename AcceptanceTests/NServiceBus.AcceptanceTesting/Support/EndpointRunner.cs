﻿namespace NServiceBus.AcceptanceTesting.Support
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.Remoting.Lifetime;
    using System.Threading;
    using System.Threading.Tasks;
    using Installation.Environments;

    [Serializable]
    public class EndpointRunner : MarshalByRefObject
    {
        IStartableBus bus;
        Configure config;

        EndpointConfiguration configuration;
        ScenarioContext scenarioContext;
        EndpointBehaviour behaviour;
        Semaphore contextChanged = new Semaphore(0, int.MaxValue);
        bool stopped = false;

        public Result Initialize(RunDescriptor run, EndpointBehaviour endpointBehaviour, IDictionary<Type, string> routingTable, string endpointName)
        {
            try
            {
                behaviour = endpointBehaviour;
                scenarioContext = run.ScenarioContext;
                configuration = ((IEndpointConfigurationFactory)Activator.CreateInstance(endpointBehaviour.EndpointBuilderType)).Get();
                configuration.EndpointName = endpointName;

                if (!string.IsNullOrEmpty(configuration.CustomMachineName))
                {
                    NServiceBus.Support.RuntimeEnvironment.MachineNameAction = () => configuration.CustomMachineName;
                }
                
                config = configuration.GetConfiguration(run, routingTable);

                //apply custom config settings
                endpointBehaviour.CustomConfig.ForEach(customAction => customAction(config));


                if (scenarioContext != null)
                {
                    config.Configurer.RegisterSingleton(scenarioContext.GetType(), scenarioContext);
                    scenarioContext.ContextPropertyChanged += scenarioContext_ContextPropertyChanged;
                }


                bus = config.CreateBus();

                Configure.Instance.ForInstallationOn<Windows>().Install();

                Task.Factory.StartNew(() =>
                    {
                        while (!stopped)
                        {
                            contextChanged.WaitOne();

                            foreach (var when in behaviour.Whens)
                            {
                                var action = when.GetAction(scenarioContext);
                                action(bus);
                            }
                            
                        }
                    });

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ex);
            }
        }

        void scenarioContext_ContextPropertyChanged(object sender, EventArgs e)
        {
            contextChanged.Release();
        }


        public Result Start()
        {
            try
            {
                foreach (var given in behaviour.Givens)
                {
                    var action = given.GetAction(scenarioContext);

                    action(bus);
                }

                bus.Start();


                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ex);
            }
        }

        public Result Stop()
        {
            try
            {
                stopped = true;
                
                scenarioContext.ContextPropertyChanged -= scenarioContext_ContextPropertyChanged;
                
                bus.Dispose();

                

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ex);
            }
        }

        public override object InitializeLifetimeService()
        {
            ILease lease = (ILease)base.InitializeLifetimeService();
            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.FromMinutes(2);
                lease.SponsorshipTimeout = TimeSpan.FromMinutes(2);
                lease.RenewOnCallTime = TimeSpan.FromSeconds(2);
            }
            return lease;
        }

        public string Name()
        {
            return AppDomain.CurrentDomain.FriendlyName;
        }


        [Serializable]
        public class Result : MarshalByRefObject
        {
            public string ExceptionMessage { get; set; }

            public bool Failed
            {
                get { return ExceptionMessage != null; }

            }

            public static Result Success()
            {
                return new Result();
            }

            public static Result Failure(Exception ex)
            {
                return new Result
                    {
                        ExceptionMessage = ex.ToString(),
                        ExceptionType = ex.GetType()
                    };
            }

            public Type ExceptionType { get; set; }
        }
    }


}