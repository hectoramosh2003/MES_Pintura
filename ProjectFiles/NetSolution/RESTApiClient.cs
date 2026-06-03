#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.UI;
using FTOptix.Retentivity;
using FTOptix.NativeUI;
using FTOptix.CoreBase;
using FTOptix.Core;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Threading.Tasks;
using FTOptix.DataLogger;
using FTOptix.RAEtherNetIP;
using FTOptix.CommunicationDriver;
using FTOptix.SerialPort;
#endregion

public class RESTApiClient : BaseNetLogic
{
    readonly struct HTTPResponse
    {
        /// <summary>
        /// This method sets the payload and status code for an HTTP response.
        /// <example>
        /// For example:
        /// <code>
        /// HTTPResponse response = new HTTPResponse("Hello World", 200);
        /// </code>
        /// results in <c>response.Payload</c> containing "Hello World" and <c>response.Code</c> being set to 200.
        /// </example>
        /// </summary>
        /// <param name="payload">The content to be sent as the response payload.</param>
        /// <param name="code">The HTTP status code to be associated with this response.</param>
        /// <returns>A reference to the created HTTPResponse object.</returns>
        public HTTPResponse(string payload, int code)
        {
            Payload = payload;
            Code = code;
        }

        public string Payload { get; }
        public int Code { get; }
    };

    public override void Start()
    {
    }

    public override void Stop()
    {
    }

    /// <summary>
    /// Retrieves the timeout value from the logic object's variable with the key "Timeout".
    /// If the variable is missing or cannot be retrieved, an exception will be thrown.
    /// The returned value represents the timeout duration as a long.
    /// </summary>
    /// <returns>The timeout value as a long.</returns>
    private long GetTimeout()
    {
        var timeoutVariable = LogicObject.Get<IUAVariable>("Timeout");
        if (timeoutVariable == null)
            throw new Exception($"Missing Timeout variable under the NetLogic {LogicObject.BrowseName}");

        return timeoutVariable.Value;
    }

    /// <summary>
    /// Retrieves the user agent from the logic object and throws an exception if it's missing.
    /// </summary>
    /// <returns>The value of the user agent as a string.</returns>
    private string GetUserAgent()
    {
        var userAgentVariable = LogicObject.Get<IUAVariable>("UserAgent");
        if (userAgentVariable == null)
            throw new Exception($"Missing UserAgent variable under the NetLogic {LogicObject.BrowseName}");

        return userAgentVariable.Value;
    }

    /// <summary>
    /// This method checks if a given scheme string is either HTTP or HTTPS.
    /// <example>
    /// For example:
    /// <code>
    /// bool supported = IsSupportedScheme("http");
    /// </code>
    /// would result in <c>supported</c> being true.
    /// </example>
    /// </summary>
    /// <param name="scheme">The scheme string to check.</param>
    /// <returns>
    /// A boolean indicating whether the scheme is HTTP or HTTPS.
    /// </returns>
    private bool IsSupportedScheme(string scheme)
    {
        return scheme == "http" || scheme == "https";
    }

    /// <summary>
    /// This method checks if the given scheme is secure (HTTPS).
    /// <example>
    /// For example:
    /// <code>
    /// bool isSecure = IsSecureScheme("https");
    /// </code>
    /// results in <c>isSecure</c> being true.
    /// </example>
    /// </summary>
    /// <param name="scheme">The scheme to check.</param>
    /// <returns>
    /// A boolean indicating whether the scheme is secure (true) or not (false).
    /// </returns>
    private bool IsSecureScheme(string scheme)
    {
        return scheme == "https";
    }

