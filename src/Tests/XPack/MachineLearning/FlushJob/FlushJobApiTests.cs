﻿using System;
using System.Threading;
using Elasticsearch.Net;
using FluentAssertions;
using Nest;
using Tests.Framework;
using Tests.Framework.Integration;
using Tests.Framework.ManagedElasticsearch.Clusters;

namespace Tests.XPack.MachineLearning.FlushJob
{
	public class FlushJobApiTests : ApiIntegrationTestBase<XPackMachineLearningCluster, IFlushJobResponse, IFlushJobRequest, FlushJobDescriptor, FlushJobRequest>
	{
		public FlushJobApiTests(XPackMachineLearningCluster cluster, EndpointUsage usage) : base(cluster, usage) { }

		protected override void IntegrationSetup(IElasticClient client, CallUniqueValues values)
		{
			// TODO: create a job with data, to allow it to be flushed

		}

		protected override LazyResponses ClientUsage() => Calls(
			fluent: (client, f) => client.FlushJob(CallIsolatedValue, f),
			fluentAsync: (client, f) => client.FlushJobAsync(CallIsolatedValue, f),
			request: (client, r) => client.FlushJob(r),
			requestAsync: (client, r) => client.FlushJobAsync(r)
		);

		protected override bool ExpectIsValid => true;
		protected override int ExpectStatusCode => 200;
		protected override HttpMethod HttpMethod => HttpMethod.POST;

		protected override string UrlPath => $"_xpack/ml/anomaly_detectors/{CallIsolatedValue}/_flush";

		protected override bool SupportsDeserialization => true;

		protected override FlushJobDescriptor NewDescriptor() => new FlushJobDescriptor(CallIsolatedValue);

		protected override object ExpectJson => null;

		protected override Func<FlushJobDescriptor, IFlushJobRequest> Fluent => f => f;

		protected override FlushJobRequest Initializer =>
			new FlushJobRequest(CallIsolatedValue);

		protected override void ExpectResponse(IFlushJobResponse response)
		{
			response.Flushed.Should().BeTrue();
		}
	}
}