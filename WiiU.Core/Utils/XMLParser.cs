using System;
using System.Xml;

namespace NUSPacker.Utils
{
    public class XMLParser
    {
        private XmlDocument? document;

        public void LoadDocument(string path)
        {
            var doc = new XmlDocument();
            doc.Load(path);
            document = doc;
        }

        public void LoadDocument(System.IO.Stream stream)
        {
            var doc = new XmlDocument();
            doc.Load(stream);
            document = doc;
        }

        public AppXMLInfo GetAppXMLInfo()
        {
            var appxmlinfo = new AppXMLInfo();
            appxmlinfo.SetOSVersion(GetValueOfElementAsLongHex("os_version", 0));
            appxmlinfo.SetTitleID(GetValueOfElementAsLongHex("title_id", 0));
            appxmlinfo.SetTitleVersion((short)GetValueOfElementAsLongHex("title_version", 0));
            appxmlinfo.SetSDKVersion((int)GetValueOfElementAsInt("sdk_version", 0));
            appxmlinfo.SetAppType((int)GetValueOfElementAsLongHex("app_type", 0));
            appxmlinfo.SetGroupID((short)GetValueOfElementAsLongHex("group_id", 0));
            appxmlinfo.SetOSMask(GetValueOfElementAsByteArray("os_mask", 0));
            appxmlinfo.SetCommon_id(GetValueOfElementAsLongHex("common_id", 0));
            return appxmlinfo;
        }

        public long GetValueOfElementAsInt(string element, int index)
        {
            return long.Parse(GetValueOfElement(element, index));
        }

        public long GetValueOfElementAsLong(string element, int index)
        {
            return long.Parse(GetValueOfElement(element, index));
        }

        public long GetValueOfElementAsLongHex(string element, int index)
        {
            return Utils.HexStringToLong(GetValueOfElement(element, index));
        }

        public byte[] GetValueOfElementAsByteArray(string element, int index)
        {
            return Utils.HexStringToByteArray(GetValueOfElement(element, index));
        }

        public string GetValueOfElement(string element)
        {
            return GetValueOfElement(element, 0);
        }

        public string GetValueOfElement(string element, int index)
        {
            if (document == null)
            {
                Console.WriteLine("Please load the document first.");
                return "";
            }
            XmlNodeList list = document.GetElementsByTagName(element);
            if (list == null || index >= list.Count)
            {
                return "";
            }
            XmlNode? node = list.Item(index);
            if (node == null)
            {
                return "";
            }
            return node.InnerText;
        }
    }
}
