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

using System.Globalization;
using System.Management.Automation;
using System.Net;
using System.Security.Permissions;
using Microsoft.Azure.Commands.DataFactories.Properties;

namespace Microsoft.Azure.Commands.DataFactories
{
    [Cmdlet(VerbsCommon.Remove, Constants.LinkedService)]
    public class RemoveAzureDataFactoryLinkedServiceCommand : LinkedServiceContextBaseCmdlet
    {
        [Parameter(Mandatory = false, HelpMessage = "Don't ask for confirmation.")]
        public SwitchParameter Force { get; set; }

        [EnvironmentPermission(SecurityAction.Demand, Unrestricted = true)]
        public override void ExecuteCmdlet()
        {
            ConfirmAction(
                Force.IsPresent,
                string.Format(
                    CultureInfo.InvariantCulture,
                    Resources.LinkedServiceConfirmationMessage,
                    Name,
                    DataFactoryName),
                string.Format(
                    CultureInfo.InvariantCulture,
                    Resources.LinkedServiceRemoving,
                    Name,
                    DataFactoryName),
                Name,
                ExecuteDelete);
        }

        private void ExecuteDelete()
        {
            HttpStatusCode response = DataFactoryClient.DeleteLinkedService(ResourceGroupName, DataFactoryName, Name);

            if (response == HttpStatusCode.NoContent)
            {
                WriteWarning(string.Format(
                    CultureInfo.InvariantCulture, Resources.LinkedServiceNotFound, Name, DataFactoryName));
            }
        }
    }
}