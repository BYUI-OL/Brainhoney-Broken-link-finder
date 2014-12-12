using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DLAP;
using System.Xml.Linq;
using System.Xml;

namespace BrokenLinkFinder
{
    class Crawler
    {
        public bool IsLoggedIn = false;
        private Session _session;

        public Crawler(string username, string password, string userPrefix)
        {
            // IMPORTANT - fill these in, or the app will not compile!!!
            
            // Choose an agent string unique to your application
            string agent = "test";

            // The URL to dlap.ashx
            string dlapUrl = "http://gls.agilix.com/dlap.ashx";

            // Use a user with admin rights in their domain for the later calls to succeed

            _session = new Session(agent, dlapUrl);

            _session.Verbose = true;

            // Login
            XElement result = _session.Login(userPrefix, username, password);
            if (!Session.IsSuccess(result))
            {
                Console.WriteLine("Unable to login: " + Session.GetMessage(result));
                return;
            }
            IsLoggedIn = true;            
        }

        public XElement GetDomains()
        {
            XElement user = _session.Get("getuser2", null);
            XmlDocument userEle = new XmlDocument();
            userEle.LoadXml(user.ToString());
            XmlAttributeCollection attrs = userEle.GetElementsByTagName("user")[0].Attributes;
            string basedomain = GetAttribute(attrs, "domainid");
            XElement domains = _session.Get("listdomains", new string[] {"domainid", basedomain});
            return domains;
        }

        public string GetAttribute(XmlAttributeCollection attrs, string name)
        {
            foreach (XmlAttribute attr in attrs)
            {
                if (attr.Name == name)
                {
                    return attr.Value;
                }
            }
            return null;
        }

        public void Logout()
        {
            _session.Logout();
        }
    }
}
