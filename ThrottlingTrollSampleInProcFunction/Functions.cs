using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ThrottlingTroll;

namespace ThrottlingTrollSampleInProcFunction
{
    public class Functions
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConnectionMultiplexer _redis;
        private readonly IThrottlingTroll _thtr;

        public Functions(IThrottlingTroll thtr, IHttpClientFactory httpClientFactory, IConnectionMultiplexer redis = null)
        {
            this._thtr = thtr;
            this._httpClientFactory = httpClientFactory;
            this._redis = redis;
        }

        public const string FixedWindow3RequestsPer10SecondsConfiguredViaAppSettingsRoute = "fixed-window-3-requests-per-10-seconds-configured-via-appsettings";
        /// <summary>
        /// Rate limited to 3 requests per a fixed window of 10 seconds. Configured via appsettings.json.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [FunctionName(FixedWindow3RequestsPer10SecondsConfiguredViaAppSettingsRoute)]
        public Task<IActionResult> Test1([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
            => this._thtr.WithThrottlingTroll(
                new InProcHttpRequestProxy(req),
                async ctx =>
                {
                    // Here is how to set a custom header with the number of remaining requests
                    // Obtaining the current list of limit check results from HttpContext.Items
                    var limitCheckResults = ctx.LimitCheckResults;

                    // Now finding the minimal RequestsRemaining number (since there can be multiple rules matched)
                    var minRequestsRemaining = limitCheckResults.OrderByDescending(r => r.RequestsRemaining).FirstOrDefault();
                    if (minRequestsRemaining != null)
                    {
                        // Now setting the custom header
                        req.HttpContext.Response.Headers.Add("X-Requests-Remaining", minRequestsRemaining.RequestsRemaining.ToString());
                    }

                    return (IActionResult)new OkObjectResult("OK");
                },
                async ctx =>
                {
                    return (IActionResult)new StatusCodeResult((int)HttpStatusCode.TooManyRequests);
                });

        public const string SlidingWindow5RequestsPer15SecondsWith5BucketsConfiguredViaAppSettingsRoute = "sliding-window-5-requests-per-15-seconds-with-5-buckets-configured-via-appsettings";
        /// <summary>
        /// Rate limited to 5 requests per a sliding window of 15 seconds split into 5 buckets. Configured via appsettings.json.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [FunctionName(SlidingWindow5RequestsPer15SecondsWith5BucketsConfiguredViaAppSettingsRoute)]
        public Task<IActionResult> Test2([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
            => this._thtr.WithThrottlingTroll(
                new InProcHttpRequestProxy(req),
                ctx => Task.FromResult<IActionResult>(new OkObjectResult("OK")),
                ctx => Task.FromResult<IActionResult>(new StatusCodeResult((int)HttpStatusCode.TooManyRequests)));

        public const string FixedWindow1RequestPer2SecondsConfiguredProgrammaticallyRoute = "fixed-window-1-request-per-2-seconds-configured-programmatically";
        /// <summary>
        /// Rate limited to 1 request per a fixed window of 2 seconds. Configured programmatically.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [FunctionName(FixedWindow1RequestPer2SecondsConfiguredProgrammaticallyRoute)]
        public Task<IActionResult> Test3([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
            => this._thtr.WithThrottlingTroll(
                new InProcHttpRequestProxy(req),
                ctx => Task.FromResult<IActionResult>(new OkObjectResult("OK")),
                ctx => Task.FromResult<IActionResult>(new StatusCodeResult((int)HttpStatusCode.TooManyRequests)));

        public const string SlidingWindow7RequestsPer20SecondsWith4BucketsConfiguredReactively = "sliding-window-7-requests-per-20-seconds-with-4-buckets-configured-reactively";
        /// <summary>
        /// Rate limited to 7 requests per a sliding window of 20 seconds split into 4 buckets. Configured reactively (via a callback, that's being re-executed periodically).
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [FunctionName(SlidingWindow7RequestsPer20SecondsWith4BucketsConfiguredReactively)]
        public Task<IActionResult> Test4([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
            => this._thtr.WithThrottlingTroll(
                new InProcHttpRequestProxy(req),
                ctx => Task.FromResult<IActionResult>(new OkObjectResult("OK")),
                ctx => Task.FromResult<IActionResult>(new StatusCodeResult((int)HttpStatusCode.TooManyRequests)));

        public const string FixedWindow3RequestsPer15SecondsPerEachApiKeyRoute = "fixed-window-3-requests-per-15-seconds-per-each-api-key";
        /// <summary>
        /// Rate limited to 3 requests per a fixed window of 15 seconds per each identity.
        /// Query string's 'api-key' parameter is used as identityId.
        /// Demonstrates how to use identity extractors.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="429">TooManyRequests</response>
        [FunctionName(FixedWindow3RequestsPer15SecondsPerEachApiKeyRoute)]
        public Task<IActionResult> Test5([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
            => this._thtr.WithThrottlingTroll(
                new InProcHttpRequestProxy(req),
                ctx => Task.FromResult<IActionResult>(new OkObjectResult("OK")),
                ctx => Task.FromResult<IActionResult>(new StatusCodeResult((int)HttpStatusCode.TooManyRequests)));

        public const string FixedWindow1RequestPer2SecondsDelayedResponseRoute = "fixed-window-1-request-per-2-seconds-delayed-response";
        /// <summary>
        /// Rate limited to 1 request per a fixed window of 2 seconds.
        /// Throttled response is delayed for 3 seconds (instead of returning an error).
        /// Demonstrates how to implement a delay with a custom response fabric.
        /// </summary>
        /// <response code="200">OK</response>
        [FunctionName(FixedWindow1RequestPer2SecondsDelayedResponseRoute)]
        public Task<IActionResult> Test7([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
            => this._thtr.WithThrottlingTroll(
                new InProcHttpRequestProxy(req),
                ctx => Task.FromResult<IActionResult>(new OkObjectResult("OK")),
                ctx => Task.FromResult<IActionResult>(new StatusCodeResult((int)HttpStatusCode.TooManyRequests)));

        public const string Semaphore2ConcurrentRequestsRoute = "semaphore-2-concurrent-requests";
        /// <summary>
        /// Rate limited to 2 concurrent requests.
        /// Demonstrates Semaphore (Concurrency) rate limiter.
        /// DON'T TEST IT IN BROWSER, because browsers themselves limit the number of concurrent requests to the same URL.
        /// </summary>
        /// <response code="200">OK</response>
        [FunctionName(Semaphore2ConcurrentRequestsRoute)]
        public Task<IActionResult> Test8([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
            => this._thtr.WithThrottlingTroll(
                new InProcHttpRequestProxy(req),
                async ctx => 
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));

                    return (IActionResult)new OkObjectResult("OK");
                },
                ctx => Task.FromResult<IActionResult>(new StatusCodeResult((int)HttpStatusCode.TooManyRequests)));

        public const string NamedCriticalSectionRoute = "named-critical-section";
        /// <summary>
        /// Rate limited to 1 concurrent request per each identity, other requests are delayed.
        /// Demonstrates how to make a named distributed critical section with Semaphore (Concurrency) rate limiter and Identity Extractor.
        /// Query string's 'id' parameter is used as identityId.
        /// DON'T TEST IT IN BROWSER, because browsers themselves limit the number of concurrent requests to the same URL.
        /// </summary>
        /// <response code="200">OK</response>
        [FunctionName(NamedCriticalSectionRoute)]
        public Task<IActionResult> Test9([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
            => this._thtr.WithThrottlingTroll(
                new InProcHttpRequestProxy(req),
                async ctx =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));

                    return (IActionResult)new OkObjectResult("OK");
                },
                ctx => Task.FromResult<IActionResult>(new StatusCodeResult((int)HttpStatusCode.TooManyRequests)));

        private static Dictionary<string, long> Counters = new Dictionary<string, long>();

        public const string DistributedCounterRoute = "distributed-counter";
        /// <summary>
        /// Endpoint for testing Semaphores. Increments a counter value, but NOT atomically.
        /// </summary>
        /// <response code="200">OK</response>
        [FunctionName(DistributedCounterRoute)]
        public Task<IActionResult> Test10([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
            => this._thtr.WithThrottlingTroll(
                new InProcHttpRequestProxy(req),
                async ctx =>
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

                    return (IActionResult)new OkObjectResult("OK");
                },
                ctx => Task.FromResult<IActionResult>(new StatusCodeResult((int)HttpStatusCode.TooManyRequests)));

        public const string RequestDeduplicationRoute = "request-deduplication";
        /// <summary>
        /// Demonstrates how to use request deduplication. First request with a given id will be processed, other requests with the same id will be rejected with 409 Conflict.
        /// Duplicate detection window is set to 10 seconds.
        /// </summary>
        /// <response code="200">OK</response>
        /// <response code="409">Conflict</response>
        [FunctionName(RequestDeduplicationRoute)]
        public Task<IActionResult> Test13([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
            => this._thtr.WithThrottlingTroll(
                new InProcHttpRequestProxy(req),
                ctx => Task.FromResult<IActionResult>(new OkObjectResult("OK")),
                ctx => Task.FromResult<IActionResult>(new StatusCodeResult((int)HttpStatusCode.TooManyRequests)));

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
        [FunctionName(CircuitBreaker2ErrorsPer10SecondsRoute)]
        public Task<IActionResult> Test14([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
            => this._thtr.WithThrottlingTroll(
                new InProcHttpRequestProxy(req),
                async ctx =>
                {
                    if (Random.Shared.Next(0, 2) == 1)
                    {
                        Console.WriteLine($"{CircuitBreaker2ErrorsPer10SecondsRoute} succeeded");

                        return (IActionResult)new OkObjectResult("OK");
                    }
                    else
                    {
                        Console.WriteLine($"{CircuitBreaker2ErrorsPer10SecondsRoute} failed");

                        throw new Exception("Oops, I am broken");
                    }
                },
                ctx => Task.FromResult<IActionResult>(new StatusCodeResult((int)HttpStatusCode.ServiceUnavailable)));

        public const string MyThrottledHttpClientName = "my-throttled-httpclient";
        public const string EgressFixedWindow2RequestsPer5SecondsConfiguredViaAppSettingsRoute = "egress-fixed-window-2-requests-per-5-seconds-configured-via-appsettings";
        /// <summary>
        /// Uses a rate-limited HttpClient to make calls to a dummy endpoint. Rate limited to 2 requests per a fixed window of 5 seconds.
        /// </summary>
        /// <response code="200">OK</response>
        [FunctionName(EgressFixedWindow2RequestsPer5SecondsConfiguredViaAppSettingsRoute)]
        public async Task<IActionResult> EgressTest1([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            using var client = this._httpClientFactory.CreateClient(MyThrottledHttpClientName);

            string url = $"{req.Scheme}://{req.Host}/api/{DummyRoute}";

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
        [FunctionName(EgressFixedWindow3RequestsPer10SecondsConfiguredProgrammaticallyRoute)]
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

            string url = $"{req.Scheme}://{req.Host}/api/{FixedWindow3RequestsPer10SecondsConfiguredViaAppSettingsRoute}";

            try
            {
                await client.GetAsync(url);
            }
            catch (ThrottlingTrollTooManyRequestsException ex)
            {
                return new StatusCodeResult((int)HttpStatusCode.TooManyRequests);
            }

            return new OkObjectResult("OK");
        }

        public const string MyRetryingHttpClientName = "my-retrying-httpclient";
        public const string EgressFixedWindow3RequestsPer10SecondsWithRetriesRoute = "egress-fixed-window-3-requests-per-10-seconds-with-retries";
        /// <summary>
        /// Calls /fixed-window-3-requests-per-10-seconds-configured-via-appsettings endpoint 
        /// using an HttpClient that is configured to do retries.
        /// </summary>
        /// <response code="200">OK</response>
        [FunctionName(EgressFixedWindow3RequestsPer10SecondsWithRetriesRoute)]
        public async Task<IActionResult> EgressTest4([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            using var client = this._httpClientFactory.CreateClient(MyRetryingHttpClientName);

            string url = $"{req.Scheme}://{req.Host}/api/{FixedWindow3RequestsPer10SecondsConfiguredViaAppSettingsRoute}";

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
        [FunctionName(EgressFixedWindow3RequestsPer5SecondsWithDelaysRoute)]
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

            string url = $"{req.Scheme}://{req.Host}/api/{DummyRoute}";

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
        [FunctionName(EgressSemaphore2ConcurrentRequestsRoute)]
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

            string url = $"{req.Scheme}://{req.Host}/api/{LazyDummyRoute}";

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
        [FunctionName(EgressCircuitBreaker2ErrorsPer10SecondsRoute)]
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

            string url = $"{req.Scheme}://{req.Host}/api/{SemiFailingDummyRoute}";

            var clientResponse = await client.GetAsync(url);

            return new OkObjectResult($"Dummy endpoint returned {clientResponse.StatusCode}");
        }

        public const string DummyRoute = "dummy";
        /// <summary>
        /// Dummy endpoint for testing HttpClient. Isn't throttled.
        /// </summary>
        /// <response code="200">OK</response>
        [FunctionName(DummyRoute)]
        public IActionResult Dummy([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            return new OkObjectResult("OK");
        }

        public const string LazyDummyRoute = "lazy-dummy";
        /// <summary>
        /// Dummy endpoint for testing HttpClient. Sleeps for 10 seconds. Isn't throttled.
        /// </summary>
        /// <response code="200">OK</response>
        [FunctionName(LazyDummyRoute)]
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
        [FunctionName(SemiFailingDummyRoute)]
        public IActionResult SemiFailingDummy([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            if (Random.Shared.Next(0, 2) == 1)
            {
                Console.WriteLine($"{SemiFailingDummyRoute} succeeded");

                return new OkObjectResult("OK");
            }
            else
            {
                Console.WriteLine($"{SemiFailingDummyRoute} failed");

                return new StatusCodeResult((int)HttpStatusCode.InternalServerError);
            }
        }

        public const string ThrottlingTrollConfigDebugDumpRoute = "throttling-troll-config-debug-dump";
        /// <summary>
        /// Dumps all the current effective ThrottlingTroll configuration for debugging purposes.
        /// Never do this in a real service.
        /// </summary>
        [FunctionName(ThrottlingTrollConfigDebugDumpRoute)]
        public async Task<IActionResult> ThrottlingTrollConfigDebugDump([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            var configList = await this._thtr.GetCurrentConfig();

            req.HttpContext.Response.Headers.Add("Content-Type", "application/json");

            return new OkObjectResult(JsonSerializer.Serialize(configList));
        }
    }
}
