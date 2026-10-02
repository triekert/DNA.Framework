using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Dna
{
    /// <summary>
    /// Provides HTTP calls for sending and receiving information from a HTTP server
    /// </summary>
    public static class WebRequests
    {
        /// <summary>
        /// Shared HttpClient instance to manage underlying TCP connections efficiently
        /// </summary>
        private static readonly HttpClient Client = new HttpClient();

        /// <summary>
        /// GETs a web request to a URL and returns the raw http web response
        /// </summary>
        /// <param name="url">The URL</param>
        /// <param name="configureRequest">Allows caller to customize and configure the HttpRequestMessage prior to sending</param>
        /// <param name="bearerToken">If specified, provides the Authorization header with JWT bearer token</param>
        public static async Task<HttpResponseMessage> GetAsync(string url, Action<HttpRequestMessage> configureRequest = null, string bearerToken = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrEmpty(bearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            configureRequest?.Invoke(request);

            // Send request without throwing on HTTP status errors (so 401/404 can be inspected)
            return await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        }

        /// <summary>
        /// POSTs a web request to a URL and returns the raw http web response
        /// </summary>
        /// <param name="url">The URL</param>
        /// <param name="content">The content to post</param>
        /// <param name="sendType">The format to serialize the content into</param>
        /// <param name="returnType">The expected type of content to be returned from the server</param>
        /// <param name="configureRequest">Allows caller to customize and configure the HttpRequestMessage prior to sending</param>
        /// <param name="bearerToken">If specified, provides the Authorization header with JWT bearer token</param>
        public static async Task<HttpResponseMessage> PostAsync(
            string url,
            object content = null,
            KnownContentSerializers sendType = KnownContentSerializers.Json,
            KnownContentSerializers returnType = KnownContentSerializers.Json,
            Action<HttpRequestMessage> configureRequest = null,
            string bearerToken = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);

            // Set Accept header
            request.Headers.Accept.ParseAdd(returnType.ToMimeString());

            if (!string.IsNullOrEmpty(bearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            // Prepare Request Body
            if (content != null)
            {
                string contentString = string.Empty;

                if (sendType == KnownContentSerializers.Json)
                {
                    contentString = JsonConvert.SerializeObject(content);
                }
                else if (sendType == KnownContentSerializers.Xml)
                {
                    var xmlSerializer = new XmlSerializer(content.GetType());
                    using var stringWriter = new StringWriter();
                    xmlSerializer.Serialize(stringWriter, content);
                    contentString = stringWriter.ToString();
                }

                request.Content = new StringContent(contentString, Encoding.UTF8, sendType.ToMimeString());
            }

            configureRequest?.Invoke(request);

            return await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        }

        /// <summary>
        /// POSTs a web request to a URL and returns a response of the expected data type
        /// </summary>
        public static async Task<WebRequestResult<TResponse>> PostAsync<TResponse>(
            string url,
            object content = null,
            KnownContentSerializers sendType = KnownContentSerializers.Json,
            KnownContentSerializers returnType = KnownContentSerializers.Json,
            Action<HttpRequestMessage> configureRequest = null,
            string bearerToken = null)
        {
            HttpResponseMessage serverResponse;

            try
            {
                serverResponse = await PostAsync(url, content, sendType, returnType, configureRequest, bearerToken);
            }
            catch (Exception ex)
            {
                return new WebRequestResult<TResponse>
                {
                    ErrorMessage = ex.Message
                };
            }

            var result = await serverResponse.CreateWebRequestResultAsync<TResponse>();

            if (result.StatusCode != HttpStatusCode.OK)
            {
                return result;
            }

            if (string.IsNullOrEmpty(result.RawServerResponse))
            {
                return result;
            }

            try
            {
                var responseMediaType = serverResponse.Content?.Headers.ContentType?.MediaType;
                if (responseMediaType != null && !responseMediaType.ToLower().Contains(returnType.ToMimeString().ToLower()))
                {
                    result.ErrorMessage = $"Server did not return data in expected type. Expected {returnType.ToMimeString()}, received {responseMediaType}";
                    return result;
                }

                if (returnType == KnownContentSerializers.Json)
                {
                    result.ServerResponse = JsonConvert.DeserializeObject<TResponse>(result.RawServerResponse);
                }
                else if (returnType == KnownContentSerializers.Xml)
                {
                    var xmlSerializer = new XmlSerializer(typeof(TResponse));
                    using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(result.RawServerResponse));
                    result.ServerResponse = (TResponse)xmlSerializer.Deserialize(memoryStream);
                }
                else
                {
                    result.ErrorMessage = "Unknown return type, cannot deserialize server response to the expected type";
                    return result;
                }
            }
            catch (Exception)
            {
                result.ErrorMessage = "Failed to deserialize server response to the expected type";
                return result;
            }

            return result;
        }
    }
}