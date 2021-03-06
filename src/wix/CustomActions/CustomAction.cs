﻿namespace NServiceBus.Wix.CustomActions
{
    using System;
    using System.IO;
    using System.Text;
    using Microsoft.Deployment.WindowsInstaller;
    using PowerShell;
    using Setup.Windows.Dtc;
    using Setup.Windows.Msmq;
    using Setup.Windows.PerformanceCounters;
    using Setup.Windows.RavenDB;

    public class CustomActions
    {
        [CustomAction]
        public static ActionResult InstallMsmq(Session session)
        {
            session.Log("Installing/Starting MSMQ if necessary.");

            try
            {
                CaptureOut(() =>
                    {
                        if (MsmqSetup.StartMsmqIfNecessary(true))
                        {
                            session.Log("MSMQ installed and configured.");
                        }
                        else
                        {
                            session.Log("MSMQ already properly configured.");
                        }

                        session["MSMQ_INSTALL"] = "SUCCESS";
                    }, session);

                return ActionResult.Success;
            }
            catch (Exception)
            {
                return ActionResult.Failure;
            }
        }

        [CustomAction]
        public static ActionResult InstallDtc(Session session)
        {
            session.Log("Installing/Starting DTC if necessary.");

            try
            {
                CaptureOut(() =>
                    {
                        DtcSetup.StartDtcIfNecessary();
                        session["DTC_INSTALL"] = "SUCCESS";

                        session.Log("DTC installed and configured.");
                    }, session);

                return ActionResult.Success;
            }
            catch (Exception)
            {
                return ActionResult.Failure;
            }
        }

        [CustomAction]
        public static ActionResult InstallRavenDb(Session session)
        {
            session.Log("Installing RavenDB if necessary.");

            try
            {
                int port;

                if (!int.TryParse(session["RAVEN_PORT"], out port))
                {
                    throw new InvalidOperationException("No RavenDB.Port property found please set it");
                }

                string installPath = session["RAVEN_INSTALLPATH"];

                   
                CaptureOut(() =>
                    {
                        RavenDBSetup.Install(port, installPath);

                        session["RAVEN_INSTALL"] = "SUCCESS";

                        session.Log("RavenDB installed and configured.");
                    }, session);


                return ActionResult.Success;
            }
            catch (Exception)
            {
                return ActionResult.Failure;
            }
        }


        [CustomAction]
        public static ActionResult DetectRavenDBPort(Session session)
        {
            session.Log("Checking if RavenDB is installed");

            try
            {
                CaptureOut(() =>
                    {
                        var port = RavenDBSetup.FindRavenDBPort();

                        if (port != 0)
                        {
                            session["RAVEN_ISINSTALLED"] = "true";
                            session["RAVEN_PORT"] = port.ToString();

                        }
                        else
                        {
                            session["RAVEN_ISINSTALLED"] = "false";
                        }
                    }, session);

                return ActionResult.Success;
            }
            catch (Exception)
            {
                return ActionResult.Failure;
            }
        }

        [CustomAction]
        public static ActionResult FindAvailablePort(Session session)
        {
            session.Log("Finding an available port where RavenDB can be installed");

            try
            {
                CaptureOut(() =>
                {
                        session["PORT_AVAILABLE"] = PortUtils.FindAvailablePort(8080).ToString();
                    
                }, session);

                return ActionResult.Success;
            }
            catch (Exception)
            {
                return ActionResult.Failure;
            }
        }


        [CustomAction]
        public static ActionResult IsPortAvaialable(Session session)
        {
            try
            {
                int port;

                if (!int.TryParse(session["PORT_TOCHECK"], out port))
                {
                    throw new InvalidOperationException("No SelectedPort property found please set it");
                }

                session.Log("Checking if port {0} is available", port);

                CaptureOut(() =>
                    {
                        session["PORT_CHECK"] = PortUtils.IsPortAvailable(port).ToString();

                    }, session);

                return ActionResult.Success;
            }
            catch (Exception)
            {
                return ActionResult.Failure;
            }
        }

        [CustomAction]
        public static ActionResult InstallPerformanceCounters(Session session)
        {
            session.Log("Installing NSB performance counters.");

            try
            {
                CaptureOut(() =>
                    {
                        PerformanceCounterSetup.SetupCounters();
                        
                        session["COUNTERS_ISINSTALLED"] = "false";

                        session.Log("NSB performance counters installed.");
                    }, session);

                return ActionResult.Success;
            }
            catch (Exception)
            {
                return ActionResult.Failure;
            }
        }

        private static void CaptureOut(Action execute, Session session)
        {
            var sb = new StringBuilder();
            TextWriter standardOut = Console.Out;
            using (var stringWriter = new StringWriter(sb))
            {
                Console.SetOut(stringWriter);

                try
                {
                    execute();

                    session.Log(sb.ToString());
                }
                finally
                {
                    Console.SetOut(standardOut);
                }
            }
        }
    }
}