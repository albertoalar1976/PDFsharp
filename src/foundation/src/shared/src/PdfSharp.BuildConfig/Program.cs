// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using Console = System.Console;

namespace PdfSharp.Internal
{
    internal class Program
    {
        // We completely eliminate GitVersion
        /// <summary>
        /// Why so cumbersome?
        /// We want to create the GitVersion information during a pre-build step only once,
        /// not more than 100 times for every single project and every target framework.
        /// Executing
        ///   dotnet gitversion > GitVersion.json
        /// in PowerShell would work fine, but requires GitVersion.Tool to be installed
        /// as a global dotnet tool. We don’t want to force every user to install this
        /// just to compile PDFsharp. So we create the GitVersion.json with this dummy
        /// project and execute it during the pre-build step.
        /// In this project GitVersion comes with a NuGet package.
        /// </summary>
        static void Main(string[] args)
        {
            Console.WriteLine("This program does nothing anymore.");
            return;
        }
    }
}
