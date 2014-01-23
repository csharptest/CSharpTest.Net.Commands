#region Copyright 2011-2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
/* Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Security.AccessControl;
using System.ServiceProcess;
using System.ComponentModel;
using System.Threading;
using CSharpTest.Net.Utils;

namespace CSharpTest.Net.Services
{
    /// <summary>
    /// Advanced Service Control for installation/uninstallation and security settings
    /// </summary>
    public class SvcControlManager : IDisposable
    {
        private const string UnableToControlService = "The service did not respond.";
        /// <summary>
        /// Represets common NT_AUTHORITY service accounts that do not require a password
        /// at install time.
        /// </summary>
        public static class NT_AUTHORITY
        {
            /// <summary> NT_AUTHORITY\LocalSystem </summary>
            public const string LocalSystem = null;
            /// <summary> NT_AUTHORITY\LocalService </summary>
            public const string LocalService = @"NT AUTHORITY\LocalService";
            /// <summary> NT_AUTHORITY\NetworkService </summary>
            public const string NetworkService = @"NT AUTHORITY\NetworkService";
            /// <summary> Selects the account bysed on the System.ServiceProcess.ServiceAccount enumeration </summary>
            public static string Account(ServiceAccount account)
            {
                switch (account)
                {
                    case ServiceAccount.LocalService:
                        return LocalService;
                    case ServiceAccount.LocalSystem:
                        return LocalSystem;
                    case ServiceAccount.NetworkService:
                        return NetworkService;
                    default:
                        throw new ArgumentOutOfRangeException(
                            "The service account must be one of LocalService, NetworkService, or LocalSystem.",
                            "account");
                }
            }
        }

        private readonly string _svcName;

        /// <summary>
        /// Constructs the SvcControlManager for the service name provided.
        /// </summary>
        public SvcControlManager(string serviceName)
        {
            _svcName = serviceName;
        }

        /// <summary>
        /// Disposes of the SvcControlManager
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Starts the service with the arguments specified and waits for the service
        /// to enter the running state.
        /// </summary>
        public void Start(string[] arguments)
        {
            using (var sc = new ServiceController(_svcName))
            {
                if (sc.Status == ServiceControllerStatus.Running)
                    return;

                sc.Start(arguments ?? new string[0]);
                do
                {
                    Thread.Sleep(1000);
                    sc.Refresh();
                }
                while (sc.Status == ServiceControllerStatus.StartPending);

                if (sc.Status != ServiceControllerStatus.Running)
                    throw new ApplicationException(UnableToControlService);
            }
        }

        /// <summary>
        /// Stops the service and waits for the service to enter the Stopped state.
        /// </summary>
        public void Stop()
        {
            using (var sc = new ServiceController(_svcName))
            {
                if (sc.Status == ServiceControllerStatus.Stopped)
                    return;

                sc.Stop();
                do
                {
                    Thread.Sleep(1000);
                    sc.Refresh();
                }
                while (sc.Status == ServiceControllerStatus.StopPending);

                if (sc.Status != ServiceControllerStatus.Stopped)
                    throw new ApplicationException(UnableToControlService);
            }
        }

        /// <summary>
        /// Configures the service to use the delayed auto-start policy
        /// </summary>
        public void SetDelayAutostart(bool enabled)
        {
            SetServiceConfig(SERVICE_CONFIG_INFO.DELAYED_AUTO_START_INFO, enabled ? 1 : 0);
        }

        /// <summary>
        /// Sets the service's default shutdown timeout period.
        /// </summary>
        public void SetShutdownTimeout(TimeSpan timeoutValue)
        {
            SetServiceConfig(SERVICE_CONFIG_INFO.PRESHUTDOWN_INFO, (int)timeoutValue.TotalMilliseconds);
        }

        /// <summary>
        /// Sets the description text of the service.
        /// </summary>
        public void SetDescription(string description)
        {
            GCHandle hdata = GCHandle.Alloc(description, GCHandleType.Pinned);
            try
            {
                SC_DESCRIPTION desc = new SC_DESCRIPTION();
                desc.Description = hdata.AddrOfPinnedObject();
                SetServiceConfig(SERVICE_CONFIG_INFO.DESCRIPTION, desc);
            }
            finally
            {
                hdata.Free();
            }
        }

        void SetServiceConfig<T>(SERVICE_CONFIG_INFO infoId, T objData)
        {
            GCHandle hdata = GCHandle.Alloc(objData, GCHandleType.Pinned);
            try
            {
                WithServiceHandle(
                    ServiceAccessRights.GENERIC_READ | ServiceAccessRights.GENERIC_WRITE,
                    delegate(IntPtr svcHandle)
                    {
                        if (0 == Win32.ChangeServiceConfig2(svcHandle, (int)infoId, hdata.AddrOfPinnedObject()))
                            throw new Win32Exception();
                    }
                );
            }
            finally
            {
                hdata.Free();
            }
        }

        /// <summary>
        /// Changes the service's executable and arguments
        /// </summary>
        public void SetServiceExeArgs(string exePath, string[] arguments)
        {
            exePath = ArgumentList.EscapeArguments(new string[] {Check.NotEmpty(exePath)});
            if (arguments != null && arguments.Length > 0)
                exePath = String.Format("{0} {1}", exePath, ArgumentList.EscapeArguments(arguments));

            WithServiceHandle(
                ServiceAccessRights.GENERIC_READ | ServiceAccessRights.GENERIC_WRITE,
                delegate(IntPtr svcHandle)
                    {
                        const int notChanged = -1;
                        if (0 ==
                            Win32.ChangeServiceConfig(svcHandle, notChanged, notChanged, notChanged, exePath,
                                                      null, IntPtr.Zero, null, null, null, null))
                            throw new Win32Exception();
                    }
                );
        }

        /// <summary>
        /// Configures the service to auto-restart on failure
        /// </summary>
        public void SetRestartOnFailure(int restartAttempts, int restartDelay, int resetFailuresDelay)
        {
            SC_ACTION[] actions =
                new SC_ACTION[3]
                {
                    new SC_ACTION
                        {
                            Delay = Math.Max(0, Math.Min(int.MaxValue, restartDelay)),
                            Type = SC_ACTION_TYPE.SC_ACTION_RESTART
                        },
                    new SC_ACTION
                        {
                            Delay = Math.Max(0, Math.Min(int.MaxValue, restartDelay)),
                            Type = SC_ACTION_TYPE.SC_ACTION_RESTART
                        },
                    new SC_ACTION
                        {
                            Delay = Math.Max(0, Math.Min(int.MaxValue, restartDelay)),
                            Type = SC_ACTION_TYPE.SC_ACTION_RESTART
                        },
                };

            for (int i = Math.Max(0, restartAttempts); i < actions.Length; i++)
                actions[i] = new SC_ACTION { Delay = 0, Type = SC_ACTION_TYPE.SC_ACTION_NONE };

            GCHandle hdata = GCHandle.Alloc(actions, GCHandleType.Pinned);
            try
            {
                SERVICE_FAILURE_ACTIONS cfg = new SERVICE_FAILURE_ACTIONS();
                cfg.dwResetPeriod = Math.Max(-1, Math.Min(int.MaxValue, resetFailuresDelay));
                cfg.lpRebootMsg = cfg.lpCommand = IntPtr.Zero;
                cfg.cActions = actions.Length;
                cfg.lpsaActions = hdata.AddrOfPinnedObject();

                SetServiceConfig(SERVICE_CONFIG_INFO.FAILURE_ACTIONS, cfg);
            }
            finally
            {
                hdata.Free();
            }
        }

        /// <summary>
        /// Replaces the access control list for the service.
        /// </summary>
        public void SetAccess(IEnumerable<ServiceAccessEntry> aces)
        {
            uint bufSizeNeeded;
            byte[] psd = new byte[0];

            WithServiceHandle(
                ServiceAccessRights.SERVICE_ALL_ACCESS,
                delegate(IntPtr svcHandle)
                {
                    Win32.QueryServiceObjectSecurity(svcHandle, SecurityInfos.DiscretionaryAcl, psd, 0, out bufSizeNeeded);
                    if (bufSizeNeeded < 0 || bufSizeNeeded > short.MaxValue)
                        throw new Win32Exception();

                    if (!Win32.QueryServiceObjectSecurity(svcHandle, SecurityInfos.DiscretionaryAcl, psd = new byte[bufSizeNeeded], bufSizeNeeded, out bufSizeNeeded))
                        throw new Win32Exception();
                }
            );
            RawSecurityDescriptor rsd = new RawSecurityDescriptor(psd, 0);

            while (rsd.DiscretionaryAcl.Count > 0)
                rsd.DiscretionaryAcl.RemoveAce(0);

            rsd.DiscretionaryAcl.InsertAce(rsd.DiscretionaryAcl.Count,
                new CommonAce(AceFlags.None, AceQualifier.AccessAllowed, (int)ServiceAccessRights.SERVICE_ALL_ACCESS,
                    new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), false, null));

            foreach (ServiceAccessEntry ace in aces)
            {
                SecurityIdentifier sid = new SecurityIdentifier(ace.Sid, null);
                rsd.DiscretionaryAcl.InsertAce(rsd.DiscretionaryAcl.Count,
                    new CommonAce(AceFlags.None, ace.Qualifier, (int)ace.AccessMask, sid, false, null));
            }

            byte[] rawsd = new byte[rsd.BinaryLength];
            rsd.GetBinaryForm(rawsd, 0);

            WithServiceHandle(
                ServiceAccessRights.SERVICE_ALL_ACCESS,
                delegate(IntPtr svcHandle)
                {
                    if (!Win32.SetServiceObjectSecurity(svcHandle, SecurityInfos.DiscretionaryAcl, rawsd))
                        throw new Win32Exception();
                }
            );
        }

        /// <summary> Creates the specified service and returns a SvcControlManager for the service created </summary>
        public static SvcControlManager Create(string serviceName, string displayName, bool interactive,
                                               ServiceStartMode startupType, string exePath, string[] arguments,
                                               string accountName, string password)
        {
            exePath = ArgumentList.EscapeArguments(new string[] { Check.NotEmpty(exePath) });
            if (arguments != null && arguments.Length > 0)
                exePath = String.Format("{0} {1}", exePath, ArgumentList.EscapeArguments(arguments));

            using (SCMHandle hScm = new SCMHandle(SCM_ACCESS.SC_MANAGER_CREATE_SERVICE))
            {
                IntPtr hSvc = Win32.CreateService(
                    hScm,
                    serviceName,
                    displayName ?? serviceName,
                    ServiceAccessRights.SERVICE_ALL_ACCESS,
                    SC_SERVICE_TYPE.SERVICE_WIN32_OWN_PROCESS |
                    (interactive ? SC_SERVICE_TYPE.SERVICE_INTERACTIVE_PROCESS : 0),
                    startupType,
                    SC_SERVICE_ERROR_CONTROL.SERVICE_ERROR_NORMAL,
                    exePath,
                    null,
                    null,
                    null,
                    accountName,
                    password);

                if (hSvc == IntPtr.Zero)
                    throw new Win32Exception();

                Win32.CloseServiceHandle(hSvc);
            }

            return new SvcControlManager(serviceName);
        }

        /// <summary>
        /// Stops/Deletes the specified service
        /// </summary>
        public void Delete()
        {
            try
            {
                Stop();
            }
            finally
            {
                WithServiceHandle(
                    ServiceAccessRights.GENERIC_READ | ServiceAccessRights.DELETE,
                    delegate(IntPtr hSvc)
                        {
                            if (!Win32.DeleteService(hSvc))
                                throw new Win32Exception();
                        }
                    );
            }
        }

        private void WithServiceHandle(ServiceAccessRights access, Action<IntPtr> action)
        {
            using (SCMHandle hScm = new SCMHandle(SCM_ACCESS.STANDARD_RIGHTS_REQUIRED))
            {
                IntPtr hSvc = Win32.OpenService(hScm, _svcName, access);
                if (hSvc == IntPtr.Zero)
                    throw new Win32Exception();
                try
                {
                    action(hSvc);
                }
                finally
                {
                    Win32.CloseServiceHandle(hSvc);
                }
            }
        }

        #region WIN32 Service Methods
        [Flags]
        enum SCM_ACCESS : uint
        {
            STANDARD_RIGHTS_REQUIRED = 0xF0000,
            SC_MANAGER_CONNECT = 0x00001,
            SC_MANAGER_CREATE_SERVICE = 0x00002,
            SC_MANAGER_ENUMERATE_SERVICE = 0x00004,
            SC_MANAGER_LOCK = 0x00008,
            SC_MANAGER_QUERY_LOCK_STATUS = 0x00010,
            SC_MANAGER_MODIFY_BOOT_CONFIG = 0x00020,
            SC_MANAGER_ALL_ACCESS = STANDARD_RIGHTS_REQUIRED |
                             SC_MANAGER_CONNECT |
                             SC_MANAGER_CREATE_SERVICE |
                             SC_MANAGER_ENUMERATE_SERVICE |
                             SC_MANAGER_LOCK |
                             SC_MANAGER_QUERY_LOCK_STATUS |
                             SC_MANAGER_MODIFY_BOOT_CONFIG
        }

        private class SCMHandle : SafeHandle
        {
            public SCMHandle(SCM_ACCESS rights)
                : base(Win32.OpenSCManager(null, null, rights), true)
            {
                if (this.handle == IntPtr.Zero)
                {
                    GC.SuppressFinalize(this);
                    throw new Win32Exception();
                }
            }
            public override bool IsInvalid { get { return handle == IntPtr.Zero; } }
            protected override bool ReleaseHandle()
            {
                return Win32.CloseServiceHandle(handle);
            }
        }

        private enum SERVICE_CONFIG_INFO
        {
            DESCRIPTION = 1,
            FAILURE_ACTIONS = 2,
            DELAYED_AUTO_START_INFO = 3,
            FAILURE_ACTIONS_FLAG = 4,
            SERVICE_SID_INFO = 5,
            REQUIRED_PRIVILEGES_INFO = 6,
            PRESHUTDOWN_INFO = 7
        }

        private enum SC_ACTION_TYPE : uint
        {
            SC_ACTION_NONE = 0x00000000, // No action.
            SC_ACTION_RESTART = 0x00000001, // Restart the service.
            SC_ACTION_REBOOT = 0x00000002, // Reboot the computer.
            SC_ACTION_RUN_COMMAND = 0x00000003 // Run a command.
        }

        [Flags]
        private enum SC_SERVICE_TYPE : uint
        {
            SERVICE_ADAPTER = 0x00000004,
            SERVICE_FILE_SYSTEM_DRIVER = 0x00000002,
            SERVICE_KERNEL_DRIVER = 0x00000001,
            SERVICE_RECOGNIZER_DRIVER = 0x00000008,
            SERVICE_WIN32_OWN_PROCESS = 0x00000010,
            SERVICE_WIN32_SHARE_PROCESS = 0x00000020,
            SERVICE_INTERACTIVE_PROCESS = 0x00000100,
        }

        private struct SERVICE_FAILURE_ACTIONS
        {
            public Int32 dwResetPeriod;
            public IntPtr lpRebootMsg;
            public IntPtr lpCommand;
            public Int32 cActions;
            public IntPtr lpsaActions;
        }

        private struct SC_DESCRIPTION
        {
            public IntPtr Description;
        }

        private struct SC_ACTION
        {
            public SC_ACTION_TYPE Type;
            public Int32 Delay;
        }

        private enum SC_SERVICE_ERROR_CONTROL
        {
            SERVICE_ERROR_IGNORE = 0x00000000, //The startup program ignores the error and continues the startup operation.
            SERVICE_ERROR_NORMAL = 0x00000001, //The startup program logs the error in the event log but continues the startup operation.
            SERVICE_ERROR_SEVERE = 0x00000002, //The startup program logs the error in the event log. If the last-known-good configuration is being started, the startup operation continues. Otherwise, the system is restarted with the last-known-good configuration.
        }

        private static class Win32
        {  
            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool SetServiceObjectSecurity(IntPtr serviceHandle,
                                                                SecurityInfos secInfos,
                                                                [In] byte[] lpSecDesrBuf);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool QueryServiceObjectSecurity(IntPtr serviceHandle,
                                                                    SecurityInfos secInfo,
                                                                    [Out] byte[] lpSecDesrBuf, uint bufSize,
                                                                    out uint bufSizeNeeded);

            [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", ExactSpelling = true,
                CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern int ChangeServiceConfig2(IntPtr hService, int dwInfoLevel, IntPtr lpInfo);

            [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfigW", ExactSpelling = true,
                CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern int ChangeServiceConfig(IntPtr hService, int nServiceType, int nStartType,
                                                            int nErrorControl,
                                                            String lpBinaryPathName, String lpLoadOrderGroup,
                                                            IntPtr lpdwTagId, [In] String lpDependencies,
                                                            String lpServiceStartName,
                                                            String lpPassword, String lpDisplayName);

            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern IntPtr OpenService(SCMHandle hSCManager, string lpServiceName, ServiceAccessRights dwDesiredAccess);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool DeleteService(IntPtr hService);
            
            [DllImport("advapi32.dll", EntryPoint="OpenSCManagerW", ExactSpelling=true, CharSet=CharSet.Unicode, SetLastError=true)]
            public static extern IntPtr OpenSCManager(string machineName, string databaseName, SCM_ACCESS dwAccess);
            
            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool CloseServiceHandle(IntPtr hSCObject);

            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern IntPtr CreateService(
                SCMHandle hSCManager,
                string lpServiceName,
                string lpDisplayName,
                ServiceAccessRights dwDesiredAccess,
                SC_SERVICE_TYPE dwServiceType,
                ServiceStartMode dwStartType,
                SC_SERVICE_ERROR_CONTROL dwErrorControl,
                string lpBinaryPathName,
                string lpLoadOrderGroup,
                string lpdwTagId,
                string lpDependencies,
                string lpServiceStartName,
                string lpPassword);
        }
        #endregion
    }
}
