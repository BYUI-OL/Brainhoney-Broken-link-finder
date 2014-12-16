using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DLAP;
using System.Xml.Linq;
using System.Xml;
using CsQuery;
using System.Net;

namespace BrokenLinkFinder
{
    public class BrokenLinkFinder
    {
        private string _courseId;
        private Session _session;
        private bool _isAuthenticated = false; // Default is false
        private XmlNodeList _resourceList;
        private XmlNodeList _itemList;

        /// <summary>
        /// Constructor, pass the course id
        /// </summary>
        /// <param name="courseId"></param>
        public BrokenLinkFinder(string courseId)
        {
            _courseId = courseId;
            _session = new Session("BrokenLinkFinder", "http://gls.agilix.com/dlap.ashx", 30000, false);
        }

        /// <summary>
        /// Log the user into the agilix system
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public bool Authenticate(string prefix, string username, string password)
        {
            
            try
            {
                XElement result = Verify(_session.Login(prefix, username, password)); // Call the Dlap built in login method
                _isAuthenticated = true;
                return true;
            }
            catch (DlapException e) // Throw the exception right up the chain
            {
                throw e;
            }
        }

        /// <summary>
        /// Returns a list of the broken hyperlinks
        /// </summary>
        /// <returns></returns>
        public XmlDocument Start()
        {
            if (!_isAuthenticated) // If the user is not authenticated
            {
                throw new DlapException("Unable to authenticate");
            }

            try
            {
                /*
                 * Creating the xml structure
                 * 
                 * <links>
                 *  <link courseid='' itemid='' name='' statuscode='' />
                 * </link>
                 */
                XmlDocument returnDoc = new XmlDocument();
                XmlElement linksNode = returnDoc.CreateElement("links");

                XmlDocument itemDoc = GetItemList().Convert(); // Convert is the extension below
                _itemList = itemDoc.SelectNodes("//item"); // get a list of the item nodes
                if (_itemList.Count <= 0) // If there aren't any course items available
                {
                    throw new DlapException("Unable to pull items with the courseid, " + _courseId);
                }

                XmlDocument resourceDoc = GetResourceList().Convert();
                _resourceList = resourceDoc.SelectNodes("//resource");

                foreach (XmlNode item in _itemList)
                {
                    string href = item.SelectSingleNode("//data/href").InnerText; // Get the href
                    string id = item.GetAttribute("id");
                    string html = GetHtml(href); // Get the html as a string
                    Parsed links = GetLinks(html); // Parse it to Parsed and populate the links with Link
                    List<Link> tested = TestLinks(links);
                    if (tested.Count > 0)
                    {
                        foreach (Link link in tested)
                        {
                            XmlElement ele = returnDoc.CreateElement("link");
                            ele.SetAttribute("courseid", _courseId);
                            ele.SetAttribute("itemid", id);
                            ele.SetAttribute("name", link.name);
                            ele.SetAttribute("status", link.status + "");
                            linksNode.AppendChild(ele);
                        }
                    }
                }

                returnDoc.AppendChild(linksNode);
                return returnDoc;
            }
            catch (DlapException e) // Throw the exception right up the chain
            {
                throw e;
            }
        }

        /// <summary>
        /// Test each link
        /// </summary>
        /// <param name="links"></param>
        /// <returns></returns>
        private List<Link> TestLinks(Parsed links)
        {
            List<Link> broken = new List<Link>();
            int count = 0;
            foreach (Link link in links.links)
            {
                if (link.type == LinkType.EXTERNAL) // status code
                {
                    Link one = link;
                    one.status = GetStatus(link.href);
                    if (one.status == 301 || one.status == 308 || one.status >= 400)
                    {
                        broken.Add(one);
                    }
                }
                else if (link.type == LinkType.ITEM) // check items
                {
                    if (!CheckItemList(link.href))
                    {
                        broken.Add(link);
                    }
                }
                else if (link.type == LinkType.RESOURCE) // check if in resource list
                {
                    if (!checkResourceList(link.href))
                    {
                        broken.Add(link);
                    }
                }
                else
                {
                    throw new DlapException("Corrupted html page");
                }
                count++;
            }
            return broken;
        }

        /// <summary>
        /// Get status codes
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private int GetStatus(string url)
        {
            HttpStatusCode result = default(HttpStatusCode);

            var request = HttpWebRequest.Create(url);
            request.Method = "HEAD";
            using (var response = request.GetResponse() as HttpWebResponse)
            {
                if (response != null)
                {
                    result = response.StatusCode;
                    response.Close();
                }
            }

            return (int)result;
        }