    /// <summary>
    /// This method constructs an HTTP GET message with specified URL, user agent, and bearer token.
    /// <example>
    /// For example:
    /// <code>
    /// HttpRequestMessage request = BuildGetMessage(new Uri("https://api.example.com/data"), "MyApp/1.0", "Bearer mytoken");
    /// </code>
    /// results in <c>request</c> containing the constructed GET request with appropriate headers.
    /// </example>
    /// </summary>
    /// <param name="url">The target URL for the GET request.</param>
    /// <param name="userAgent">User agent string for the request.</param>
    /// <param name="bearerToken">Bearer token for authentication.</param>
    /// <returns>
    /// The constructed HttpRequestMessage object with the specified headers.
    /// </returns>
    /// <remarks>
    /// The method adds user agent information to the request's headers if provided.
    /// It also adds a Bearer authorization header if a bearer token is given.
    /// </remarks>
    private HttpRequestMessage BuildGetMessage(Uri url, string userAgent, string bearerToken)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);

        if (!string.IsNullOrWhiteSpace(userAgent))
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(new ProductHeaderValue(userAgent)));

        if (!string.IsNullOrWhiteSpace(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        return request;
    }

    /// <summary>
    /// This method creates a POST request message with specified URL, body content, content type, user agent, and bearer token.
    /// <example>
    /// For example:
    /// <code>
    /// HttpRequestMessage postRequest = BuildPostMessage(new Uri("https://api.example.com/data"), "{\"key\":\"value\"}", "application/json", "MyApp/1.0", "Bearer mytoken");
    /// </code>
    /// results in <c>postRequest</c> having the appropriate headers for a POST request.
    /// </example>
    /// </summary>
    /// <param name="url">The target URL for the POST request.</param>
    /// <param name="body">The JSON-formatted body content for the POST request.</param>
    /// <param name="contentType">The content type of the request body (e.g., "application/json").</param>
    /// <param name="userAgent">The user agent header for the request.</param>
    /// <param name="bearerToken">The Bearer token for authentication.</param>
    /// <returns>A new instance of HttpRequestMessage representing the created POST request.</returns>
    private HttpRequestMessage BuildPostMessage(Uri url, string body, string contentType, string userAgent, string bearerToken)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);

        if (!string.IsNullOrWhiteSpace(body))
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, contentType);

        if (!string.IsNullOrWhiteSpace(userAgent))
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(new ProductHeaderValue(userAgent)));

        if (!string.IsNullOrWhiteSpace(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        return request;
    }

    /// <summary>
    /// This method creates a PUT request with specified URL, body content, content type, user agent, and bearer token.
    /// <example>
    /// For example:
    /// <code>
    /// HttpRequestMessage message = BuildPutMessage(new Uri("https://api.example.com/resource"), "{\"key\":\"value\"}", "application/json", "MyApp/Version1", "Bearer MyToken");
    /// </code>
    /// results in <c>message</c> containing a PUT request with the provided parameters.
    /// </example>
    /// </summary>
    /// <param name="url">The target URI for the PUT request.</param>
    /// <param name="body">The body content for the request (if applicable).</param>
    /// <param name="contentType">The content type of the request's body.</param>
    /// <param name="userAgent">The user agent header for the request.</param>
    /// <param name="bearerToken">The bearer token for authentication.</param>
    /// <returns>A new HttpRequestMessage object representing the created request.</returns>
    private HttpRequestMessage BuildPutMessage(Uri url, string body, string contentType, string userAgent, string bearerToken)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, url);

        if (!string.IsNullOrWhiteSpace(body))
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, contentType);

        if (!string.IsNullOrWhiteSpace(userAgent))
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(new ProductHeaderValue(userAgent)));

        if (!string.IsNullOrWhiteSpace(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        return request;
    }

    /// <summary>
    /// This asynchronous method sends an HTTP request to a server with a specified timeout duration.
    /// It awaits for the response from the server, reads the content into a string, and returns it along with the status code as an HTTPResponse object.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="timeout">The maximum time allowed for sending the request and receiving the response.</param>
    /// <returns>An HTTPResponse containing the response body and status code.</returns>
    private async Task<HTTPResponse> PerformRequest(HttpRequestMessage request, TimeSpan timeout)
    {
        HttpClient client = new HttpClient();
        client.Timeout = timeout;

        using HttpResponseMessage httpResponse = await client.SendAsync(request);
        string responseBody = await httpResponse.Content.ReadAsStringAsync();

        return new HTTPResponse(responseBody, (int)httpResponse.StatusCode);
    }

    /// <summary>
    /// This method constructs an HTTP request message based on the provided verb, URL, request body, bearer token, and content type.
    /// <example>
    /// For example:
    /// <code>
    /// var message = BuildMessage(HttpMethod.Get, new Uri("https://api.example.com/data"), null, null, null);
    /// </code>
    /// results in <c>message</c> containing the constructed HTTP GET request with the specified user agent and bearer token.
    /// </example>
    /// </summary>
    /// <param name="verb">The HTTP verb for the request.</param>
    /// <param name="url">The target URL for the request.</param>
    /// <param name="requestBody">The request body data.</param>
    /// <param name="bearerToken">The bearer token for authentication.</param>
    /// <param name="contentType">The content type of the request.</param>
    /// <returns>
    /// An HttpWebRequest object representing the built HTTP request message.
    /// </returns>
    /// <remarks>
    /// The method handles various scenarios such as checking the supported schemes, validating the bearer token, and constructing appropriate messages for different HTTP verbs.
    /// </remarks>
    private HttpRequestMessage BuildMessage(HttpMethod verb, Uri url, string requestBody, string bearerToken, string contentType)
    {
        TimeSpan timeout = TimeSpan.FromMilliseconds(GetTimeout());
        string userAgent = GetUserAgent();

        if (string.IsNullOrWhiteSpace(contentType))
            contentType = "application/json";

        if (!IsSupportedScheme(url.Scheme))
            throw new Exception($"The URI scheme {url.Scheme} is not supported");

        if (!IsSecureScheme(url.Scheme) && !string.IsNullOrWhiteSpace(bearerToken))
            Log.Warning("Possible sending of unencrypted confidential information");

        if (verb == HttpMethod.Get)
            return BuildGetMessage(url, userAgent, bearerToken);
        if (verb == HttpMethod.Post)
            return BuildPostMessage(url, requestBody, contentType, userAgent, bearerToken);
        if (verb == HttpMethod.Put)
            return BuildPutMessage(url, requestBody, contentType, userAgent, bearerToken);

        throw new Exception($"Unsupported verb {verb}");
    }

    /// <summary>
    /// This method sends a GET request to the specified API URL with the given query string and bearer token.
    /// It performs the request asynchronously and captures the response payload and status code.
    /// </summary>
    /// <param name="apiUrl">The URL of the API endpoint to send the request to.</param>
    /// <param name="queryString">The query string to be appended to the URL.</param>
    /// <param name="bearerToken">The Bearer token for authentication purposes.</param>
    /// <param name="out response">A variable where the response payload will be stored.</param>
    /// <param name="out code">A variable where the HTTP status code will be stored.</param>
    [ExportMethod]
    public void Get(string apiUrl, string queryString, string bearerToken, out string response, out int code)
    {
        TimeSpan timeout = TimeSpan.FromMilliseconds(GetTimeout());
        UriBuilder uriBuilder = new UriBuilder(apiUrl);
        uriBuilder.Query = queryString;

        var requestMessage = BuildMessage(HttpMethod.Get, uriBuilder.Uri, "", bearerToken, "");
        var requestTask = PerformRequest(requestMessage, timeout);
        var httpResponse = requestTask.Result;

        (response, code) = (httpResponse.Payload, httpResponse.Code);
    }

    /// <summary>
    /// This method sends a POST request to the specified API URL with the given request body, bearer token, content type, and performs asynchronous HTTP request operations.
    /// The method captures the response payload and status code asynchronously and returns them as output parameters.
    /// <example>
    /// For example:
    /// <code>
    /// string responseBody, statusCode;
    /// Post("https://api.example.com", "{\"key\":\"value\"}", "Bearer your_token", "application/json", out responseBody, out statusCode);
    /// </code>
    /// Results in <c>responseBody</c> containing the response payload and <c>statusCode</c> containing the HTTP status code.
    /// </example>
    /// </summary>
    /// <param name="apiUrl">The URL of the API endpoint to send the request to.</param>
    /// <param name="requestBody">The JSON or other data that needs to be sent as the request body.</param>
    /// <param name="bearerToken">The Bearer token for authentication purposes.</param>
    /// <param name="contentType">The content type of the request body.</param>
    /// <param name="out response">A variable where the response payload will be stored.</param>
    /// <param name="out code">A variable where the HTTP status code will be stored.</param>
    /// <returns>
    /// A tuple containing the response payload and HTTP status code.
    /// </returns>
    [ExportMethod]
    public void Post(string apiUrl, string requestBody, string bearerToken, string contentType, out string response, out int code)
    {
        TimeSpan timeout = TimeSpan.FromMilliseconds(GetTimeout());
        UriBuilder uriBuilder = new UriBuilder(apiUrl);

        var requestMessage = BuildMessage(HttpMethod.Post, uriBuilder.Uri, requestBody, bearerToken, contentType);
        var requestTask = PerformRequest(requestMessage, timeout);
        var httpResponse = requestTask.Result;

        (response, code) = (httpResponse.Payload, httpResponse.Code);
    }
    /// <summary>
    /// This method sends a PUT request to the specified API URL with the given request body, bearer token, content type, and performs the request asynchronously using a timeout.
    /// The method then extracts the response payload and HTTP status code from the response object.
    /// </summary>
    /// <param name="apiUrl">The API endpoint URL.</param>
    /// <param name="requestBody">The request body data for the PUT operation.</param>
    /// <param name="bearerToken">The bearer token required for authentication.</param>
    /// <param name="contentType">The content type of the request.</param>
    /// <param name="out response">The extracted response payload as a string.</param>
    /// <param name="out code">The HTTP status code as an integer.</param>
    /// <returns></returns>
    /// <remarks>
    /// Example usage:
    /// <code>
    /// string responseBody, statusCode;
    /// Put("https://api.example.com", "{\"key\":\"value\"}", "Bearer your_token_here", "application/json", out responseBody, out statusCode);
    /// </code>
    /// will extract the response payload and status code into responseBody and statusCode respectively.
    /// </remarks>
    [ExportMethod]
    public void Put(string apiUrl, string requestBody, string bearerToken, string contentType, out string response, out int code)
    {
        TimeSpan timeout = TimeSpan.FromMilliseconds(GetTimeout());
        UriBuilder uriBuilder = new UriBuilder(apiUrl);

        var requestMessage = BuildMessage(HttpMethod.Put, uriBuilder.Uri, requestBody, bearerToken, contentType);
        var requestTask = PerformRequest(requestMessage, timeout);
        var httpResponse = requestTask.Result;

        (response, code) = (httpResponse.Payload, httpResponse.Code);
    }
}
