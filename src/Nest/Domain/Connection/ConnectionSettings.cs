﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Nest.Resolvers;
using System;
using System.Collections.Specialized;
using Newtonsoft.Json;

namespace Nest
{
	/// <summary>
	/// Control how NEST's behaviour.
	/// </summary>
	public class ConnectionSettings : IConnectionSettings
	{
		private string _defaultIndex;
		public string DefaultIndex
		{
			get
			{
				if (this._defaultIndex.IsNullOrEmpty())
					throw new NullReferenceException("No default index set on connection!");
				return this._defaultIndex;
			}
			private set { this._defaultIndex = value; }
		}
		public Uri Uri { get; private set; }
		public string Host { get; private set; }
		public int Port { get; private set; }
		public int Timeout { get; private set; }
		public string ProxyUsername { get; private set; }
		public string ProxyPassword { get; private set; }
		public string ProxyAddress { get; private set; }

		public int MaximumAsyncConnections { get; private set; }
		public bool UsesPrettyResponses { get; private set; }
		public bool TraceEnabled { get; private set; }

		public Func<Type, string> DefaultTypeNameInferrer { get; private set; }
		public Action<ConnectionStatus> ConnectionStatusHandler { get; private set; }
		public FluentDictionary<Type, string> DefaultIndices { get; private set; }
		public FluentDictionary<Type, string> DefaultTypeNames { get; private set; }
		public NameValueCollection QueryStringParameters { get; private set; }
		public Func<string, string> DefaultPropertyNameInferrer { get; private set; }

		//these are set once to make sure we don't query the Uri too often
		public bool UriSpecifiedBasicAuth { get; private set; }
			 
		//Serializer settings
		public Action<JsonSerializerSettings> ModifyJsonSerializerSettings { get; private set; }

		public ReadOnlyCollection<Func<Type, JsonConverter>> ContractConverters { get; private set; }

		public ConnectionSettings(Uri uri, string defaultIndex)
		{
			uri.ThrowIfNull("uri");
			defaultIndex.ThrowIfNullOrEmpty("defaultIndex");

			this.Timeout = 60 * 1000;

			this.SetDefaultIndex(defaultIndex);
			this.Uri = uri;
			
			
			if (!uri.OriginalString.EndsWith("/"))
				this.Uri = new Uri(uri.OriginalString + "/");
			this.Host = uri.Host;
			this.Port = uri.Port;
			this.UriSpecifiedBasicAuth = !uri.UserInfo.IsNullOrEmpty();

			this.MaximumAsyncConnections = 0;
			this.DefaultTypeNameInferrer = (t => t.Name.ToLower()); 
			this.DefaultIndices = new FluentDictionary<Type, string>();
			this.DefaultTypeNames = new FluentDictionary<Type, string>();
			this.ConnectionStatusHandler = this.ConnectionStatusDefaultHandler;

			this.ModifyJsonSerializerSettings = (j) => { };
			this.ContractConverters = Enumerable.Empty<Func<Type, JsonConverter>>().ToList().AsReadOnly();


		}

		/// <summary>
		/// Enable Trace signals to the IConnection that it should put debug information on the Trace.
		/// </summary>
		public ConnectionSettings EnableTrace(bool enabled = true)
		{
			this.TraceEnabled = enabled;
			return this;
		}

		/// <summary>
		/// This calls SetDefaultTypenameInferrer with an implementation that will pluralize type names. This used to be the default prior to Nest 0.90
		/// </summary>
		public ConnectionSettings PluralizeTypeNames()
		{
			this.DefaultTypeNameInferrer = this.LowerCaseAndPluralizeTypeNameInferrer;
			return this;
		}

		/// <summary>
		/// Allows you to update internal the json.net serializer settings to your liking
		/// </summary>
		public ConnectionSettings SetJsonSerializerSettingsModifier(Action<JsonSerializerSettings> modifier)
		{
			if (modifier == null)
				return this;
			this.ModifyJsonSerializerSettings = modifier;
			return this;

		}
		/// <summary>
		/// Add a custom JsonConverter to the build in json serialization by passing in a predicate for a type.
		/// This is faster then adding them using AddJsonConverters() because this way they will be part of the cached 
		/// Json.net contract for a type.
		/// </summary>
		public ConnectionSettings AddContractJsonConverters(params Func<Type, JsonConverter>[] contractSelectors)
		{
			this.ContractConverters = contractSelectors.ToList().AsReadOnly();
			return this;
		}

