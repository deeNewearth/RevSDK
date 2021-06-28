using System;
using System.Collections.Generic;
using System.Text;

namespace revCore
{
    public class UploadedFile
    {
        /// <summary>
        /// originalPageName of the file
        /// </summary>
        public string id { get; set; }


        public string pageId { get; set; }
    }

    public class ExistingPage
    {
        public string id { get; set; }
        public long orderNumber { get; set; }
        public string pageType { get; set; }
        public string originalPageName { get; set; }
    }

    public class RevField
    {
        public string displayName { get; set; }
        public string fieldType { get; set; }
        public string[] userlistValues { get; set; }
    }

    public class RevDoc
    {
        public string pvnDocId { get; set; }
        public string id { get; set; }
        public Dictionary<string, string> indexes { get; set; }

        public ExistingPage[] pages { get; set; }
    }

}
