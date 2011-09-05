using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;


namespace Linnarsson.Utilities
{
	public class SimpleXmlSerializer
	{

		public static void ToXmlFile(string path, object obj)
		{
			ToXmlFile(path, obj, new Type[] { });
		}

		public static void ToXmlFile(string path, object obj, Type[] extraTypes)
		{
			XmlSerializer xs = new XmlSerializer(obj.GetType(), extraTypes);
			System.IO.StreamWriter sw = new System.IO.StreamWriter(path);
			xs.Serialize(sw, obj);
			sw.Close();
		}

		public static T FromXml<T>(string xml)
		{
			return FromXml<T>(xml, new Type[] { });
		}

		public static T FromXml<T>(string xml, Type[] extraTypes)
		{
			XmlSerializer xs = new XmlSerializer(typeof(T), extraTypes);
			System.IO.StringReader sr = new System.IO.StringReader(xml);
			return (T)xs.Deserialize(sr);
		}

		public static T FromXmlFile<T>(string path)
		{
			return FromXmlFile<T>(path, new Type[] { });
		}

		public static T FromXmlFile<T>(string path, Type[] extraTypes)
		{
			XmlSerializer xs = new XmlSerializer(typeof(T), extraTypes);
			System.IO.StreamReader sr = new System.IO.StreamReader(path);
			T result = (T)xs.Deserialize(sr); 
			sr.Close();
			return result;
		}

		public static string ToXml(object obj)
		{
			XmlSerializer xs = new XmlSerializer(obj.GetType());
			System.IO.MemoryStream ms = new System.IO.MemoryStream();
			xs.Serialize(ms, obj);
			return ms.ToString();
		}

	}
}
