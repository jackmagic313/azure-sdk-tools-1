﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------


using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.WindowsAzure.Commands.Common.Storage;
using Microsoft.WindowsAzure.Commands.ServiceManagement.IaaS.Extensions.DSC;
using Microsoft.WindowsAzure.Commands.ServiceManagement.Model;
using Microsoft.WindowsAzure.Commands.ServiceManagement.Properties;
using Microsoft.WindowsAzure.Commands.Utilities.Common;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace Microsoft.WindowsAzure.Commands.ServiceManagement.IaaS.Extensions
{

    [Cmdlet(VerbsCommon.Set, VirtualMachineDscExtensionCmdletNoun, SupportsShouldProcess = true)]
    [OutputType(typeof(IPersistentVM))]
    public class SetAzureVMDscExtensionCommand : VirtualMachineDscExtensionCmdletBase
    {
        /// <summary>
        /// The Extension Reference Name
        /// </summary>
        [Parameter(
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The Extension Reference Name")]
        [ValidateNotNullOrEmpty]
        public override string ReferenceName { get; set; }

        /// <summary>
        /// A hashtable specifying the arguments to the ConfigurationFunction. The keys 
        /// correspond to the parameter names and the values to the parameter values.
        /// </summary>
        [Parameter(
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "A hashtable specifying the arguments to the ConfigurationFunction")]
        [ValidateNotNullOrEmpty]
        public Hashtable ConfigurationArgument { get; set; }

        /// <summary>
        /// Path to a .psd1 file that specifies the data for the Configuration. This 
        /// file must contain a hashtable with the items described in 
        /// http://technet.microsoft.com/en-us/library/dn249925.aspx.
        /// </summary>
        [Parameter(
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Path to a .psd1 file that specifies the data for the Configuration")]
        [ValidateNotNullOrEmpty]
        public string ConfigurationDataPath { get; set; }

        /// <summary>
        /// The name of the configuration archive that was previously uploaded by 
        /// Publish-AzureVMDSCConfiguration. This parameter must specify only the name 
        /// of the file, without any path.
        /// A null value or empty string indicate that the VM extension should install DSC,
        /// but not start any configuration
        /// </summary>
        [Parameter(
            Mandatory = true,
            Position = 1,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The name of the configuration file that was previously uploaded by Publish-AzureVMDSCConfiguration")]
        [AllowEmptyString]
        [AllowNull]
        public string ConfigurationArchive { get; set; }

        /// <summary>
        /// Name of the configuration that will be invoked by the DSC Extension. The value of this parameter should be the name of one of the configurations 
        /// contained within the file specified by ConfigurationArchive.
        /// 
        /// If omitted, this parameter will default to the name of the file given by the ConfigurationArchive parameter, excluding any extension, for example if 
        /// ConfigurationArchive is "SalesWebSite.ps1", the default value for ConfigurationName will be "SalesWebSite".
        /// </summary>
        [Parameter(
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Name of the configuration that will be invoked by the DSC Extension")]
        [ValidateNotNullOrEmpty]
        public string ConfigurationName { get; set; }

        /// <summary>
        /// Name of the Azure Storage Container where the configuration script is located.
        /// </summary>
        [Parameter(
            ValueFromPipelineByPropertyName = true,
            HelpMessage = " Name of the Azure Storage Container where the configuration script is located")]
        [ValidateNotNullOrEmpty]
        public string ContainerName { get; set; }

        /// <summary>
        /// By default Set-AzureVMDscExtension will not overwrite any existing blobs. Use -Force to overwrite them.
        /// </summary>
        [Parameter(HelpMessage = "Use this parameter to overwrite any existing blobs")]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// The Azure Storage Context that provides the security settings used to access the configuration script. This context should provide read access to the 
        /// container specified by ContainerName.
        /// </summary>
        [Parameter(
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The Azure Storage Context that provides the security settings used to access the configuration script")]
        [ValidateNotNullOrEmpty]
        public AzureStorageContext StorageContext { get; set; }

        /// <summary>
        /// The specific version of the DSC extension that Set-AzureVMDSCExtension will 
        /// apply the settings to. If not given, it will default to "1.*"
        /// </summary>
        [Parameter(
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The version of the DSC extension that Set-AzureVMDSCExtension will apply the settings to")]
        [ValidateNotNullOrEmpty]
        public override string Version { get; set; }

        /// <summary>
        /// The DNS endpoint suffix for all storage services, e.g. "core.windows.net".
        /// </summary>
        [Parameter(
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The Storage Endpoint Suffix.")]
        [ValidateNotNullOrEmpty]
        public string StorageEndpointSuffix { get; set; }

        /// <summary>
        /// Credentials used to access Azure Storage
        /// </summary>
        private StorageCredentials _storageCredentials;
        
        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            ExecuteCommand();
        }

        internal void ExecuteCommand()
        {
            ValidateParameters();
            
            CreateConfiguration();

            this.ConfirmAction(true, string.Empty, string.Format(CultureInfo.CurrentUICulture, Resources.AzureVMDscApplyConfigurationAction, this.ConfigurationName), "VM", () =>
            {
                RemovePredicateExtensions();
                AddResourceExtension();
            });

            WriteObject(VM);
        }

        protected override void ValidateParameters()
        {
            base.ValidateParameters();

            //
            // Validate parameters
            //
            if (string.IsNullOrEmpty(this.ConfigurationArchive))
            {
                if (this.ConfigurationName != null || this.ConfigurationArgument != null || this.ConfigurationDataPath != null)
                {
                    this.ThrowInvalidArgumentError(Resources.AzureVMDscNullArchiveNoConfiguragionParameters);
                }
                if (this.StorageContext != null || this.ContainerName != null || this.StorageEndpointSuffix != null)
                {
                    this.ThrowInvalidArgumentError(Resources.AzureVMDscNullArchiveNoStorageParameters);
                }
            }
            else 
            {
                if (string.Compare(Path.GetFileName(this.ConfigurationArchive), this.ConfigurationArchive, StringComparison.InvariantCultureIgnoreCase) != 0)
                {
                    this.ThrowInvalidArgumentError(Resources.AzureVMDscConfigurationDataFileShouldNotIncludePath);
                }

                if (this.ConfigurationDataPath != null)
                {
                    this.ConfigurationDataPath = this.GetUnresolvedProviderPathFromPSPath(this.ConfigurationDataPath);

                    if (!File.Exists(this.ConfigurationDataPath))
                    {
                        this.ThrowInvalidArgumentError(Resources.AzureVMDscCannotFindConfigurationDataFile, this.ConfigurationDataPath);
                    }
                    if (string.Compare(Path.GetExtension(this.ConfigurationDataPath), ".psd1", StringComparison.InvariantCultureIgnoreCase) != 0)
                    {
                        this.ThrowInvalidArgumentError(Resources.AzureVMDscInvalidConfigurationDataFile);
                    }
                }

                this._storageCredentials = this.GetStorageCredentials(this.StorageContext);

                if (this.ConfigurationName == null)
                {
                    this.ConfigurationName = Path.GetFileNameWithoutExtension(this.ConfigurationArchive);
                }

                if (this.ContainerName == null)
                {
                    this.ContainerName = DefaultContainerName;
                }
            }

            if (this.Version == null)
            {
                this.Version = DefaultExtensionVersion;
            }

            if (this.ReferenceName == null)
            {
                this.ReferenceName = ExtensionPublishedName;
            }
        }

        private void CreateConfiguration()
        {
            var publicSettings = new DscPublicSettings();
            var privateSettings = new DscPrivateSettings();
            publicSettings.ProtocolVersion = CurrentProtocolVersion;

            if (!string.IsNullOrEmpty(this.ConfigurationArchive))
            {
                //
                // Get a reference to the container in blob storage
                //
                var storageAccount = string.IsNullOrEmpty(this.StorageEndpointSuffix)
                                            ? new CloudStorageAccount(this._storageCredentials, true)
                                            : new CloudStorageAccount(this._storageCredentials,
                                                                    this.StorageEndpointSuffix, true);

                var blobClient = storageAccount.CreateCloudBlobClient();

                var containerReference = blobClient.GetContainerReference(this.ContainerName);

                //
                // Get a reference to the configuration blob and create a SAS token to access it
                //
                var blobAccessPolicy = new SharedAccessBlobPolicy()
                    {
                        SharedAccessExpiryTime = DateTime.UtcNow.AddHours(1),
                        Permissions = SharedAccessBlobPermissions.Read
                    };

                var configurationBlobName = this.ConfigurationArchive;

                var configurationBlobReference = containerReference.GetBlockBlobReference(configurationBlobName);

                var configurationBlobSasToken = configurationBlobReference.GetSharedAccessSignature(blobAccessPolicy);

                //
                // Upload the configuration data to blob storage and get a SAS token
                //
                string configurationDataBlobUri = null;

                if (this.ConfigurationDataPath != null)
                {
                    this.ConfirmAction(true, string.Empty,
                                        string.Format(CultureInfo.CurrentUICulture,
                                                        Resources.AzureVMDscUploadToBlobStorageAction,
                                                        this.ConfigurationDataPath),
                                        configurationBlobReference.Uri.AbsoluteUri, () =>
                                            {
                                                var guid = Guid.NewGuid();
                                                // there may be multiple VMs using the same configuration

                                                var configurationDataBlobName =
                                                    string.Format(CultureInfo.InvariantCulture, "{0}-{1}.psd1",
                                                                    this.ConfigurationName, guid);

                                                var configurationDataBlobReference =
                                                    containerReference.GetBlockBlobReference(
                                                        configurationDataBlobName);

                                                if (!this.Force && configurationDataBlobReference.Exists())
                                                {
                                                    this.ThrowTerminatingError(
                                                        new ErrorRecord(
                                                            new UnauthorizedAccessException(
                                                                string.Format(CultureInfo.CurrentUICulture,
                                                                                Resources.AzureVMDscStorageBlobAlreadyExists,
                                                                                configurationDataBlobName)),
                                                            "StorageBlobAlreadyExists",
                                                            ErrorCategory.PermissionDenied,
                                                            null));
                                                }

                                                configurationDataBlobReference.UploadFromFile(
                                                    this.ConfigurationDataPath, FileMode.Open);

                                                var configurationDataBlobSasToken =
                                                    configurationDataBlobReference.GetSharedAccessSignature(
                                                        blobAccessPolicy);

                                                configurationDataBlobUri =
                                                    configurationDataBlobReference.StorageUri.PrimaryUri.AbsoluteUri +
                                                    configurationDataBlobSasToken;
                                            });
                }

                publicSettings.SasToken = configurationBlobSasToken;
                publicSettings.ModulesUrl = configurationBlobReference.StorageUri.PrimaryUri.AbsoluteUri;
                publicSettings.ConfigurationFunction = string.Format(CultureInfo.InvariantCulture, "{0}\\{1}",
                                                                        Path.GetFileNameWithoutExtension(
                                                                            this.ConfigurationArchive),
                                                                        this.ConfigurationName);
                Tuple<DscPublicSettings.Property[], Hashtable> settings = ConvertSettings(this.ConfigurationArgument);
                publicSettings.Properties = settings.Item1;
                privateSettings.Items = settings.Item2;

                privateSettings.DataBlobUri = configurationDataBlobUri;
            }

            //
            // Define the public and private property bags that will be passed to the extension.
            //
            this.PublicConfiguration = JsonConvert.SerializeObject(publicSettings);
            //
            // PrivateConfuguration contains sensitive data in a plain text.
            //
            this.PrivateConfiguration = JsonConvert.SerializeObject(privateSettings);
        }

        /// <summary>
        /// Convert hashtable of public settings into two parts:
        /// 1) Array of public settings in format:
        /// [
        ///             {
        ///                 "Name":  "String Parameter",
        ///                 "Value":  "String Value",
        ///                 "TypeName":  "System.String"
        ///             }
        /// ]
        /// 2) Private settings hashtable. We extract all sensitive information (like password from PSCredential)
        ///    and store it in private settings. Public settings will reference them in form:
        ///             {
        ///                 "Name":  "AdminCredential",
        ///                 "Value":  
        ///                 {
        ///                     "Password" : "PrivateSettings:28AC4D36-A99B-41DE-8421-2BCC1C7C1A3B"
        ///                     "UserName" : "DOMAIN\LOGIN"
        ///                 },
        ///                 "TypeName":  "System.Management.Automation.PSCredential"
        ///             }
        /// and private hashtable will look like that:
        /// {
        ///     "28AC4D36-A99B-41DE-8421-2BCC1C7C1A3B" : "password"
        /// }
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns>tuple of array (public settings) and hashtable (private settings)</returns>
        private Tuple<DscPublicSettings.Property[], Hashtable> ConvertSettings(Hashtable arguments)
        {
            var publicSettings = new List<DscPublicSettings.Property>();
            var privateSettings = new Hashtable();
            if (arguments != null)
            {
                foreach (DictionaryEntry argument in arguments)
                {
                    object entryValue = argument.Value;
                    string entryType = argument.Value == null ? "null" : argument.Value.GetType().ToString();
                    string entryName = argument.Key.ToString();
                    // Special case for PSCredential
                    PSCredential credential = argument.Value as PSCredential;
                    if (credential == null)
                    {
                        PSObject psObject = argument.Value as PSObject;
                        if (psObject != null)
                        {
                            credential = psObject.BaseObject as PSCredential;
                        }
                    }
                    if (credential != null)
                    {
                        // plainTextPassword is a string object with sensitive information  in plain text. 
                        // We pass it to 3rd party serializer which may create copies of the string.
                        string plainTextPassword = ConvertToUnsecureString(credential.Password);
                        string userName = credential.UserName;
                        string passwordRef = Guid.NewGuid().ToString();
                        privateSettings.Add(passwordRef, plainTextPassword);
                        var newValue = new Hashtable();
                        newValue["UserName"] = String.Format(CultureInfo.InvariantCulture, userName);
                        newValue["Password"] = String.Format(CultureInfo.InvariantCulture, "PrivateSettingsRef:{0}",
                            passwordRef);
                        entryValue = newValue;
                        entryType = typeof (PSCredential).ToString();
                    }

                    var entry = new DscPublicSettings.Property()
                    {
                        Name = entryName,
                        TypeName = entryType,
                        Value = entryValue,
                    };
                    publicSettings.Add(entry);
                }
            }
            return new Tuple<DscPublicSettings.Property[], Hashtable>(publicSettings.ToArray(), privateSettings);
        }

        /// <summary>
        /// Converte SecureString to String.
        /// </summary>
        /// <remarks>
        /// This method creates a managed object with sensitive information and undetermined lifecycle.
        /// </remarks>
        /// <param name="source"></param>
        /// <returns></returns>
        private static string ConvertToUnsecureString(SecureString source)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(source);
                return Marshal.PtrToStringUni(unmanagedString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }
    }
}
