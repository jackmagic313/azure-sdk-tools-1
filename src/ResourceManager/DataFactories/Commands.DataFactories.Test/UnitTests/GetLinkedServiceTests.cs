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

using System.Collections.Generic;
using Microsoft.Azure.Commands.DataFactories.Models;
using Microsoft.WindowsAzure.Commands.ScenarioTest;
using Moq;
using Xunit;

namespace Microsoft.Azure.Commands.DataFactories.Test
{
    public class GetLinkedServiceTests : DataFactoryUnitTestBase
    {
        private const string linkedServiceName = "foo";

        private GetAzureDataFactoryLinkedServiceCommand cmdlet;

        public GetLinkedServiceTests()
        {
            base.SetupTest();

            cmdlet = new GetAzureDataFactoryLinkedServiceCommand()
            {
                CommandRuntime = commandRuntimeMock.Object,
                DataFactoryClient = dataFactoriesClientMock.Object,
                ResourceGroupName = ResourceGroupName,
                DataFactoryName = DataFactoryName
            };
        }

        [Fact]
        [Trait(Category.AcceptanceType, Category.CheckIn)]
        public void CanGetLinkedService()
        {
            // Arrange
            PSLinkedService expected = new PSLinkedService()
            {
                LinkedServiceName = linkedServiceName,
                DataFactoryName = DataFactoryName,
                ResourceGroupName = ResourceGroupName
            };

            dataFactoriesClientMock
                .Setup(
                    c =>
                        c.FilterPSLinkedServices(
                            It.Is<LinkedServiceFilterOptions>(
                                options =>
                                    options.ResourceGroupName == ResourceGroupName &&
                                    options.DataFactoryName == DataFactoryName && 
                                    options.Name == linkedServiceName)))
                .CallBase()
                .Verifiable();

            dataFactoriesClientMock
                .Setup(c => c.GetLinkedService(ResourceGroupName, DataFactoryName, linkedServiceName))
                .Returns(expected)
                .Verifiable();

            // Action
            cmdlet.Name = linkedServiceName;
            cmdlet.ExecuteCmdlet();

            // Assert
            dataFactoriesClientMock.VerifyAll();

            commandRuntimeMock.Verify(f => f.WriteObject(expected), Times.Once());
        }

        [Fact]
        [Trait(Category.AcceptanceType, Category.CheckIn)]
        public void CanListLinkedServices()
        {
            // Arrange
            List<PSLinkedService> expected = new List<PSLinkedService>()
            {
                new PSLinkedService()
                {
                    LinkedServiceName = linkedServiceName,
                    DataFactoryName = DataFactoryName,
                    ResourceGroupName = ResourceGroupName
                },
                new PSLinkedService()
                {
                    LinkedServiceName = "anotherLinkedService",
                    DataFactoryName = DataFactoryName,
                    ResourceGroupName = ResourceGroupName
                }
            };

            dataFactoriesClientMock
                .Setup(
                    c =>
                        c.FilterPSLinkedServices(
                            It.Is<LinkedServiceFilterOptions>(
                                options =>
                                    options.ResourceGroupName == ResourceGroupName &&
                                    options.DataFactoryName == DataFactoryName && 
                                    options.Name == null)))
                .CallBase()
                .Verifiable();

            dataFactoriesClientMock
                .Setup(f => f.ListLinkedServices(ResourceGroupName, DataFactoryName))
                .Returns(expected)
                .Verifiable();

            // Action
            cmdlet.ExecuteCmdlet();

            // Assert
            dataFactoriesClientMock.VerifyAll();

            commandRuntimeMock.Verify(f => f.WriteObject(expected, true), Times.Once());
        }
    }
}
