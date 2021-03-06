﻿namespace NServiceBus.PowerShell
{
    using System.Management.Automation;
    using System.Xml.Linq;
    using System.Xml.XPath;

    [Cmdlet(VerbsCommon.Add, "NServiceBusUnicastBusConfig")]
    public class AddUnicastBusConfig : AddConfigSection
    {
        const string Instructions = @"To register all message types defined in an assembly:
      <add Assembly=""assembly"" endpoint=""queue@machinename"" />
      
      To register all message types defined in an assembly with a specific namespace (it does not include sub namespaces):
      <add Assembly=""assembly"" Namespace=""namespace"" Endpoint=""queue@machinename"" />
      
      To register a specific type in an assembly:
      <add Assembly=""assembly"" Type=""type fullname (http://msdn.microsoft.com/en-us/library/system.type.fullname.aspx)"" Endpoint=""queue@machinename"" />";
        
        public override void ModifyConfig(XDocument doc)
        {
            var sectionElement = doc.XPathSelectElement("/configuration/configSections/section[@name='UnicastBusConfig' and @type='NServiceBus.Config.UnicastBusConfig, NServiceBus.Core']");
            if (sectionElement == null)
            {

                doc.XPathSelectElement("/configuration/configSections").Add(new XElement("section",
                                                                                         new XAttribute("name",
                                                                                                        "UnicastBusConfig"),
                                                                                         new XAttribute("type",
                                                                                                        "NServiceBus.Config.UnicastBusConfig, NServiceBus.Core")));

            }

            var forwardingElement = doc.XPathSelectElement("/configuration/UnicastBusConfig");
            if (forwardingElement == null)
            {
                doc.Root.LastNode.AddAfterSelf(new XElement("UnicastBusConfig",
                                                            new XAttribute("ForwardReceivedMessagesTo", "audit"),
                                                            new XElement("MessageEndpointMappings", new XComment(Instructions))));
            }
        }
    }
}