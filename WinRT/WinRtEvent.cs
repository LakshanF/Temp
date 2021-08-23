//
// First, thanks go to MarkMil for figuring out how to interact with a WinRT event from a
// Desktop app.
//
// This program shows that registering WinRT event handlers across multiple appdomains
// can easily lead to the CLR "leaking" event handler CCWs and will even generate crashes
// if any of the event handlers are attached by threads running non-default COM context
// (e.g., STA threads).
//
//
// Usage instructions:
//
//    - Run WinRtEvent.exe, which will print some diagnostic messages to the console and will
//      then enter an infinite sleep.
//
//    - Once WinRtEvent.exe has started sleeping, leave it running and then take parallel
//      steps to generate at least three NetworkStatusChanged notifications (e.g., using the
//      process described below).  If bug #380021 is present, then the program should crash
//      (with an AV) during processing of the third NetworkStatusChanged notification.
//
//
// How to generate NetworkStatusChanged events:
//
//      NetworkStatusChanged events can be generated via the Control Panel.  The following steps
//      seem to work on Win10:
//        - Open Windows Explorer.
//        - Navigate to: Control Panel\Network and Internet\Network Connections
//        - Find the adapter that is being used to deliver general internet access on the machine.
//        - Right-click the adapter and select "Disable" or "Enable" to toggle the enable state.
//
//      Each toggle will generate at least one NetworkStatusChanged event.  When done toggling,
//      it is of course important to make sure that the adapter is left in the enabled state.
//
//      Note that these steps will kill any RDP sessions to the target machine (since they
//      briefly turn off the network adapter) and therefore only work on local machines or VMs
//      accessible via a non-RDP mechanism (e.g., local Hyper-V Virtual Machine Connection
//      windows).
//
//
// Details on the failure sequence this program tries to generate:
//
//      If NetworkStatusChanged events are generated after the domains are unloaded (e.g., by
//      disabling/enabling the network adapter on the machine), the events result in attempts to
//      invoke the "neutered" CCWs associated with the handlers that once existed in the
//      unloaded domains.
//
//      When the Main method IS tagged with [STAThread]:
//
//          Each handler is registered in the GIT in the context of an STA and therefore must be
//          "unmarshaled" over to the non-STA native threadpool thread used to deliver the events.
//
//          When targeting a neutered CCW, each unmarshaling operation results in a call to
//          clr!ComCallUnmarshal::UnmarshalInterface, which then returns COR_E_APPDOMAINUNLOADED
//          but accidentally "leaks" the CCW interface pointer into the "*ppv" slot.
//
//          Due to the way the caller code is written in Windows, this leak reliably causes caller
//          to call Release on the CCW interface pointer.  Since there was no matching add, this is
//          an illegal over-deref which causes each invoke attempt to drop the CCW refcount by one.
//
//          In practice, the CCW refcount ends up set to 2 when it is first installed in the GIT.
//          As a result, the third network status change event tries to interact with a freed CCW
//          (since the refcount dropped to zero during the second event) and reliably AVs.
//
//      When the Main method is NOT tagged with [STAThread]:
//
//          No "unmarshaling" is needed to get the neutered CCWs over to the non-STA native
//          threadpool thread used to deliver the events, so the bug described above does not occur.
//
//          When targeting a neutered CCW, each invoke attempt still fundamentally fails with
//          COR_E_APPDOMAINUNLOADED (specifically, the GIT code fails to QI the CCW IUnknown for the
//          specific NetworkStatusChangedEventHandler delegate interface being invoked).
//
//          These failures are handled gracefully, but generate RoTransformError events which can be
//          observed in the debugger (they are displayed in the debugger window by default and can
//          be "caught" by running "sxe rtt" to tell the debugger to break when they occur).
//
//
// How to compile this program:
//
//      Compile this file with a command line like:
//
//          call %CSC% /nologo /debug+ /optimize+ /platform:x64 /target:exe ^
//              /r:c:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.InteropServices.WindowsRuntime.dll ^
//              /r:c:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.dll ^
//              /r:c:\Windows\system32\WinMetadata\Windows.Foundation.winmd ^
//              /r:c:\Windows\system32\WinMetadata\Windows.Networking.winmd ^
//              WinRtEvent.cs
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting;
using System.Threading;
using Windows.Networking.Connectivity;


namespace Test
{
    public static class Util
    {
        private static readonly object consoleLock = new object();
        private static volatile TextWriter GlobalOutputStream = null;
        private static volatile CrossDomainLogger LogSinkForRemoteDomain = null;


        public static void StartLoggingInEntrypointDomain()
        {
            Util.FailIfAnyLoggerIsActive();
            Util.GlobalOutputStream = Console.Out;
            return;
        }


        public static void StartLogging(CrossDomainLogger logger)
        {
            Util.FailIfAnyLoggerIsActive();
            Util.LogSinkForRemoteDomain = logger;
            return;
        }


        public static Exception Fail(string format, params object[] args)
        {
            string textBlock;

            textBlock = String.Format(format, args);

            if (Util.LoggingIsActive())
            {
                Util.Log("FAIL! {0}", textBlock);
                Environment.Exit(101);
                throw new Exception("Not reached.");
            }
            else
            {
                throw new Exception(
                    "Failed before logging was enabled: " + textBlock
                );
            }
        }


