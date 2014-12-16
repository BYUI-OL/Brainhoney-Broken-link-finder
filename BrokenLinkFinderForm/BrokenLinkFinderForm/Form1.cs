using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace BrokenLinkFinderForm
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {

            BrokenLinkFinder.BrokenLinkFinder set = new BrokenLinkFinder.BrokenLinkFinder("15336545");
            set.Authenticate("byui/", "EnterUserName", "EnterPassword");
            XmlDocument sheet = set.Start();
            richTextBox1.Text = GetXMLAsString(sheet);
        }

        public string GetXMLAsString(XmlDocument sheet)
        {

            StringWriter sw = new StringWriter();
            XmlTextWriter tx = new XmlTextWriter(sw);
            sheet.WriteTo(tx);

            string str = sw.ToString();// 
            return str;
        }
    }
}
