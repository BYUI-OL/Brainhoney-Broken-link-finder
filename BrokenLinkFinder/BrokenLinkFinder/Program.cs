using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrokenLinkFinder
{
    static class Program
    {
        public static Login _login;
        public static Crawler _crawler;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            _login = new Login();
            Application.Run(_login);
        }

        public static string _username;
        public static string _password;
        public static string _prefix;

        public static void Next()
        {
            _login.Hide();
            _crawler = new Crawler(_username, _password, _prefix);
            (new Form1()).Show();
        }
    }
}