        public static void Log(string format, params object[] args)
        {
            string textBlock;

            textBlock = String.Format(format, args);

            if (Util.GlobalOutputStream != null)
            {
                lock (Util.consoleLock)
                {
                    Util.GlobalOutputStream.Write("{0}", textBlock);
                    Util.GlobalOutputStream.Flush();
                }
            }
            else if (Util.LogSinkForRemoteDomain != null)
            {
                Util.LogSinkForRemoteDomain.LogOneTextBlock(
                    textBlock
                );
            }
            else
            {
                throw new Exception("Tried to log when no writer has been installed.");
            }

            return;
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TryToForceFullGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return;
        }


        private static bool LoggingIsActive()
        {
            return (
                (Util.GlobalOutputStream != null) ||
                (Util.LogSinkForRemoteDomain != null)
            );
        }


        private static void FailIfAnyLoggerIsActive()
        {
            if (Util.LoggingIsActive())
            {
                throw new Exception("Tried to initialize logging multiple times.");
            }

            return;
        }
    }


    public sealed class CrossDomainLogger : MarshalByRefObject
    {
        public void LogOneTextBlock(string text)
        {
            Util.Log("{0}", text);
            return;
        }
    }


    public sealed class RemoteDomainController : MarshalByRefObject
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SynchronouslyAttachHandlerInRemoteDomain(CrossDomainLogger logger)
        {
            Util.StartLogging(logger);
            Util.Log("TRACE: Attaching handler in domain `{0}'.\r\n", AppDomain.CurrentDomain.FriendlyName);
            NetworkInformation.NetworkStatusChanged += RemoteDomainController.RemoteDomainStatusChangeHandler;
            return;
        }


        private static void RemoteDomainStatusChangeHandler(object sender)
        {
            Util.Log("TRACE: Processing NetworkStatusChanged event in remote domain `{0}'...\r\n", AppDomain.CurrentDomain.FriendlyName);
            return;
        }
    }


    static class App
    {
        private static List<AppDomain> LoadedDomainList = new List<AppDomain>();


        [STAThread]
        static void Main(string[] args)
        {
            const int RemoteDomainCount = 4;

            int index;

            Util.StartLoggingInEntrypointDomain();
            Util.Log("Running add/remove/add sequence in entrypoint domain...\r\n");
            NetworkInformation.NetworkStatusChanged += App.EntrypointDomainStatusChangeHandler;
            NetworkInformation.NetworkStatusChanged -= App.EntrypointDomainStatusChangeHandler;
            NetworkInformation.NetworkStatusChanged += App.EntrypointDomainStatusChangeHandler;
            Util.Log("Adding handlers in remote domains...\r\n");

            for (index = 0; index < RemoteDomainCount; index++)
            {
                App.RunOneRemoteDomainScenario(index);
            }

            Util.Log("Unloading all remote domains...\r\n");
            App.UnloadAllRemoteDomains();
            Console.Write("During sleep, attach a debugger and generate NetworkStatusChanged events to observe their effect on the neutered CCWs.\r\n");

            Console.Write(
                "    NetworkStatusChanged events can be triggered, e.g., by using the control panel to toggle the network\r\n" +
                "    adapter on and off.\r\n"
            );

            Console.Write("In the debugger, 'sxe rtt' can be used to break just after a neutered event handler invocation fails.\r\n");
            Util.Log("Remote domains have been unloaded, blocking forever...\r\n");
            Thread.Sleep(Timeout.Infinite);
            return;
        }


        private static void EntrypointDomainStatusChangeHandler(object sender)
        {
            Util.Log("TRACE: Processing NetworkStatusChanged event in the entrypoint domain...\r\n");
            return;
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        private static AppDomain RunOneRemoteDomainScenario(int index)
        {
            RemoteDomainController controller;
            ObjectHandle handleToController;
            AppDomain remoteDomain;

            remoteDomain = AppDomain.CreateDomain(String.Format("RemoteDomain_{0}", index));

            handleToController = remoteDomain.CreateInstance(
                assemblyName: Assembly.GetExecutingAssembly().GetName().Name,
                typeName: "Test.RemoteDomainController"
            );

            controller = (RemoteDomainController)handleToController.Unwrap();

            controller.SynchronouslyAttachHandlerInRemoteDomain(
                logger: new CrossDomainLogger()
            );

            App.LoadedDomainList.Add(remoteDomain);
            return remoteDomain;
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UnloadAllRemoteDomains()
        {
            //
            // Unload all loaded domains.
            //

            App.InternalUnloadAllRemoteDomainsWorker();

            //
            // Try to force a full GC as a way to clean up everything related to the unloaded domains
            // (e.g., the associated System.AppDomain objects, transparent proxies, etc which reside in
            // the current domain and may not have been collected).
            //
            // N.B. The function called above is marked no-inline, so when control reaches this point
            //      it is guaranteed that there are no longer any live stack frames that could potentially
            //      contain references to the objects that this operation is trying to collect.
            //

            Util.TryToForceFullGc();
            return;
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InternalUnloadAllRemoteDomainsWorker()
        {
            string domainName;

            foreach (AppDomain domain in App.LoadedDomainList)
            {
                domainName = domain.FriendlyName;

                try
                {
                    AppDomain.Unload(domain);
                }
                catch (Exception e)
                {
                    throw Util.Fail(
                        "Error: Failed to unload `{0}' ({1})\r\n",
                        domainName,
                        e.GetType().ToString()
                    );
                }
            }

            App.LoadedDomainList.Clear();
            return;
        }
    }
}

