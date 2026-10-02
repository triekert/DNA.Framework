using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Dna
{
    public static class HttpResponseMessageExtensions
    {
        /// <summary>
        /// Creates a <see cref="WebRequestResult{TResponse}"/> from an <see cref="HttpResponseMessage"/>
        /// </summary>
        /// <typeparam name="TResponse">The expected response type</typeparam>
        /// <param name="response">The HTTP response message</param>
        /// <returns></returns>
        public static async Task<WebRequestResult<TResponse>> CreateWebRequestResultAsync<TResponse>(this HttpResponseMessage response)
        {
            var result = new WebRequestResult<TResponse>
            {
                // Map status code
                StatusCode = response.StatusCode,

                // Map headers if needed
                ContentType = response.Content?.Headers.ContentType?.MediaType
            };

            // Read the raw content string asynchronously
            if (response.Content != null)
            {
                result.RawServerResponse = await response.Content.ReadAsStringAsync();
            }

            return result;
        }
    }
}