﻿//
// Copyright 2015 the original author or authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using Microsoft.Extensions.Configuration;
using Xunit;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.AspNet.Hosting;
using Microsoft.Extensions.Configuration.Xml;

namespace Spring.Extensions.Configuration.Server.Test
{
    public class ConfigServerConfigurationExtensionsTest
    {
        [Fact]
        public void AddConfigServer_ThrowsIfConfigBuilderNull()
        {
            // Arrange
            IConfigurationBuilder configurationBuilder = null;
            var environment = new HostingEnvironment();

            // Act and Assert
            var ex = Assert.Throws<ArgumentNullException>(() => ConfigServerConfigurationExtensions.AddConfigServer(configurationBuilder, environment));
            Assert.Contains(nameof(configurationBuilder), ex.Message);

        }

        [Fact]
        public void AddConfigServer_ThrowsIfHostingEnvironmentNull()
        {
            // Arrange
            IConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
            IHostingEnvironment environment = null;
            // Act and Assert
            var ex = Assert.Throws<ArgumentNullException>(() => ConfigServerConfigurationExtensions.AddConfigServer(configurationBuilder, environment));
            Assert.Contains(nameof(environment), ex.Message);

        }

        [Fact]
        public void AddConfigServer_AddsConfigServerProviderToProvidersList()
        {
            // Arrange
            var configurationBuilder = new ConfigurationBuilder();
            var environment = new HostingEnvironment();

            // Act and Assert
            configurationBuilder.AddConfigServer(environment);

            ConfigServerConfigurationProvider configServerProvider = null;
            foreach (IConfigurationProvider provider in configurationBuilder.Providers)
            {
                configServerProvider = provider as ConfigServerConfigurationProvider;
                if (configServerProvider != null)
                    break;
            }
            Assert.NotNull(configServerProvider);

        }

        [Fact]
        public void AddConfigServer_WithLoggerFactorySucceeds()
        {
            // Arrange
            var configurationBuilder = new ConfigurationBuilder();
            var loggerFactory = new LoggerFactory();
            var environment = new HostingEnvironment();

            // Act and Assert
            configurationBuilder.AddConfigServer(environment,loggerFactory);

            ConfigServerConfigurationProvider configServerProvider = null;
            foreach (IConfigurationProvider provider in configurationBuilder.Providers)
            {
                configServerProvider = provider as ConfigServerConfigurationProvider;
                if (configServerProvider != null)
                    break;
            }

            Assert.NotNull(configServerProvider);
            Assert.NotNull(configServerProvider.Logger);

        }

        [Fact]
        public void AddConfigServer_JsonAppSettingsConfiguresClient()
        {
            // Arrange
            var appsettings = @"
{
    'spring': {
        'application': {
            'name': 'myName'
    },
      'cloud': {
        'config': {
            'uri': 'http://user:password@foo.com:9999',
            'enabled': false,
            'failFast': false,
            'label': 'myLabel',
            'username': 'myUsername',
            'password': 'myPassword'
        }
      }
    }
}";

            var path = ConfigServerTestHelpers.CreateTempFile(appsettings);
            var configurationBuilder = new ConfigurationBuilder();
            var environment = new HostingEnvironment();
            configurationBuilder.AddJsonFile(path);

            // Act and Assert
            configurationBuilder.AddConfigServer(environment);

            ConfigServerConfigurationProvider configServerProvider = null;
            foreach (IConfigurationProvider provider in configurationBuilder.Providers)
            {
                configServerProvider = provider as ConfigServerConfigurationProvider;
                if (configServerProvider != null)
                    break;
            }
            Assert.NotNull(configServerProvider);
            ConfigServerClientSettings settings = configServerProvider.Settings;

            Assert.False(settings.Enabled);
            Assert.False(settings.FailFast);
            Assert.Equal("http://user:password@foo.com:9999", settings.Uri);
            Assert.Equal(ConfigServerClientSettings.DEFAULT_ENVIRONMENT, settings.Environment);
            Assert.Equal("myName", settings.Name);
            Assert.Equal("myLabel", settings.Label);
            Assert.Equal("myUsername", settings.Username);
            Assert.Equal("myPassword", settings.Password);
        }


