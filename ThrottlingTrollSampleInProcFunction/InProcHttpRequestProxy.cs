using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using ThrottlingTroll;

namespace ThrottlingTrollSampleInProcFunction
{
    /// <summary>
    /// Converts InProc Function's <see cref="HttpRequest"/> into ThrottlingTroll's <see cref="IHttpRequestProxy"/>
    /// </summary>
    class InProcHttpRequestProxy : IHttpRequestProxy
    {
        public HttpRequest Request { get; private set; }

        public string Uri => $"{this.Request.Scheme}://{this.Request.Host}{this.Request.Path}{this.Request.QueryString}";

        public string UriWithoutQueryString => $"{this.Request.Scheme}://{this.Request.Host}{this.Request.Path}";

        public string Method => this.Request.Method;

        public IDictionary<string, StringValues> Headers => this.Request.Headers;

        public IDictionary<object, object> RequestContextItems => this.Request.HttpContext.Items;

        public InProcHttpRequestProxy(HttpRequest request)
        {
            this.Request = request;
        }

        public void AppendToContextItem<T>(string key, List<T> list)
        {
            this.Request.HttpContext.Items.TryAdd(key, new List<T>());
            ((List<T>)this.Request.HttpContext.Items[key]).AddRange(list);
        }
    }
}
