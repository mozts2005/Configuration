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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Microsoft.AspNet.Hosting;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Net.Security;

namespace Spring.Extensions.Configuration.Common
{

    public class ConfigServerConfigurationProviderBase : ConfigurationProvider
    {

        private static readonly TimeSpan DEFAULT_TIMEOUT = new TimeSpan(0,0,5);
        protected ConfigServerClientSettingsBase _settings;
        protected HttpClient _client;
        protected ILogger _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="ConfigServerConfigurationProvider"/>.
        /// </summary>
        /// <param name="settings">the configuration settings the provider uses when
        /// accessing the server.</param>
        /// <param name="logFactory">optional logging factory</param>
        /// </summary>
        internal protected ConfigServerConfigurationProviderBase(ConfigServerClientSettingsBase settings, ILoggerFactory logFactory = null) :
            this(settings, GetHttpClient(settings), logFactory)
        {
            _client.Timeout = DEFAULT_TIMEOUT;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ConfigServerConfigurationProvider"/>.
        /// </summary>
        /// <param name="settings">the configuration settings the provider uses when
        /// accessing the server.</param>
        /// <param name="httpClient">a HttpClient the provider uses to make requests of
        /// the server.</param>
        /// <param name="logFactory">optional logging factory</param>
        /// </summary>
        internal protected ConfigServerConfigurationProviderBase(ConfigServerClientSettingsBase settings, HttpClient httpClient, ILoggerFactory logFactory = null)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            
            if (httpClient == null)
            {
                throw new ArgumentNullException(nameof(httpClient));
            }

            _logger = logFactory?.CreateLogger<ConfigServerConfigurationProviderBase>();
            _settings = settings;
            _client = httpClient;
        }

 
        /// <summary>
        /// Loads configuration data from the Spring Cloud Configuration Server as specified by
        /// the <see cref="Settings"/> 
        /// </summary>
        public override void Load()
        {
            // Adds client settings (e.g spring:cloud:config:uri) to the Data dictionary
            AddConfigServerClientSettings();

            Exception error = null;

            try {

                // Make Config Server URI from settings
                var path = GetConfigServerUri();

                // Invoke config server, and wait for results
                Task<Environment> task = RemoteLoadAsync(path);
                task.Wait();
                Environment env = task.Result;

                // Update config Data dictionary with any results
                if (env != null)
                {
                    _logger?.LogInformation("Located environment: {0}, {1}, {2}, {3}", env.Name, env.Profiles, env.Label, env.Version);
                    var sources = env.PropertySources;
                    if (sources != null)
                    {

                        foreach (PropertySource source in sources)
                        {
                            AddPropertySource(source);
                        }
                    }
                    return;
                }

            } catch (Exception e)
            {
                error = e;
            }

            _logger?.LogWarning("Could not locate PropertySource: " + error?.ToString());

            if (_settings.FailFast)
            {
                throw new ConfigServerException("Could not locate PropertySource, fail fast property is set, failing", error);
            }
        }

        /// <summary>
        /// Create the HttpRequestMessage that will be used in accessing the Spring Cloud Configuration server
        /// </summary>
        /// <param name="requestUri">the Uri used when accessing the server</param>
        /// <returns>The HttpRequestMessage built from the path</returns>
        internal protected virtual HttpRequestMessage GetRequestMessage(string requestUri)
        {
            return new HttpRequestMessage(HttpMethod.Get, requestUri);
        }

        /// <summary>
        /// Adds the client settings for the Configuration Server to the data dictionary
        /// </summary>
        internal protected virtual void AddConfigServerClientSettings()
        {
            Data["spring:cloud:config:enabled"] = _settings.Enabled.ToString();
            Data["spring:cloud:config:failFast"] = _settings.FailFast.ToString();
            Data["spring:cloud:config:env"] = _settings.Environment;
            Data["spring:cloud:config:label"] = _settings.Label;
            Data["spring:cloud:config:name"] = _settings.Name;
            Data["spring:cloud:config:password"] = _settings.Password;
            Data["spring:cloud:config:uri"] = _settings.Uri;
            Data["spring:cloud:config:username"] = _settings.Username;
            Data["spring:cloud:config:validate_certificates"] = _settings.ValidateCertificates.ToString();
        }

