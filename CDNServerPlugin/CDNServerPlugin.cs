using INSM.Server.Plugin.Framework.v1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmptyPlugin
{
    /// <summary>
    /// Example of an CDN server plugin
    /// </summary>
    public class CDNServerPlugin : ICDNServerPlugin
    {
        private ICDNServerPluginContext _Context;

        public ICDNServerPluginContext Context
        {
            get { return _Context; }
            set { _Context = value; }
        }

        public int RequiredPlatformAPILevel
        {
            get { return 1; }
        }

        public string Name
        {
            get { return "Acme CDN plugin"; }
        }

        public string Vendor
        {
            get { return "INSM"; }
        }

        public string Version
        {
            get { return "1.0"; }
        }

        public bool UploadFile(string localFilePath, int fileId, FileType fileType)
        {
            //TODO upload file here

            bool enableDebugLogging = _Context.GetAppSettingAsBool("enableDebugLogging");
            _Context.Log(SeverityType.OK, 0, "Redirect CDN Plugin with debug " + enableDebugLogging);


            Context.UploadProgress(fileId, 0.5f, "Uploading file to CDN...");

            Context.Log(SeverityType.Error, 0, "Upload file failed");

            Context.SetState("Upload", SeverityType.Error, "CDN could not receive file");

            return false;
        }

        public bool DeleteFile(int fileId, FileType fileType)
        {
            //TODO delete file here

            throw new NotImplementedException("CDN DeleteFile");
        }

        public Uri Redirect(int fileId, FileType fileType, string requesterIPAddress, DateTime requestExpires)
        {
            //TODO get redirect uri here

            return new Uri(@"http://insm.eu");
        }

        public bool Ping()
        {
            //Ping that CDN is online here

            return true;
        }

        public bool Check()
        {
            //Check that CDN is online here and does not report any errors

            return true;
        }
    }
}
