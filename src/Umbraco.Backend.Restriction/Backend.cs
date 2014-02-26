﻿using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;
using Newtonsoft.Json;
using System.IO;

namespace Umbraco.Backend.Restriction
{
    public class Backend : IHttpModule
    {
        const string CONFIG_FILE = "~/Config/BackendRestriction.json";

        #region IHttpModule Members
        public void Dispose()
        {
            
        }

        public void Init(HttpApplication context)
        {
            context.BeginRequest += (new EventHandler(this.HandleBeginRequest));
        }

        #endregion
        
        private void HandleBeginRequest( object sender, EventArgs evargs )
        {
            HttpApplication app = sender as HttpApplication;
            // ensure context
            if (EnsureServerVariables(app))
            {
                if (string.IsNullOrWhiteSpace(Config.ConfigFileFullPath))
                {
                    Config.ConfigFileFullPath = app.Server.MapPath(CONFIG_FILE);
                }

                //1. check for not allowed host/ip
                if (!Config.Settings.SafeHosts.Any(h => app.Context.Request.ServerVariables["HTTP_HOST"].Equals(h, StringComparison.InvariantCultureIgnoreCase))
                    && !Config.Settings.SafeIps.Any(h => GetIP(app).Equals(h, StringComparison.InvariantCultureIgnoreCase)))
                {
                    //2. not allowed route i.e: the login page.
                    if (Config.Settings.RegexForbiddenRoutes.Any(r => r.Match(app.Context.Request.ServerVariables["URL"]).Success))
                    {
                        //3. reply -> 403
                        app.Context.Response.StatusCode = 403;
                        app.Context.Response.Write("403");
                        app.Context.Response.End();
                        return;
                    }
                }
                
            }
            return;
        }

        private static String GetIP(HttpApplication app)
        {
            //if you are in a load balanced enviroment, behind a proxy or another thing that may change the client info, you need to chek if 
            // the Ip recived its the rigth one; and not the IP of your middleware hardware.
            String ip = app.Context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];

            if (string.IsNullOrEmpty(ip))
            {
                ip = app.Context.Request.ServerVariables["REMOTE_ADDR"];
            }

            return ip;
        }

        private static bool EnsureServerVariables(HttpApplication app)
        {
            return (app != null
                    && app.Context != null
                    && app.Context.Request != null
                    && app.Context.Request.ServerVariables != null
                    && !string.IsNullOrWhiteSpace(app.Context.Request.ServerVariables["HTTP_HOST"])
                    && (
                            !string.IsNullOrWhiteSpace(app.Context.Request.ServerVariables["REMOTE_ADDR"]) 
                            ||
                            !string.IsNullOrWhiteSpace(app.Context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"])
                        )
                    );
        }

    }

    public static class Config
    {
        public static string ConfigFileFullPath = null;
        private static Settings _value = null;

        internal static Settings Settings
        {
            get {
                return _value ?? (_value = LoadSettings()); 
            }
        }

        private static Settings LoadSettings()
        {
            Settings items = null;
            using (StreamReader r = new StreamReader(ConfigFileFullPath))
            {
                items = JsonConvert.DeserializeObject<Settings>(r.ReadToEnd());
            }
            return items;
        }
    }

    public class Settings
    {
        public List<Regex> RegexForbiddenRoutes { get; set; }
        public List<string> SafeHosts { get; set; }
        public List<string> SafeIps { get; set; }
    }
}
