using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace BrokenLinkFinder
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            DisplayLoginStatus();
            PopulateDomains();
        }

        private void PopulateDomains()
        {
            XElement predomains = Program._crawler.GetDomains();
            XmlDocument domainsDoc = new XmlDocument();
            domainsDoc.LoadXml(predomains.ToString());
            XmlNodeList domainList = domainsDoc.GetElementsByTagName("domain");
            
            foreach (XmlNode domain in domainList)
            {
                string name = Program._crawler.GetAttribute(domain.Attributes, "name");
                domains.Items.Add(name);
            }
        }

        private void DisplayLoginStatus()
        {
            if (Program._crawler.IsLoggedIn)
            {
                loggedInLabel.Text = Program._username + ": Logged In";
            }
            else
            {
                loggedInLabel.Text = Program._username + ": Not Logged In";
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Application.Exit();
        }
    }
}
