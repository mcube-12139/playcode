using System.Net;
using System.Text;
using System.Text.Json;

class BaseResult {
    public bool win { get; set; } = true;
}

class Playcode {
    static string PATH_NOT_EXIST = "{\"win\":false,\"error\":\"Path not exist\"}";
    static string WRONG_FORMAT = "{\"win\":false,\"error\":\"Wrong format\"}";
    static string EMPTY_WIN = "{\"win\":true}";

    public static HttpListener listener;
    public static string url = "http://localhost:12139/";
    public static int pageViews = 0;
    public static int requestCount = 0;
    public static bool runServer = true;

    public static Dictionary<string, Func<string, string>> pathDict = new() {
        { "/shutdown", getResponseFunNoParamNoResult(Shutdowner.response) },
        { "/sum", getResponseFun<AdderParam, AdderResult>(Adder.response) }
    };

    public static async Task HandleIncomingConnections() {
        // While a user hasn't visited the `shutdown` url, keep on handling requests
        while (runServer) {
            // Will wait here until we hear from a connection
            HttpListenerContext ctx = await listener.GetContextAsync();

            // Peel out the requests and response objects
            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse resp = ctx.Response;

            if (req.HttpMethod == "POST") {
                // 处理 POST
                string? path = req.Url?.AbsolutePath;
                Console.WriteLine($"post {path}");

                string responseStr;
                if (path != null && pathDict.ContainsKey(path)) {
                    // 请求路径存在，调用处理函数
                    Func<string, string> handler = pathDict[path];

                    Stream body = req.InputStream;
                    Encoding encoding = req.ContentEncoding;
                    StreamReader reader = new(body, encoding);
                    string bodyStr = reader.ReadToEnd();

                    responseStr = handler(bodyStr);
                } else {
                    // 请求路径不存在，返回错误
                    responseStr = PATH_NOT_EXIST;
                }

                byte[] responseData = Encoding.UTF8.GetBytes(responseStr);
                resp.ContentType = "application/json";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = responseData.LongLength;

                await resp.OutputStream.WriteAsync(responseData);
                resp.Close();
            } else if (req.HttpMethod == "GET") {
                // Make sure we don't increment the page views counter if `favicon.ico` is requested
                if (req.Url.AbsolutePath != "/favicon.ico")
                    pageViews += 1;

                // Write the response info
                string pageData = File.ReadAllText("index.html");
                byte[] data = Encoding.UTF8.GetBytes(pageData);
                resp.ContentType = "text/html";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;

                // Write out to the response stream (asynchronously), then close it
                await resp.OutputStream.WriteAsync(data);
                resp.Close();
            }
        }
    }

    public static Func<string, string> getResponseFun<Param, Result>(Func<Param, Result> handler) {
        return (string requestStr) => {
            string responseStr;

            try {
                Param? bodyJson = JsonSerializer.Deserialize<Param>(requestStr);
                if (bodyJson != null) {
                    Result response = handler(bodyJson);
                    responseStr = JsonSerializer.Serialize(response);
                } else {
                    return WRONG_FORMAT;
                }
            } catch (Exception _) {
                return WRONG_FORMAT;
            }

            return responseStr;
        };
    }

    public static Func<string, string> getResponseFunNoParam<Result>(Func<Result> handler) {
        return (string _) => {
            string responseStr;

            Result response = handler();
            responseStr = JsonSerializer.Serialize(response);

            return responseStr;
        };
    }

    public static Func<string, string> getResponseFunNoResult<Param>(Action<Param> handler) {
        return (string requestStr) => {
            try {
                Param? bodyJson = JsonSerializer.Deserialize<Param>(requestStr);
                if (bodyJson != null) {
                    handler(bodyJson);
                }
            } catch (Exception _) {
                return WRONG_FORMAT;
            }

            return EMPTY_WIN;
        };
    }

    public static Func<string, string> getResponseFunNoParamNoResult(Action handler) {
        return (string _) => {
            handler();
            return EMPTY_WIN;
        };
    }

    public static void Main(string[] _) {
        // Create a Http server and start listening for incoming connections
        listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();
        Console.WriteLine("Listening for connections on {0}", url);

        // Handle requests
        Task listenTask = HandleIncomingConnections();
        listenTask.GetAwaiter().GetResult();

        // Close the listener
        listener.Close();
    }
}