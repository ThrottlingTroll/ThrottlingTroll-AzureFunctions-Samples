using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using StackExchange.Redis;
using ThrottlingTroll;

namespace ThrottlingTrollSampleAspNetFunction
{
    public class Functions
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConnectionMultiplexer? _redis;

        public Functions(IHttpClientFactory httpClientFactory, IConnectionMultiplexer? redis = null)
        {
            this._httpClientFactory = httpClientFactory;
            this._redis = redis;
        }

        public const string FixedWindow3RequestsPer10SecondsConfiguredViaAppSettingsRoute = "fixed-window-3-requests-per-10-seconds-configured-via-appsettings";
        /// <summary>
        /// Rate limited to 3 requests per a fixed window of 10 seconds. Configured via appsettings.json.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [Function(FixedWindow3RequestsPer10SecondsConfiguredViaAppSettingsRoute)]
        public IActionResult Test1([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            return new OkObjectResult("OK");
        }

        public const string SlidingWindow5RequestsPer15SecondsWith5BucketsConfiguredViaAppSettingsRoute = "sliding-window-5-requests-per-15-seconds-with-5-buckets-configured-via-appsettings";
        /// <summary>
        /// Rate limited to 5 requests per a sliding window of 15 seconds split into 5 buckets. Configured via appsettings.json.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [Function(SlidingWindow5RequestsPer15SecondsWith5BucketsConfiguredViaAppSettingsRoute)]
        public IActionResult Test2([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            return new OkObjectResult("OK");
        }

        public const string FixedWindow1RequestPer2SecondsConfiguredProgrammaticallyRoute = "fixed-window-1-request-per-2-seconds-configured-programmatically";
        /// <summary>
        /// Rate limited to 1 request per a fixed window of 2 seconds. Configured programmatically.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [Function(FixedWindow1RequestPer2SecondsConfiguredProgrammaticallyRoute)]
        public IActionResult Test3([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            return new OkObjectResult("OK");
        }

        public const string SlidingWindow7RequestsPer20SecondsWith4BucketsConfiguredReactivelyRoute = "sliding-window-7-requests-per-20-seconds-with-4-buckets-configured-reactively";
        /// <summary>
        /// Rate limited to 7 requests per a sliding window of 20 seconds split into 4 buckets. Configured reactively (via a callback, that's being re-executed periodically).
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [Function(SlidingWindow7RequestsPer20SecondsWith4BucketsConfiguredReactivelyRoute)]
        public IActionResult Test4([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            return new OkObjectResult("OK");
        }

        public const string FixedWindow3RequestsPer15SecondsPerEachApiKeyRoute = "fixed-window-3-requests-per-15-seconds-per-each-api-key";
        /// <summary>
        /// Rate limited to 3 requests per a fixed window of 15 seconds per each identity.
        /// Query string's 'api-key' parameter is used as identityId.
        /// Demonstrates how to use identity extractors.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [Function(FixedWindow3RequestsPer15SecondsPerEachApiKeyRoute)]
        public IActionResult Test5([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            return new OkObjectResult("OK");
        }

        public const string FixedWindow1RequestPer2SecondsResponseFabricRoute = "fixed-window-1-request-per-2-seconds-response-fabric";
        /// <summary>
        /// Rate limited to 1 request per a fixed window of 2 seconds.
        /// Custom throttled response is returned, with 400 BadRequest status code and custom body.
        /// Demonstrates how to use custom response fabrics.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="400">BadRequest</response>
        [Function(FixedWindow1RequestPer2SecondsResponseFabricRoute)]
        public IActionResult Test6([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            return new OkObjectResult("OK");
        }

        public const string FixedWindow1RequestPer2SecondsDelayedResponseRoute = "fixed-window-1-request-per-2-seconds-delayed-response";
        /// <summary>
        /// Rate limited to 1 request per a fixed window of 2 seconds.
        /// Throttled response is delayed for 3 seconds (instead of returning an error).
        /// Demonstrates how to implement a delay with a custom response fabric.
        /// </summary>
        /// <response code="200">OK</response>
        [Function(FixedWindow1RequestPer2SecondsDelayedResponseRoute)]
        public IActionResult Test7([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            return new OkObjectResult("OK");
        }

        public const string Semaphore2ConcurrentRequestsRoute = "semaphore-2-concurrent-requests";
        /// <summary>
        /// Rate limited to 2 concurrent requests.
        /// Demonstrates Semaphore (Concurrency) rate limiter.
        /// DON'T TEST IT IN BROWSER, because browsers themselves limit the number of concurrent requests to the same URL.
        /// </summary>
        /// <response code="200">OK</response>
        [Function(Semaphore2ConcurrentRequestsRoute)]
        public async Task<IActionResult> Test8([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            await Task.Delay(TimeSpan.FromSeconds(10));

            return new OkObjectResult("OK");
        }

        public const string NamedCriticalSectionRoute = "named-critical-section";
        /// <summary>
        /// Rate limited to 1 concurrent request per each identity, other requests are delayed.
        /// Demonstrates how to make a named distributed critical section with Semaphore (Concurrency) rate limiter and Identity Extractor.
        /// Query string's 'id' parameter is used as identityId.
        /// DON'T TEST IT IN BROWSER, because browsers themselves limit the number of concurrent requests to the same URL.
        /// </summary>
        /// <response code="200">OK</response>
        [Function(NamedCriticalSectionRoute)]
        public async Task<IActionResult> Test9([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            await Task.Delay(TimeSpan.FromSeconds(10));

            return new OkObjectResult("OK");
        }

        private static Dictionary<string, long> Counters = new Dictionary<string, long>();

        public const string DistributedCounterRoute = "distributed-counter";
        /// <summary>
        /// Endpoint for testing Semaphores. Increments a counter value, but NOT atomically.
        /// </summary>
        /// <response code="200">OK</response>
        [Function(DistributedCounterRoute)]
        public async Task<IActionResult> Test10([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            long counter = 1;
            string counterId = $"TestCounter{req.Query["id"]}";

            // The below code is intentionally not thread-safe

            if (this._redis != null)
            {
                var db = this._redis.GetDatabase();

                counter = (long)await db.StringGetAsync(counterId);

                counter++;

                await db.StringSetAsync(counterId, counter);
            }
            else
            {
                if (Counters.ContainsKey(counterId))
                {
                    counter = Counters[counterId];

                    counter++;
                }

                Counters[counterId] = counter;
            }

            return new OkObjectResult(counter.ToString());
        }

        public const string FixedWindow2RequestsPer4SecondsConfiguredDeclarativelyRoute = "fixed-window-2-requests-per-4-seconds-configured-declaratively";
        /// <summary>
        /// Rate limited to 2 requests per a fixed window of 4 seconds. Configured with <see cref="ThrottlingTrollAttribute"/>
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [Function(FixedWindow2RequestsPer4SecondsConfiguredDeclarativelyRoute)]
        [ThrottlingTroll(Algorithm = RateLimitAlgorithm.FixedWindow, PermitLimit = 2, IntervalInSeconds = 4, ResponseBody = "Retry in 4 seconds")]
        public IActionResult Test11([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            return new OkObjectResult("OK");
        }

        public const string FixedWindow3RequestsPer5SecondsConfiguredDeclarativelyRoute = "fixed-window-3-requests-per-5-seconds-configured-declaratively({num})";
        /// <summary>
        /// Function with parameters. Rate limited to 3 requests per a fixed window of 5 seconds. Configured with <see cref="ThrottlingTrollAttribute"/>
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [Function(FixedWindow3RequestsPer5SecondsConfiguredDeclarativelyRoute)]
        [ThrottlingTroll(Algorithm = RateLimitAlgorithm.FixedWindow, PermitLimit = 3, IntervalInSeconds = 5, ResponseBody = "Retry in 5 seconds")]
        public IActionResult Test12(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = FixedWindow3RequestsPer5SecondsConfiguredDeclarativelyRoute)] HttpRequest req, 
            int num
        )
        {
            return new OkObjectResult(num.ToString());
        }

        public const string RequestDeduplicationRoute = "request-deduplication";
        /// <summary>
        /// Demonstrates how to use request deduplication. First request with a given id will be processed, other requests with the same id will be rejected with 409 Conflict.
        /// Duplicate detection window is set to 10 seconds.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="409">Conflict</response>
        [Function(RequestDeduplicationRoute)]
        public IActionResult Test13([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            return new OkObjectResult("OK");
        }

        public const string CircuitBreaker2ErrorsPer10SecondsRoute = "circuit-breaker-2-errors-per-10-seconds";
        /// <summary>
        /// Demonstrates how to use circuit breaker. The method itself throws 50% of times.
        /// Once the limit of 2 errors per a 10 seconds interval is exceeded, the endpoint goes into Trial state.
        /// While in Trial state, only 1 request per 20 seconds is allowed to go through
        /// (the rest will be handled by ThrottlingTroll, which will return 503 Service Unavailable).
        /// Once a probe request succeeds, the endpoint goes back to normal state.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="500">Internal Server Error</response>
        /// <response code="503">Service Unavailable</response>
        [Function(CircuitBreaker2ErrorsPer10SecondsRoute)]
        public IActionResult Test14([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            if (Random.Shared.Next(0, 2) == 1)
            {
                Console.WriteLine($"{CircuitBreaker2ErrorsPer10SecondsRoute} succeeded");

                return new OkObjectResult("OK");
            }
            else
            {
                Console.WriteLine($"{CircuitBreaker2ErrorsPer10SecondsRoute} failed");

                throw new Exception("Oops, I am broken");
            }
        }

        public const string MyThrottledHttpClientName = "my-throttled-httpclient";
        public const string EgressFixedWindow2RequestsPer5SecondsConfiguredViaAppSettings = "egress-fixed-window-2-requests-per-5-seconds-configured-via-appsettings";
        /// <summary>
        /// Uses a rate-limited HttpClient to make calls to a dummy endpoint. Rate limited to 2 requests per a fixed window of 5 seconds.
        /// </summary>
        /// <response code="200">OK</response>
        [Function(EgressFixedWindow2RequestsPer5SecondsConfiguredViaAppSettings)]
        public async Task<IActionResult> EgressTest1([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            using var client = this._httpClientFactory.CreateClient(MyThrottledHttpClientName);

            // Print debug line with all headers.
            var headers = req.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());

            // Get hostname and port for the current function. This is needed to construct a URL for the dummy endpoint.           
            string url = $"{req.Scheme}://{GetHostAndPort(req)}/api/{DummyRoute}";

            var clientResponse = await client.GetAsync(url);

            return new OkObjectResult($"Dummy endpoint returned {clientResponse.StatusCode}");
        }

        public const string EgressFixedWindow3RequestsPer10SecondsConfiguredProgrammaticallyRoute = "egress-fixed-window-3-requests-per-10-seconds-configured-programmatically";
        /// <summary>
        /// Calls /fixed-window-3-requests-per-10-seconds-configured-via-appsettings endpoint 
        /// using an HttpClient that is configured to propagate 429 responses.  
        /// HttpClient configured in-place programmatically.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [Function(EgressFixedWindow3RequestsPer10SecondsConfiguredProgrammaticallyRoute)]
        public async Task<IActionResult> EgressTest2([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            // NOTE: HttpClient instances should normally be reused. Here we're creating separate instances only for the sake of simplicity.
            using var client = new HttpClient
            (
                new ThrottlingTrollHandler
                (
                    new ThrottlingTrollEgressConfig
                    {
                        PropagateToIngress = true
                    }
                )
            );

            string url = $"{req.Scheme}://{GetHostAndPort(req)}/api/{FixedWindow3RequestsPer10SecondsConfiguredViaAppSettingsRoute}";

            await client.GetAsync(url);

            return new OkObjectResult("OK");
        }

        public const string MyRetryingHttpClientName = "my-retrying-httpclient";
        public const string EgressFixedWindow3RequestsPer10SecondsWithRetriesRoute = "egress-fixed-window-3-requests-per-10-seconds-with-retries";
        /// <summary>
        /// Calls /fixed-window-3-requests-per-10-seconds-configured-via-appsettings endpoint 
        /// using an HttpClient that is configured to do retries.
        /// </summary>
        /// <response code="200">OK</response>
        [Function(EgressFixedWindow3RequestsPer10SecondsWithRetriesRoute)]
        public async Task<IActionResult> EgressTest4([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            using var client = this._httpClientFactory.CreateClient(MyRetryingHttpClientName);

            string url = $"{req.Scheme}://{GetHostAndPort(req)}/api/{FixedWindow3RequestsPer10SecondsConfiguredViaAppSettingsRoute}";

            var clientResponse = await client.GetAsync(url);

            return new OkObjectResult($"Dummy endpoint returned {clientResponse.StatusCode}");
        }

        public const string EgressFixedWindow3RequestsPer5SecondsWithDelaysRoute = "egress-fixed-window-3-requests-per-5-seconds-with-delays";
        /// <summary>
        /// Calls /dummy endpoint 
        /// using an HttpClient that is limited to 3 requests per 5 seconds and does automatic delays and retries.
        /// HttpClient configured in-place programmatically.
        /// </summary>
        /// <response code="200">OK</response>
        [Function(EgressFixedWindow3RequestsPer5SecondsWithDelaysRoute)]
        public async Task<IActionResult> EgressTest5([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            // NOTE: HttpClient instances should normally be reused. Here we're creating separate instances only for the sake of simplicity.
            using var client = new HttpClient
            (
                new ThrottlingTrollHandler
                (
                    async (limitExceededResult, httpRequestProxy, httpResponseProxy, cancellationToken) =>
                    {
                        var egressResponse = (IEgressHttpResponseProxy)httpResponseProxy;

                        egressResponse.ShouldRetry = true;
                    },

                    counterStore: null,

                    new ThrottlingTrollEgressConfig
                    {
                        Rules = new[]
                        {
                            new ThrottlingTrollRule
                            {
                                LimitMethod = new FixedWindowRateLimitMethod
                                {
                                    PermitLimit = 3,
                                    IntervalInSeconds = 5
                                }
                            }
                        }
                    }
                )
            );

            string url = $"{req.Scheme}://{GetHostAndPort(req)}/api/{DummyRoute}";

            await client.GetAsync(url);

            return new OkObjectResult("OK");
        }

        public const string EgressSemaphore2ConcurrentRequestsRoute = "egress-semaphore-2-concurrent-requests";
        /// <summary>
        /// Calls /lazy-dummy endpoint 
        /// using an HttpClient that is limited to 2 concurrent requests.
        /// Demonstrates Semaphore (Concurrency) rate limiter.
        /// DON'T TEST IT IN BROWSER, because browsers themselves limit the number of concurrent requests to the same URL.
        /// </summary>
        /// <response code="200">OK</response>
        [Function(EgressSemaphore2ConcurrentRequestsRoute)]
        public async Task<IActionResult> EgressTest6([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            // NOTE: HttpClient instances should normally be reused. Here we're creating separate instances only for the sake of simplicity.
            using var client = new HttpClient
            (
                new ThrottlingTrollHandler
                (
                    new ThrottlingTrollEgressConfig
                    {
                        Rules = new[]
                        {
                            new ThrottlingTrollRule
                            {
                                LimitMethod = new SemaphoreRateLimitMethod
                                {
                                    PermitLimit = 2
                                }
                            }
                        }
                    }
                )
            );

            string url = $"{req.Scheme}://{GetHostAndPort(req)}/api/{LazyDummyRoute}";

            var clientResponse = await client.GetAsync(url);

            return new OkObjectResult($"Dummy endpoint returned {clientResponse.StatusCode}");
        }

        public const string EgressCircuitBreaker2ErrorsPer10SecondsRoute = "egress-circuit-breaker-2-errors-per-10-seconds";
        /// <summary>
        /// Calls /semi-failing-dummy endpoint 
        /// using an HttpClient that is configured to break the circuit after receiving 2 errors within 10 seconds interval.
        /// Once broken, will execute no more than 1 request per 20 seconds, other requests will shortcut to 429.
        /// Once a request succceeds, will return to normal.
        /// </summary>
        /// <response code="200">OK</response>
        [Function(EgressCircuitBreaker2ErrorsPer10SecondsRoute)]
        public async Task<IActionResult> EgressTest7([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            // NOTE: HttpClient instances should normally be reused. Here we're creating separate instances only for the sake of simplicity.
            using var client = new HttpClient
            (
                new ThrottlingTrollHandler
                (
                    new ThrottlingTrollEgressConfig
                    {
                        Rules = new[]
                        {
                            new ThrottlingTrollRule
                            {
                                LimitMethod = new CircuitBreakerRateLimitMethod
                                {
                                    PermitLimit = 2,
                                    IntervalInSeconds = 10,
                                    TrialIntervalInSeconds = 20
                                }
                            }
                        }
                    }
                )
            );

            string url = $"{req.Scheme}://{GetHostAndPort(req)}/api/{SemiFailingDummyRoute}";

            var clientResponse = await client.GetAsync(url);

            return new OkObjectResult($"Dummy endpoint returned {clientResponse.StatusCode}");
        }

        public const string DummyRoute = "dummy";
        /// <summary>
        /// Dummy endpoint for testing HttpClient. Isn't throttled.
        /// </summary>
        /// <response code="200">OK</response>
        [Function(DummyRoute)]
        public IActionResult Dummy([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            return new OkObjectResult("OK");
        }

        public const string LazyDummyRoute = "lazy-dummy";
        /// <summary>
        /// Dummy endpoint for testing HttpClient. Sleeps for 10 seconds. Isn't throttled.
        /// </summary>
        /// <response code="200">OK</response>
        [Function(LazyDummyRoute)]
        public async Task<IActionResult> LazyDummy([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            await Task.Delay(TimeSpan.FromSeconds(10));

            return new OkObjectResult("OK");
        }

        public const string SemiFailingDummyRoute = "semi-failing-dummy";
        /// <summary>
        /// Dummy endpoint for testing Circuit Breaker. Fails 50% of times.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="500">Internal Server Error</response>
        [Function(SemiFailingDummyRoute)]
        public IActionResult SemiFailingDummy([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            if (Random.Shared.Next(0, 2) == 1)
            {
                Console.WriteLine($"{SemiFailingDummyRoute} succeeded");

                return new OkObjectResult("OK");
            }
            else
            {
                Console.WriteLine($"{SemiFailingDummyRoute} failed");

                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        public const string ThrottlingTrollConfigDebugDumpRoute = "throttling-troll-config-debug-dump";
        /// <summary>
        /// Dumps all the current effective ThrottlingTroll configuration for debugging purposes.
        /// Never do this in a real service.
        /// </summary>
        [Function(ThrottlingTrollConfigDebugDumpRoute)]
        public IActionResult ThrottlingTrollConfigDebugDump([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            // ThrottlingTroll places a list of ThrottlingTrollConfigs into request's context under the "ThrottlingTrollConfigsContextKey" key
            // The value is a list, because there might be multiple instances of ThrottlingTrollMiddleware configured
            var configList = req.HttpContext.GetThrottlingTrollConfig();

            return new OkObjectResult(configList);
        }

        private string GetHostAndPort(HttpRequest req)
        {
            // Use the X-Forwarded-Host header if present, otherwise use the Host header.
            if (req.Headers.TryGetValue("X-Forwarded-Host", out var hostAndPort))
            {
                return hostAndPort;
            }
            else
            {
                return req.Host.Value;
            }
        }
    }
}