        [Fact]
        public void AddConfigServer_XmlAppSettingsConfiguresClient()
        {
            // Arrange
            var appsettings = @"
<settings>
    <spring>
      <cloud>
        <config>
            <uri>http://foo.com:9999</uri>
            <enabled>false</enabled>
            <failFast>false</failFast>
            <label>myLabel</label>
            <name>myName</name>
            <username>myUsername</username>
            <password>myPassword</password>
        </config>
      </cloud>
    </spring>
</settings>";
            var path = ConfigServerTestHelpers.CreateTempFile(appsettings);
            var configurationBuilder = new ConfigurationBuilder();
            var environment = new HostingEnvironment();
            configurationBuilder.AddXmlFile(path);

            // Act and Assert
            configurationBuilder.AddConfigServer(environment);

            ConfigServerConfigurationProvider configServerProvider = null;
            foreach (IConfigurationProvider provider in configurationBuilder.Providers)
            {
                configServerProvider = provider as ConfigServerConfigurationProvider;
                if (configServerProvider != null)
                    break;
            }
            Assert.NotNull(configServerProvider);
            ConfigServerClientSettings settings = configServerProvider.Settings;

            Assert.False(settings.Enabled);
            Assert.False(settings.FailFast);
            Assert.Equal("http://foo.com:9999", settings.Uri);
            Assert.Equal(ConfigServerClientSettings.DEFAULT_ENVIRONMENT, settings.Environment);
            Assert.Equal("myName", settings.Name);
            Assert.Equal("myLabel", settings.Label);
            Assert.Equal("myUsername", settings.Username);
            Assert.Equal("myPassword", settings.Password);
   

        }
        [Fact]
        public void AddConfigServer_IniAppSettingsConfiguresClient()
        {
            // Arrange
            var appsettings = @"
[spring:cloud:config]
    uri=http://foo.com:9999
    enabled=false
    failFast=false
    label=myLabel
    name=myName
    username=myUsername
    password=myPassword
";
            var path = ConfigServerTestHelpers.CreateTempFile(appsettings);
            var configurationBuilder = new ConfigurationBuilder();
            var environment = new HostingEnvironment();
            configurationBuilder.AddIniFile(path);

            // Act and Assert
            configurationBuilder.AddConfigServer(environment);

            ConfigServerConfigurationProvider configServerProvider = null;
            foreach (IConfigurationProvider provider in configurationBuilder.Providers)
            {
                configServerProvider = provider as ConfigServerConfigurationProvider;
                if (configServerProvider != null)
                    break;
            }
            Assert.NotNull(configServerProvider);
            ConfigServerClientSettings settings = configServerProvider.Settings;

            // Act and Assert
            Assert.False(settings.Enabled);
            Assert.False(settings.FailFast);
            Assert.Equal("http://foo.com:9999", settings.Uri);
            Assert.Equal(ConfigServerClientSettings.DEFAULT_ENVIRONMENT, settings.Environment);
            Assert.Equal("myName", settings.Name);
            Assert.Equal("myLabel", settings.Label);
            Assert.Equal("myUsername", settings.Username);
            Assert.Equal("myPassword", settings.Password);


        }

        [Fact]
        public void AddConfigServer_CommandLineAppSettingsConfiguresClient()
        {
            // Arrange
            var appsettings = new string[]
                {
                    "spring:cloud:config:enabled=false",
                    "--spring:cloud:config:failFast=false",
                    "/spring:cloud:config:uri=http://foo.com:9999",
                    "--spring:cloud:config:name", "myName",
                    "/spring:cloud:config:label", "myLabel",
                    "--spring:cloud:config:username", "myUsername",
                    "--spring:cloud:config:password", "myPassword"
                };

            var configurationBuilder = new ConfigurationBuilder();
            var environment = new HostingEnvironment();
            configurationBuilder.AddCommandLine(appsettings);

            // Act and Assert
            configurationBuilder.AddConfigServer(environment);

            ConfigServerConfigurationProvider configServerProvider = null;
            foreach (IConfigurationProvider provider in configurationBuilder.Providers)
            {
                configServerProvider = provider as ConfigServerConfigurationProvider;
                if (configServerProvider != null)
                    break;
            }
            Assert.NotNull(configServerProvider);
            ConfigServerClientSettings settings = configServerProvider.Settings;

            Assert.False(settings.Enabled);
            Assert.False(settings.FailFast);
            Assert.Equal("http://foo.com:9999", settings.Uri);
            Assert.Equal(ConfigServerClientSettings.DEFAULT_ENVIRONMENT, settings.Environment);
            Assert.Equal("myName", settings.Name );
            Assert.Equal("myLabel", settings.Label );
            Assert.Equal("myUsername", settings.Username);
            Assert.Equal("myPassword", settings.Password );


        }

        [Fact]
        public void AddConfigServer_HandlesPlaceHolders()
        {
            // Arrange
            var appsettings = @"
{
    'foo': {
        'bar': {
            'name': 'testName'
        },
    },
    'spring': {
        'application': {
            'name': 'myName'
        },
      'cloud': {
        'config': {
            'uri': 'http://user:password@foo.com:9999',
            'enabled': false,
            'failFast': false,
            'name': '${foo:bar:name?foobar}',
            'label': 'myLabel',
            'username': 'myUsername',
            'password': 'myPassword'
        }
      }
    }
}";

            var path = ConfigServerTestHelpers.CreateTempFile(appsettings);
            var configurationBuilder = new ConfigurationBuilder();
            var environment = new HostingEnvironment();
            configurationBuilder.AddJsonFile(path);

            // Act and Assert
            configurationBuilder.AddConfigServer(environment);

            ConfigServerConfigurationProvider configServerProvider = null;
            foreach (IConfigurationProvider provider in configurationBuilder.Providers)
            {
                configServerProvider = provider as ConfigServerConfigurationProvider;
                if (configServerProvider != null)
                    break;
            }
            Assert.NotNull(configServerProvider);
            ConfigServerClientSettings settings = configServerProvider.Settings;

            Assert.False(settings.Enabled);
            Assert.False(settings.FailFast);
            Assert.Equal("http://user:password@foo.com:9999", settings.Uri);
            Assert.Equal(ConfigServerClientSettings.DEFAULT_ENVIRONMENT, settings.Environment);
            Assert.Equal("testName", settings.Name);
            Assert.Equal("myLabel", settings.Label);
            Assert.Equal("myUsername", settings.Username);
            Assert.Equal("myPassword", settings.Password);

        }

    }
}
