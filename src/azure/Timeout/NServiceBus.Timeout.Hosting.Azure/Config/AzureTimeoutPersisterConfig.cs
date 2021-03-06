using System.Configuration;

namespace NServiceBus.Timeout.Hosting.Azure
{
    public class AzureTimeoutPersisterConfig : ConfigurationSection
    {
        [ConfigurationProperty("ConnectionString", IsRequired = false, DefaultValue = "UseDevelopmentStorage=true")]
        public string ConnectionString
        {
            get { return (string)this["ConnectionString"]; }
            set { this["ConnectionString"] = value; }
        }

        [ConfigurationProperty("TimeoutManagerDataTableName", IsRequired = false, DefaultValue = "TimeoutManagerDataTable")]
        public string TimeoutManagerDataTableName
        {
            get { return (string)this["TimeoutManagerDataTableName"]; }
            set { this["TimeoutManagerDataTableName"] = value; }
        }

        [ConfigurationProperty("TimeoutDataTableName", IsRequired = false, DefaultValue = "TimeoutDataTableName")]
        public string TimeoutDataTableName
        {
            get { return (string)this["TimeoutDataTableName"]; }
            set { this["TimeoutDataTableName"] = value; }
        }

        [ConfigurationProperty("CatchUpInterval", IsRequired = false, DefaultValue = 3600)]
        public int CatchUpInterval
        {
            get { return (int)this["CatchUpInterval"]; }
            set { this["CatchUpInterval"] = value; }
        }

        [ConfigurationProperty("PartitionKeyScope", IsRequired = false, DefaultValue = "yyyMMddHH")]
        public string PartitionKeyScope
        {
            get { return (string)this["PartitionKeyScope"]; }
            set { this["PartitionKeyScope"] = value; }
        }

        
        
    }
}