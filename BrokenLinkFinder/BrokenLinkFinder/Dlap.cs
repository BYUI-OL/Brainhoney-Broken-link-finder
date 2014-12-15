using System;
using System.IO;
using System.Net;
using System.Xml;
using System.Xml.Linq;

namespace DLAP
{
    public class Session
    {
        /// <summary>
        /// The user agent for this application
        /// You should set this to identify the calling application for logging
        /// </summary>
        public string Agent { get; private set; }
        /// <summary>
        /// The URL to the DLAP server
        /// </summary>
        public string Server { get; private set; }
        /// <summary>
        /// Request timeout in milliseconds
        /// Defaults to 30000 (30 seconds)
        /// </summary>
        public int Timeout { get; set; }
        /// <summary>
        /// Callback for logging all requests and responses
        /// </summary>
        public bool Verbose { get; set; }

        private CookieContainer _cookies;

        /// <summary>
        /// Create a new session object
        /// </summary>
        /// <param name="agent">Useragent to identify this session</param>
        /// <param name="server">URL to DLAP server</param>
        /// <param name="timeout">Request timeout in milliseconds</param>
        /// <param name="Verbose">Log all requests and responses</param>
        public Session(string agent, string server, int timeout = 30000, bool verbose = false)
        {
            Agent = agent;
            Server = server;
            Timeout = timeout;
            Verbose = verbose;
        }

        /// <summary>
        /// Login to DLAP. Call Logout to close session.
        /// </summary>
        /// <param name="prefix">Login prefix</param>
        /// <param name="username">Username</param>
        /// <param name="password">Password</param>
        /// <returns>XML results</returns>
        public XElement Login(string prefix, string username, string password)
        {
            _cookies = new CookieContainer();
            return Post(null, new XElement("request",
                new XAttribute("cmd", "login"),
                new XAttribute("username", string.Concat(prefix, "/", username)),
                new XAttribute("password", password)));
        }

        /// <summary>
        /// Logout of DLAP
        /// </summary>
        /// <returns>XML results</returns>
        public XElement Logout()
        {
            XElement result = Get("logout");
            _cookies = null;
            return result;
        }

        /// <summary>
        /// Makes a GET request to DLAP
        /// </summary>
        /// <param name="cmd">DLAP command</param>
        /// <param name="parameters">pairs of name-value for additional parameters</param>
        /// <returns>XML results</returns>
        public XElement Get(string cmd, params string[] parameters)
        {
            string query = "?cmd=" + cmd;
            for (int index = 0; index + 1 < parameters.Length; index += 2)
            {
                query += "&" + parameters[index] + "=" + parameters[index + 1];
            }

            TraceRequest(query, null);
            return ReadResponse(Request(query, null, null, Timeout));
        }

        /// <summary>
        /// Makes a POST request to DLAP
        /// </summary>
        /// <param name="cmd">DLAP command</param>
        /// <param name="xml">XML to post to DLAP</param>
        /// <returns>XML results</returns>
        public XElement Post(string cmd, XElement xml)
        {
            string query = string.IsNullOrEmpty(cmd) ? string.Empty : ("?cmd=" + cmd);
            TraceRequest(query, xml);
            using (MemoryStream data = new MemoryStream())
            {
                using (XmlTextWriter writer = new XmlTextWriter(data, System.Text.Encoding.UTF8))
                {
                    xml.WriteTo(writer);
                    writer.Flush();
                    data.Flush();
                    data.Position = 0;
                    return ReadResponse(Request(query, data, "text/xml", Timeout));
                }
            }
        }

        /// <summary>
        /// Makes a POST request to DLAP
        /// </summary>
        /// <param name="cmd">DLAP command</param>
        /// <param name="xml">XML to post to DLAP</param>
        /// <returns>XML results</returns>
        public XElement Post(string cmd, string xml)
        {
            if (!xml.StartsWith("<request"))
                xml = "<requests>" + xml + "</requests>";
            return Post(cmd, XElement.Parse(xml));
        }

        /// <summary>
        /// Makes a raw request to DLAP. Use the Request methods when for XML data
        /// </summary>
        /// <param name="query">Full query string for the request</param>
        /// <param name="postData">Optional postdata, if present the request is a POST rather than a get.</param>
        /// <param name="contentType">Content type of the postdata</param>
        /// <param name="timeout">Request timeout in milliseconds</param>
        /// <returns>HttpWebResponse</returns>
        public HttpWebResponse Request(string query, Stream postData, string contentType, int timeout)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(Server + query);
            request.UserAgent = Agent;
            request.AllowAutoRedirect = false;
            request.CookieContainer = _cookies;
            request.Timeout = timeout;
            if (timeout > request.ReadWriteTimeout)
            {
                request.ReadWriteTimeout = timeout;
            }

            if (postData != null)
            {
                request.Method = "POST";
                if (!string.IsNullOrEmpty(contentType))
                {
                    request.ContentType = contentType;
                }
                request.ContentLength = postData.Length;
                using (Stream stream = request.GetRequestStream())
                {
                    byte[] buffer = new byte[16 * 1024];
                    int bytes;
                    // post the data
                    while ((bytes = postData.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        stream.Write(buffer, 0, bytes);
                    }
                    stream.Close();
                }
            }
            else
            {
                request.Method = "GET";
            }

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            return response;
        }

        private void TraceRequest(string query, XElement xml)
        {
            if (Verbose)
            {
                Log("Request: " + Server + query);
                if (xml != null)
                {
                    Log(xml.ToString());
                }
            }
        }

        private void TraceResponse(XElement xml)
        {
            if (Verbose)
            {
                Log(xml.ToString());
            }
        }

        private void Log(string line)
        {
            Console.WriteLine(line);
        }

        /// <summary>
        /// Returns the XML data when calling RawRequest
        /// </summary>
        /// <param name="response">HttpWebResponse returned from RawRequest</param>
        /// <returns>XML results</returns>
        public XElement ReadResponse(HttpWebResponse response)
        {
            using (Stream stream = response.GetResponseStream())
            {
                try
                {
                    XElement result = XElement.Load(stream);
                    TraceResponse(result);
                    return result;
                }
                catch (Exception e)
                {
                    return new XElement("response",
                        new XAttribute("code", e.GetType().Name),
                        new XAttribute("message", e.Message));
                }
            }
        }

        /// <summary>
        /// Checks if the DLAP call was successful
        /// </summary>
        /// <param name="result">XML result</param>
        /// <returns>TRUE is successful, otherwise false</returns>
        public static bool IsSuccess(XElement result)
        {
            return result != null &&
                result.Name == "response" &&
                result.Attribute("code") != null &&
                result.Attribute("code").Value == "OK";
        }

        /// <summary>
        /// Returns the error message for a failed DLAP call
        /// </summary>
        /// <param name="result">XML results</param>
        /// <returns>Error message as given by DLAP</returns>
        public static string GetMessage(XElement result)
        {
            if (result != null &&
                result.Name == "response" &&
                result.Attribute("message") != null)
                return result.Attribute("message").Value;
            return "Unknown error";
        }
    }
}
