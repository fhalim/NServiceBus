﻿namespace NServiceBus.Features
{
    using System;
    using System.Text;
    using Config;
    using Logging;

    public class FeatureInitializer : IFinalizeConfiguration,IWantToRunBeforeConfigurationIsFinalized
    {
        /// <summary>
        /// Go trough all conditional features and figure out if the should be enabled or not
        /// </summary>
        public void Run()
        {
            Configure.Instance.ForAllTypes<IConditionalFeature>(t =>
            {
                if (!Feature.IsEnabled(t))
                {
                    return;
                }

                var feature = (IConditionalFeature)Activator.CreateInstance(t);

                if (!feature.ShouldBeEnabled())
                {
                    Feature.Disable(t);
                    Logger.DebugFormat("{0} - Conditionally disabled", t.Name);
                }
            });


        }

        public void FinalizeConfiguration()
        {
            var statusText = new StringBuilder();

            Configure.Instance.ForAllTypes<IFeature>(t =>
                {
                    if (!Feature.IsEnabled(t))
                    {
                        statusText.AppendLine(string.Format("{0} - Disabled", t.Name));
                        return;
                    }

                    var feature = (IFeature)Activator.CreateInstance(t);
                 
                    feature.Initialize();

                    statusText.AppendLine(string.Format("{0} - Enabled", t.Name));
                });

            Logger.InfoFormat("Features: \n{0}", statusText);
        }
        
        static readonly ILog Logger = LogManager.GetLogger(typeof(FeatureInitializer));
    }
}