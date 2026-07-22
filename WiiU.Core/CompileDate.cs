using System;
using System.IO;
using System.Reflection;

namespace NUSPacker
{
    public class CompileDate
    {
        public void PrintDate()
        {
            try
            {
                string? path = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }
                DateTime manifestTime = File.GetLastWriteTime(path);
                Console.Write(" - " + manifestTime.ToString());
            }
            catch (IOException)
            {
            }
        }
    }
}
