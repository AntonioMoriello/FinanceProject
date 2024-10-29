using FinanceManager.Services;
using FinanceManager.Data;
using FinanceManager.Settings;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using DinkToPdf;
using DinkToPdf.Contracts;
using FinanceManager.Models;
using System.Runtime.InteropServices;


namespace FinanceManager
{
    using System.Runtime.InteropServices;

    namespace FinanceManager
    {
        public class CustomAssemblyLoadContext : System.Runtime.Loader.AssemblyLoadContext
        {
            public IntPtr LoadUnmanagedLibrary(string libraryPath)
            {
                return NativeLibrary.Load(libraryPath);
            }
        }
    }

}