using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ThrottlingTroll;
using ThrottlingTroll.CounterStores.Redis;

[assembly: WebJobsStartup(typeof(ThrottlingTrollSampleInProcFunction.Startup.Startup))]
namespace ThrottlingTrollSampleInProcFunction.Startup
{
    public class Startup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            var services = builder.Services;

            // If RedisConnectionString is specified, then using RedisCounterStore.
            // Otherwise the default MemoryCacheCounterStore will be used.
            var redisConnString = Environment.GetEnvironmentVariable("RedisConnectionString");
            if (!string.IsNullOrEmpty(redisConnString))
            {
                var redis = ConnectionMultiplexer.Connect(redisConnString);

                services.AddSingleton<IConnectionMultiplexer>(redis);

                services.AddSingleton<ICounterStore>(new RedisCounterStore(redis));
            }

            // <ThrottlingTroll Egress Configuration>

            // Configuring a named HttpClient for egress throttling. Rules and limits taken from host.json
            services.AddHttpClient(Functions.MyThrottledHttpClientName).AddThrottlingTrollMessageHandler((ServiceProvider, options) =>
            {
                // In InProc Functions config sections loaded from host.json have the "AzureFunctionsJobHost:" prefix in their names
                options.ConfigSectionName = "AzureFunctionsJobHost:ThrottlingTrollEgress";                
            });

            // Configuring a named HttpClient that does automatic retries with respect to Retry-After response header
            services.AddHttpClient(Functions.MyRetryingHttpClientName).AddThrottlingTrollMessageHandler((serviceProvider, options) =>
            {
                options.ResponseFabric = async (checkResults, requestProxy, responseProxy, cancelToken) =>
                {
                    var egressResponse = (IEgressHttpResponseProxy)responseProxy;

                    egressResponse.ShouldRetry = true;
                };
            });

            // </ThrottlingTroll Egress Configuration>


            // <ThrottlingTroll Ingress Configuration>

            // This method will read static configuration from appsettings.json and merge it with all the programmatically configured rules below
            builder.Services.AddThrottlingTroll(options =>
            {
                // In InProc Functions config sections loaded from host.json have the "AzureFunctionsJobHost:" prefix in their names
                options.ConfigSectionName = "AzureFunctionsJobHost:ThrottlingTrollIngress";

                options.Config = new ThrottlingTrollConfig
                {
                    // Specifying UniqueName is needed when multiple services store their
                    // rate limit counters in the same cache instance, to prevent those services
                    // from corrupting each other's counters. Otherwise you can skip it.
                    UniqueName = "MyThrottledService1",

                    Rules = new[]
                    {

                        // Static programmatic configuration
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{Functions.FixedWindow1RequestPer2SecondsConfiguredProgrammaticallyRoute}",
                            LimitMethod = new FixedWindowRateLimitMethod
                            {
                                PermitLimit = 1,
                                IntervalInSeconds = 2
                            }
                        },


                        // Demonstrates how to delay the response instead of returning 429
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{Functions.FixedWindow1RequestPer2SecondsDelayedResponseRoute}",
                            LimitMethod = new FixedWindowRateLimitMethod
                            {
                                PermitLimit = 1,
                                IntervalInSeconds = 2
                            },

                            // Custom response fabric, impedes the normal response for 3 seconds
                            ResponseFabric = async (checkResults, requestProxy, responseProxy, requestAborted) =>
                            {
                                await Task.Delay(TimeSpan.FromSeconds(3));

                                var ingressResponse = (IIngressHttpResponseProxy)responseProxy;
                                ingressResponse.ShouldContinueAsNormal = true;
                            }
                        },


                        // Demonstrates how to use identity extractors
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{Functions.FixedWindow3RequestsPer15SecondsPerEachApiKeyRoute}",
                            LimitMethod = new FixedWindowRateLimitMethod
                            {
                                PermitLimit = 3,
                                IntervalInSeconds = 15
                            },

                            IdentityIdExtractor = request =>
                            {
                                // Identifying clients by their api-key
                                request.Query.TryGetValue("api-key", out var key);
                                return key;
                            }
                        },


                        // Demonstrates Semaphore (Concurrency) rate limiter
                        // DON'T TEST IT IN BROWSER, because browsers themselves limit the number of concurrent requests to the same URL.
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{Functions.Semaphore2ConcurrentRequestsRoute}",
                            LimitMethod = new SemaphoreRateLimitMethod
                            {
                                PermitLimit = 2
                            }
                        },


                        /// Demonstrates how to make a named distributed critical section with Semaphore (Concurrency) rate limiter and Identity Extractor.
                        /// Query string's 'id' parameter is used as identityId.
                        // DON'T TEST IT IN BROWSER, because browsers themselves limit the number of concurrent requests to the same URL.
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{Functions.NamedCriticalSectionRoute}",
                            LimitMethod = new SemaphoreRateLimitMethod
                            {
                                PermitLimit = 1
                            },

                            // This must be set to something > 0 for responses to be automatically delayed
                            MaxDelayInSeconds = 120,

                            IdentityIdExtractor = request =>
                            {
                                // Identifying clients by their id
                                request.Query.TryGetValue("id", out var id);
                                return id;
                            }
                        },


                        // Demonstrates how to make a distributed counter with SemaphoreRateLimitMethod
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{Functions.DistributedCounterRoute}",
                            LimitMethod = new SemaphoreRateLimitMethod
                            {
                                PermitLimit = 1
                            },

                            // This must be set to something > 0 for responses to be automatically delayed
                            MaxDelayInSeconds = 120,

                            IdentityIdExtractor = request =>
                            {
                                // Identifying counters by their id
                                request.Query.TryGetValue("id", out var id);
                                return id;
                            }
                        },


                        // Demonstrates how to use request deduplication
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{Functions.RequestDeduplicationRoute}",

                            LimitMethod = new SemaphoreRateLimitMethod
                            {
                                PermitLimit = 1,
                                ReleaseAfterSeconds = 10
                            },

                            // Using "id" query string param to identify requests
                            IdentityIdExtractor = request =>
                            {
                                request.Query.TryGetValue("id", out var id);
                                return id;
                            },

                            // Returning 409 Conflict for duplicate requests
                            ResponseFabric = async (checkResults, requestProxy, responseProxy, requestAborted) =>
                            {
                                responseProxy.StatusCode = (int)HttpStatusCode.Conflict;

                                await responseProxy.WriteAsync("Duplicate request detected");
                            }
                        },

                        // Demonstrates how to use circuit breaker
                        new ThrottlingTrollRule
                        {
                            UriPattern = $"/{Functions.CircuitBreaker2ErrorsPer10SecondsRoute}",

                            LimitMethod = new CircuitBreakerRateLimitMethod
                            {
                                PermitLimit = 2,
                                IntervalInSeconds = 10,
                                TrialIntervalInSeconds = 20
                            }
                        }
                    }
                };


                // Reactive programmatic configuration. Allows to adjust rules and limits without restarting the service.
                
                options.GetConfigFunc = async () =>
                {
                    // Loading settings from a custom file. You can instead load them from a database
                    // or from anywhere else.

                    string ruleFileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", "my-dynamic-throttling-rule.json");

                    string ruleJson = await File.ReadAllTextAsync(ruleFileName);

                    var rule = JsonSerializer.Deserialize<ThrottlingTrollRule>(ruleJson);

                    return new ThrottlingTrollConfig
                    {
                        Rules = new[] { rule }
                    };
                };

                // The above function will be periodically called every 5 seconds
                options.IntervalToReloadConfigInSeconds = 5;
            });

            // </ThrottlingTroll Ingress Configuration>
        }
    }
}