        /// <summary>
        /// Asynchronously calls the Spring Cloud Configuration Server using the provided Uri and returning a 
        /// a task that can be used to obtain the results
        /// </summary>
        /// <param name="requestUri">the Uri used in accessing the Spring Cloud Configuration Server</param>
        /// <returns>The task object representing the asynchronous operation</returns>
        internal protected virtual async Task<Environment> RemoteLoadAsync(string requestUri)
        {
            // Get the request message 
            var request = GetRequestMessage(requestUri);

#if NET451
            // If certificate validation is disabled, inject a callback to handle properly
            RemoteCertificateValidationCallback prevValidator = null;
            if (!_settings.ValidateCertificates)
            {
                prevValidator = ServicePointManager.ServerCertificateValidationCallback;
                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            }
#endif

            // Invoke config server
            try
            {
                using (HttpResponseMessage response = await _client.SendAsync(request))
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        if (response.StatusCode == HttpStatusCode.NotFound)
                            return null;

                        // Log status
                        var message = string.Format("Config Server returned status: {0} invoking path: {1}", 
                            response.StatusCode, requestUri);

                        _logger?.LogInformation(message);

                        // Throw if status >= 400 
                        if (response.StatusCode >= HttpStatusCode.BadRequest)
                            throw new HttpRequestException(message);
                        else
                            return null;
                    }

                    Stream stream = await response.Content.ReadAsStreamAsync();
                    return Deserialize(stream);
                }
            } catch (Exception e)
            {
                // Log and rethrow
                _logger?.LogError("Config Server exception: {0}, path: {1}", e, requestUri);
                throw;
            }
#if NET451
            finally
            {
                ServicePointManager.ServerCertificateValidationCallback = prevValidator;
            }
#endif

        }

        /// <summary>
        /// Deserialize the response from the Configuration Server
        /// </summary>
        /// <param name="stream">the stream representing the response from the Configuration Server</param>
        /// <returns>The Environment object representing the response from the server</returns>
        internal protected virtual Environment Deserialize(Stream stream)
        {
            try {
                using (JsonReader reader = new JsonTextReader(new StreamReader(stream)))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    return (Environment)serializer.Deserialize(reader, typeof(Environment));
                }
            } catch (Exception e)
            {
                _logger?.LogError("Config Server serialization exception", e);
            }
            return null;
        }

        /// <summary>
        /// Create the Uri that will be used in accessing the Configuration Server
        /// </summary>
        /// <returns>The request URI for the Configuration Server</returns>
        internal protected virtual string GetConfigServerUri()
        {
            var path = "/" + _settings.Name + "/" + _settings.Environment;
            if (!string.IsNullOrWhiteSpace(_settings.Label))
                path = path + "/" + _settings.Label;

            return _settings.Uri + path;
        }

        /// <summary>
        /// Adds values from a PropertySource to the Configurtation Data dictionary managed
        /// by this provider
        /// </summary>
        /// <param name="source">a property source to add</param>

        internal protected virtual void AddPropertySource(PropertySource source)
        {
            if (source == null || source.Source == null)
                return;
    
            foreach(KeyValuePair<string,object> kvp in source.Source)
            {
                try {
                    string key = kvp.Key.Replace(".", Constants.KeyDelimiter);
                    string value = kvp.Value.ToString();
                    Data[key] = value;
                } catch (Exception e)
                {
                    _logger?.LogError("Config Server exception, property: {0}={1}", kvp.Key, kvp.Value.GetType(), e);
                }

            }
        }

        /// <summary>
        /// Creates an appropriatly configured HttpClient that will be used in communicating with the
        /// Spring Cloud Configuration Server
        /// </summary>
        /// <param name="settings">the settings used in configuring the HttpClient</param>
        /// <returns>The HttpClient used by the provider</returns>
        protected static HttpClient GetHttpClient(ConfigServerClientSettingsBase settings)
        {
#if NET451
            return new HttpClient();
#else
            // TODO: For coreclr, disabling certificate validation only works on windows
            // https://github.com/dotnet/corefx/issues/4476
            if (settings != null && !settings.ValidateCertificates)
            {
                var handler = new WinHttpHandler();
                handler.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                return new HttpClient(handler);
            } else
            {
                return new HttpClient();
            }
#endif
        }

        internal IDictionary<string, string> Properties
        {
            get
            {
                return Data;
            }
        }

        internal ILogger Logger
        {
            get
            {
                return _logger;
            }
        }
    }
}
