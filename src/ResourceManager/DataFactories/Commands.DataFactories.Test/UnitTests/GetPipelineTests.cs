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
using System.Collections.Generic;
using Microsoft.Azure.Commands.DataFactories.Models;
using System.Management.Automation;
using Microsoft.WindowsAzure.Commands.ScenarioTest;
using Moq;
using Xunit;

namespace Microsoft.Azure.Commands.DataFactories.Test
{
    public class GetPipelineTests : DataFactoryUnitTestBase
    {
        private const string pipelineName = "foo";

        private GetAzureDataFactoryPipelineCommand cmdlet;

        public GetPipelineTests()
        {
            base.SetupTest();

            cmdlet = new GetAzureDataFactoryPipelineCommand()
            {
                CommandRuntime = commandRuntimeMock.Object,
                DataFactoryClient = dataFactoriesClientMock.Object,
                ResourceGroupName = ResourceGroupName,
                DataFactoryName = DataFactoryName
            };
        }

        [Fact]
        [Trait(Category.AcceptanceType, Category.CheckIn)]
        public void CanGetPipeline()
        {
            // Arrange
            PSPipeline expected = new PSPipeline()
            {
                PipelineName = pipelineName,
                DataFactoryName = DataFactoryName,
                ResourceGroupName = ResourceGroupName
            };

            dataFactoriesClientMock
                .Setup(
                    c =>
                        c.FilterPSPipelines(
                            It.Is<PipelineFilterOptions>(
                                options =>
                                    options.ResourceGroupName == ResourceGroupName &&
                                    options.DataFactoryName == DataFactoryName &&
                                    options.Name == pipelineName)))
                .CallBase()
                .Verifiable();

            dataFactoriesClientMock
                .Setup(c => c.GetPipeline(ResourceGroupName, DataFactoryName, pipelineName))
                .Returns(expected)
                .Verifiable();

            // Action
            cmdlet.Name = "  ";
            Exception whiteSpace = Assert.Throws<PSArgumentNullException>(() => cmdlet.ExecuteCmdlet());

            cmdlet.Name = "";
            Exception empty = Assert.Throws<PSArgumentNullException>(() => cmdlet.ExecuteCmdlet());

            cmdlet.Name = pipelineName;
            cmdlet.ExecuteCmdlet();

            // Assert
            dataFactoriesClientMock.VerifyAll();
            Assert.Contains("Value cannot be null", whiteSpace.Message);
            Assert.Contains("Value cannot be null", empty.Message);

            commandRuntimeMock.Verify(f => f.WriteObject(expected), Times.Once());
        }

        [Fact]
        [Trait(Category.AcceptanceType, Category.CheckIn)]
        public void CanListPipelines()
        {
            // Arrange
            List<PSPipeline> expected = new List<PSPipeline>()
            {
                new PSPipeline()
                {
                    PipelineName = pipelineName,
                    DataFactoryName = DataFactoryName,
                    ResourceGroupName = ResourceGroupName
                },
                new PSPipeline()
                {
                    PipelineName = "anotherPipeline",
                    DataFactoryName = DataFactoryName,
                    ResourceGroupName = ResourceGroupName
                }
            };

            dataFactoriesClientMock
                .Setup(
                    c =>
                        c.FilterPSPipelines(
                            It.Is<PipelineFilterOptions>(
                                options =>
                                    options.ResourceGroupName == ResourceGroupName &&
                                    options.DataFactoryName == DataFactoryName &&
                                    options.Name == null)))
                .CallBase()
                .Verifiable();

            dataFactoriesClientMock
                .Setup(f => f.ListPipelines(ResourceGroupName, DataFactoryName))
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
