// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System;
using System.Diagnostics;
using System.Reflection;

namespace PdfSharp.TestHelper
{
    public static class TestHelperExtensions
    {
        public static string[] Splitter(this string text, string sep)
        {
            var result = new List<string>();
            while (true)
            {
                int idx = text.IndexOf(sep, StringComparison.Ordinal);
                if (idx < 0)
                {
                    result.Add(text);
                    return result.ToArray();
                }

                result.Add(text.Substring(0, idx));
                text = text.Substring(idx + sep.Length);
            }
        }

        public static string GetOriginalLocation(this Assembly assembly)
        {
            var dllPath = assembly.Location;
            return dllPath;
        }

    }
}
