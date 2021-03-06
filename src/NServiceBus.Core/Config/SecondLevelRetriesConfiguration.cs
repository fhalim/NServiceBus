namespace NServiceBus.Config
{
    using System;
    using Faults.Forwarder;
    using Management.Retries;

    public class SecondLevelRetriesConfiguration : IWantToRunBeforeConfigurationIsFinalized
    {
        public static bool IsDisabled;
        private static Address retriesQueueAddress;

        public void Run()
        {
            // disabled by configure api
            if (IsDisabled)
            {
                return;
            }
            // if we're not using the Fault Forwarder, we should act as if SLR is disabled
            if (!Configure.Instance.Configurer.HasComponent<FaultManager>())
            {
                DisableSecondLevelRetries();
                return;
            }

            var retriesConfig = Configure.GetConfigSection<SecondLevelRetriesConfig>();
            var enabled = retriesConfig == null || retriesConfig.Enabled;

            // if SLR is disabled from app.config, we should disable SLR, but install the queue
            if (!enabled)
            {
                DisableSecondLevelRetries();
                return;
            }

            SetUpRetryPolicy(retriesConfig);
                            
            // and only when the retries satellite is running should we alter the FaultManager                              
            Configure.Instance.Configurer.ConfigureProperty<FaultManager>(fm => fm.RetriesErrorQueue, RetriesQueueAddress);

            Configure.Instance.Configurer.ConfigureProperty<SecondLevelRetries>(rs => rs.InputAddress, RetriesQueueAddress);
        }

        static void DisableSecondLevelRetries()
        {
            Configure.Instance.Configurer.ConfigureProperty<SecondLevelRetries>(rs => rs.Disabled, true);
        }

        static void SetUpRetryPolicy(SecondLevelRetriesConfig retriesConfig)
        {
            if (retriesConfig == null)
                return;

            if (retriesConfig.NumberOfRetries != default(int))
            {
                DefaultRetryPolicy.NumberOfRetries = retriesConfig.NumberOfRetries;
            }
            
            if (retriesConfig.TimeIncrease != TimeSpan.MinValue)
            {
                DefaultRetryPolicy.TimeIncrease = retriesConfig.TimeIncrease;
            }
        }
        
        static Address RetriesQueueAddress 
        {
            get 
            {
                return retriesQueueAddress ?? (retriesQueueAddress = Address.Parse(Configure.EndpointName).SubScope("Retries"));
            }
        }
    }
}