		/// <summary>
		/// This NameValueCollection will be appended to every url NEST calls, great if you need to pass i.e an API key.
		/// </summary>
		public ConnectionSettings SetGlobalQueryStringParameters(NameValueCollection queryStringParameters)
		{
			if (this.QueryStringParameters != null)
			{
				this.QueryStringParameters.Add(queryStringParameters);
			}
			this.QueryStringParameters = queryStringParameters;
			return this;
		}

		/// <summary>
		/// Timeout in milliseconds when the .NET webrquest should abort the request, note that you can set this to a high value here,
		/// and specify the timeout in various calls on Elasticsearch's side.
		/// </summary>
		/// <param name="timeout">time out in milliseconds</param>
		public ConnectionSettings SetTimeout(int timeout)
		{
			this.Timeout = timeout;
			return this;
		}
		/// <summary>
		/// Index to default to when no index is specified.
		/// </summary>
		/// <param name="defaultIndex">When null/empty/not set might throw NRE later on
		/// when not specifying index explicitly while indexing.
		/// </param>
		public ConnectionSettings SetDefaultIndex(string defaultIndex)
		{
			this.DefaultIndex = defaultIndex;
			return this;
		}
		/// <summary>
		/// Semaphore asynchronous connections automatically by giving
		/// it a maximum concurrent connections. 
		/// </summary>
		/// <param name="maximum">defaults to 0 (unbounded)</param>
		public ConnectionSettings SetMaximumAsyncConnections(int maximum)
		{
			this.MaximumAsyncConnections = maximum;
			return this;
		}

		/// <summary>
		/// If your connection has to go through proxy use this method to specify the proxy url
		/// </summary>
		public ConnectionSettings SetProxy(Uri proxyAdress, string username, string password)
		{
			proxyAdress.ThrowIfNull("proxyAdress");
			this.ProxyAddress = proxyAdress.ToString();
			this.ProxyUsername = username;
			this.ProxyPassword = password;
			return this;
		}

		/// <summary>
		/// Append ?pretty=true to requests, this helps to debug send and received json.
		/// </summary>
		public ConnectionSettings UsePrettyResponses(bool b = true)
		{
			this.UsesPrettyResponses = b;
			this.SetGlobalQueryStringParameters(new NameValueCollection { { "pretty", b.ToString().ToLowerInvariant() } });
			return this;
		}

		private string LowerCaseAndPluralizeTypeNameInferrer(Type type)
		{
			type.ThrowIfNull("type");
			return Inflector.MakePlural(type.Name).ToLower();
		}

		private void ConnectionStatusDefaultHandler(ConnectionStatus status)
		{
			return;
		}

		/// <summary>
		/// By default NEST camelCases property names (EmailAddress => emailAddress) that do not have an explicit propertyname 
		/// either via an ElasticProperty attribute or because they are part of Dictionary where the keys should be treated verbatim.
		/// <pre>
		/// Here you can register a function that transforms propertynames (default casing, pre- or suffixing)
		/// </pre>
		/// </summary>
		public ConnectionSettings SetDefaultPropertyNameInferrer(Func<string, string> propertyNameSelector)
		{
			this.DefaultPropertyNameInferrer = propertyNameSelector;
			return this;
		}

		/// <summary>
		/// Allows you to override how type names should be reprented, the default will call .ToLowerInvariant() on the type's name.
		/// </summary>
		public ConnectionSettings SetDefaultTypeNameInferrer(Func<Type, string> defaultTypeNameInferrer)
		{
			defaultTypeNameInferrer.ThrowIfNull("defaultTypeNameInferrer");
			this.DefaultTypeNameInferrer = defaultTypeNameInferrer;
			return this;
		}
		
		/// <summary>
		/// Global callback for every response that NEST receives, useful for custom logging.
		/// </summary>
		public ConnectionSettings SetConnectionStatusHandler(Action<ConnectionStatus> handler)
		{
			handler.ThrowIfNull("handler");
			this.ConnectionStatusHandler = handler;
			return this;
		}
		/// <summary>
		/// Map types to a index names. Takes precedence over SetDefaultIndex().
		/// </summary>
		public ConnectionSettings MapDefaultTypeIndices(Action<FluentDictionary<Type, string>> mappingSelector)
		{
			mappingSelector.ThrowIfNull("mappingSelector");
			mappingSelector(this.DefaultIndices);
			return this;
		}
		/// <summary>
		/// Allows you to override typenames, takes priority over the global SetDefaultTypeNameInferrer()
		/// </summary>
		public ConnectionSettings MapDefaultTypeNames(Action<FluentDictionary<Type, string>> mappingSelector)
		{
			mappingSelector.ThrowIfNull("mappingSelector");
			mappingSelector(this.DefaultTypeNames);
			return this;
		}
	}
}
