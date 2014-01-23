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
using System.Security.AccessControl;
using System.Security.Principal;

namespace CSharpTest.Net.Services
{
    /// <summary>
    /// An over-simplified ACE for a service's access control
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class ServiceAccessEntry : Attribute
    {
        /// <summary> The well-known account identifier to grant/deny access to </summary>
        public readonly WellKnownSidType Sid;
        /// <summary> Grant or deny the access </summary>
        public readonly AceQualifier Qualifier;
        /// <summary> The permission(s) to grant </summary>
        public readonly ServiceAccessRights AccessMask;

        /// <summary>
        /// Constructs a ServiceAccessEntry to represent a simplified ACE entry for a service
        /// </summary>
        public ServiceAccessEntry(WellKnownSidType sid, AceQualifier qualifier, ServiceAccessRights accessMask)
        {
            Sid = sid;
            Qualifier = qualifier;
            AccessMask = accessMask;
        }
    }
    /// <summary>
    /// Access control rights specific to a Win32 Service
    /// </summary>
    [Flags]
    public enum ServiceAccessRights : uint
    {
        /// <summary>
        /// Required to call the QueryServiceConfig and 
        /// QueryServiceConfig2 functions to query the service configuration.
        /// </summary>
        SERVICE_QUERY_CONFIG = 0x00001,

        /// <summary>
        /// Required to call the ChangeServiceConfig or ChangeServiceConfig2 function 
        /// to change the service configuration. Because this grants the caller 
        /// the right to change the executable file that the system runs, 
        /// it should be granted only to administrators.
        /// </summary>
        SERVICE_CHANGE_CONFIG = 0x00002,

        /// <summary>
        /// Required to call the QueryServiceStatusEx function to ask the service 
        /// control manager about the status of the service.
        /// </summary>
        SERVICE_QUERY_STATUS = 0x00004,

        /// <summary>
        /// Required to call the EnumDependentServices function to enumerate all 
        /// the services dependent on the service.
        /// </summary>
        SERVICE_ENUMERATE_DEPENDENTS = 0x00008,

        /// <summary>
        /// Required to call the StartService function to start the service.
        /// </summary>
        SERVICE_START = 0x00010,

        /// <summary>
        ///     Required to call the ControlService function to stop the service.
        /// </summary>
        SERVICE_STOP = 0x00020,

        /// <summary>
        /// Required to call the ControlService function to pause or continue 
        /// the service.
        /// </summary>
        SERVICE_PAUSE_CONTINUE = 0x00040,

        /// <summary>
        /// Required to call the EnumDependentServices function to enumerate all
        /// the services dependent on the service.
        /// </summary>
        SERVICE_INTERROGATE = 0x00080,

        /// <summary>
        /// Required to call the ControlService function to specify a user-defined
        /// control code.
        /// </summary>
        SERVICE_USER_DEFINED_CONTROL = 0x00100,

        // From ACCESS_MASK
        /// <summary> READ_CONTROL, DELETE, WRITE_DAC, WRITE_OWNER </summary>
        STANDARD_RIGHTS_REQUIRED = 0x000f0000,
        /// <summary> READ_CONTROL </summary>
        STANDARD_RIGHTS_READ = 0x00020000,
        /// <summary> READ_CONTROL </summary>
        STANDARD_RIGHTS_WRITE = 0x00020000,
        /// <summary> READ_CONTROL </summary>
        STANDARD_RIGHTS_EXECUTE = 0x00020000,

        /// <summary>
        /// Includes STANDARD_RIGHTS_REQUIRED in addition to all access rights in this table.
        /// </summary>
        SERVICE_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED |
                              SERVICE_QUERY_CONFIG |
                              SERVICE_CHANGE_CONFIG |
                              SERVICE_QUERY_STATUS |
                              SERVICE_ENUMERATE_DEPENDENTS |
                              SERVICE_START |
                              SERVICE_STOP |
                              SERVICE_PAUSE_CONTINUE |
                              SERVICE_INTERROGATE |
                              SERVICE_USER_DEFINED_CONTROL),

        /// <summary>
        /// Includes STANDARD_RIGHTS_READ, SERVICE_QUERY_CONFIG, SERVICE_QUERY_STATUS, SERVICE_INTERROGATE and SERVICE_ENUMERATE_DEPENDENTS.
        /// </summary>
        GENERIC_READ = STANDARD_RIGHTS_READ |
                       SERVICE_QUERY_CONFIG |
                       SERVICE_QUERY_STATUS |
                       SERVICE_INTERROGATE |
                       SERVICE_ENUMERATE_DEPENDENTS,

        /// <summary>
        /// Includes STANDARD_RIGHTS_WRITE and SERVICE_CHANGE_CONFIG.
        /// </summary>
        GENERIC_WRITE = STANDARD_RIGHTS_WRITE |
                        SERVICE_CHANGE_CONFIG,

        /// <summary>
        /// Includes STANDARD_RIGHTS_EXECUTE, SERVICE_START, SERVICE_STOP, SERVICE_PAUSE_CONTINUE, and SERVICE_USER_DEFINED_CONTROL
        /// </summary>
        GENERIC_EXECUTE = STANDARD_RIGHTS_EXECUTE |
                          SERVICE_START |
                          SERVICE_STOP |
                          SERVICE_PAUSE_CONTINUE |
                          SERVICE_USER_DEFINED_CONTROL,

        /// <summary>
        /// Required to call the QueryServiceObjectSecurity or 
        /// SetServiceObjectSecurity function to access the SACL. The proper
        /// way to obtain this access is to enable the SE_SECURITY_NAME 
        /// privilege in the caller's current access token, open the handle 
        /// for ACCESS_SYSTEM_SECURITY access, and then disable the privilege.
        /// </summary>
        ACCESS_SYSTEM_SECURITY = 0x01000000, //ACCESS_MASK.ACCESS_SYSTEM_SECURITY,

        /// <summary>
        /// Required to call the DeleteService function to delete the service.
        /// </summary>
        DELETE = 0x00010000, //ACCESS_MASK.DELETE,

        /// <summary>
        /// Required to call the QueryServiceObjectSecurity function to query
        /// the security descriptor of the service object.
        /// </summary>
        READ_CONTROL = 0x00020000, //ACCESS_MASK.READ_CONTROL,

        /// <summary>
        /// Required to call the SetServiceObjectSecurity function to modify
        /// the Dacl member of the service object's security descriptor.
        /// </summary>
        WRITE_DAC = 0x00040000, //ACCESS_MASK.WRITE_DAC,

        /// <summary>
        /// Required to call the SetServiceObjectSecurity function to modify 
        /// the Owner and Group members of the service object's security 
        /// descriptor.
        /// </summary>
        WRITE_OWNER = 0x00080000, //ACCESS_MASK.WRITE_OWNER,
    }
}
