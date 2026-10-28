using System.Diagnostics;
using System.Xml;
using WiiU.WUP.Models;

namespace WiiU.WUP.Services
{
    public class XMLParser
    {
        private XmlDocument? document;

        public void LoadDocument(Stream stream)
        {
            var doc = new XmlDocument();

            doc.Load(stream);

            document = doc;
        }

        public AppXMLInfo GetAppXMLInfo()
        {
            var appxmlinfo = new AppXMLInfo
            {
                OSVersion = GetValueOfElementAsLongHex("os_version", 0),
                TitleID = GetValueOfElementAsLongHex("title_id", 0),
                TitleVersion = (short)GetValueOfElementAsLongHex("title_version", 0),
                SDKVersion = (int)GetValueOfElementAsInt("sdk_version", 0),
                AppType = (int)GetValueOfElementAsLongHex("app_type", 0),
                GroupID = (short)GetValueOfElementAsLongHex("group_id", 0),
                OSMask = GetValueOfElementAsByteArray("os_mask", 0),
                CommonId = GetValueOfElementAsLongHex("common_id", 0)
            };

            return appxmlinfo;
        }

        public long GetValueOfElementAsInt(string element, int index) => long.Parse(GetValueOfElement(element, index));

        public long GetValueOfElementAsLongHex(string element, int index) => Utils.HexStringToLong(GetValueOfElement(element, index));

        public byte[] GetValueOfElementAsByteArray(string element, int index) => Utils.HexStringToByteArray(GetValueOfElement(element, index));

        public string GetValueOfElement(string element, int index)
        {
            if (document == null)
            {
                Debug.WriteLine("Please load the document first.");
                return string.Empty;
            }

            XmlNodeList list = document.GetElementsByTagName(element);

            if (list == null || index >= list.Count)
                return string.Empty;

            XmlNode? node = list.Item(index);

            if (node == null)
                return string.Empty;

            return node.InnerText;
        }
    }
}