        /// <summary>
        /// Check for the internal links
        /// </summary>
        /// <param name="href"></param>
        /// <returns></returns>
        private bool CheckItemList(string href)
        {
           foreach (XmlNode item in _itemList)
           {
               if (href.Contains(item.GetAttribute("id")))
               {
                   return true;
               }
           }

            return false;
        }

        /// <summary>
        /// Check to see if resource is still in course
        /// </summary>
        /// <param name="href"></param>
        /// <returns></returns>
        private bool checkResourceList(string href)
        {
            string modifiedHref = href.Remove(0, 4);
            foreach (XmlNode resource in _resourceList)
            {
                if (resource.GetAttribute("path") == modifiedHref)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The parsed html and a list of links
        /// </summary>
        private struct Parsed
        {
            public CQ doc;
            public List<Link> links;
        }

        /// <summary>
        /// Link information
        /// </summary>
        private struct Link
        {
            public string name; // Name of link. <a href=''>Google</a> - Google
            public string href;
            public LinkType type; // href, src
            public string node; // node name, i.e. iframe, a
            public int status;
        }

        /// <summary>
        /// Differenciate between the different links
        /// </summary>
        private enum LinkType
        {
            ITEM,
            RESOURCE,
            EXTERNAL
        }

        /// <summary>
        /// Get all the links in the html
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        private Parsed GetLinks(string html)
        {
            Parsed parsed = ParseHtml(html);
            CQ hrefs = parsed.doc["[href]"];
            CQ srcs = parsed.doc["[src]"];

            foreach (IDomElement href in hrefs)
            {
                Link link = new Link();
                link.node = href.NodeName;
                link.href = href.GetAttribute("href");
                link.type = GetLinkType(link.href);
                link.name = href.InnerText;
                parsed.links.Add(link);
            }

            foreach (IDomElement src in srcs)
            {
                Link link = new Link();
                link.node = src.NodeName;
                link.href = src.GetAttribute("src");
                link.type = GetLinkType(link.href);
                link.name = src.InnerText;
                parsed.links.Add(link);
            }

            return parsed;
        }

        /// <summary>
        /// Returns the link type from the href/src attribute
        /// </summary>
        /// <param name="href"></param>
        /// <returns></returns>
        private LinkType GetLinkType(string href)
        {
            if (href.Contains("navTo"))
            {
                return LinkType.ITEM;
            }
            else if (href.Contains("[~]"))
            {
                return LinkType.RESOURCE;
            }
            else
            {
                return LinkType.EXTERNAL;
            }

        }

        /// <summary>
        /// Parse the html to CsQuery's format
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        private Parsed ParseHtml(string html)
        {
            Parsed parsed = new Parsed();
            parsed.links = new List<Link>();
            parsed.doc = html;
            return parsed;
        }

        private string GetHtml(string href)
        {
            return _session.Get2("getresource", new string[] { "entityid", _courseId, "path", href });
        }

        /// <summary>
        /// Verify that the dlap call was a success
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private XElement Verify(XElement result)
        {
            if (!Session.IsSuccess(result)) // Verify success
            {
                throw new DlapException(result.GetAttribute("message"));
            }
            return result;
        }

        /// <summary>
        /// Get the list of items in a course
        /// </summary>
        /// <returns></returns>
        private XElement GetItemList()
        {
            try
            {
                return Verify(_session.Get("getitemlist", new string[] { "entityid", _courseId })); // dlap call and verify
            }
            catch (DlapException e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Get the list of items in a course. Some links link to a resource or item within the course. This will verify it exists
        /// </summary>
        /// <returns></returns>
        private XElement GetResourceList()
        {
            try
            {
                return Verify(_session.Get("getresourcelist2", new string[] { "entityid", _courseId })); // dlap call and verify
            }
            catch (DlapException e)
            {
                throw e;
            }
        }
    }

    public class DlapException : Exception
    {
        public DlapException()
        {
        }

        public DlapException(string message)
            : base(message)
        {
        }

        public DlapException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public static class XAttributeExtension
    {
        /// <summary>
        /// Extension to add a GetAttribute to XElement
        /// </summary>
        /// <param name="ele"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetAttribute(this XElement ele, string name)
        {
            foreach (XAttribute attr in ele.Attributes())
            {
                if (attr.Name == name)
                {
                    return attr.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// Extension to add a GetAttribute to XmlNode
        /// </summary>
        /// <param name="ele"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetAttribute(this XmlNode ele, string name)
        {
            foreach (XmlAttribute attr in ele.Attributes)
            {
                if (attr.Name == name)
                {
                    return attr.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// Extension to convert the XElement to a XmlDocument (more functionality)
        /// </summary>
        /// <param name="ele"></param>
        /// <returns></returns>
        public static XmlDocument Convert(this XElement ele)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(ele.ToString());
            return doc;
        }
    }
}
