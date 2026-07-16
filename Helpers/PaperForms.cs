using System.Runtime.InteropServices;

namespace eCheque.MICO360.Helpers
{
    /// <summary>
    /// Registers the cheque's dimensions as a Windows custom paper form ("eCheque 190x85 mm") via the
    /// spooler API. Print drivers only offer page sizes they know about; once the form exists, capable
    /// drivers list it in the print dialog and honour it when the app requests the cheque size — the fix
    /// for "there is no option to select the correct page size".
    /// </summary>
    public static class PaperForms
    {
        public static string FormNameFor(double wMm, double hMm) => $"eCheque {wMm:0}x{hMm:0} mm";

        /// <summary>Creates the form if missing. Returns (ok, message). Access denied means the user must
        /// run the app elevated once — forms are machine-wide and protected by Windows.</summary>
        public static (bool Ok, string Message) EnsureChequeForm(string printerName, double wMm, double hMm)
        {
            if (string.IsNullOrWhiteSpace(printerName))
                return (false, "Choose a printer first (Printer… button).");
            var formName = FormNameFor(wMm, hMm);
            if (!OpenPrinter(printerName, out var h, IntPtr.Zero))
                return (false, $"Could not open printer \"{printerName}\" (error {Marshal.GetLastWin32Error()}).");
            try
            {
                var info = new FORM_INFO_1
                {
                    Flags = 0,
                    pName = formName,
                    Size = new SIZEL { cx = (int)Math.Round(wMm * 1000), cy = (int)Math.Round(hMm * 1000) },       // thousandths of mm
                    ImageableArea = new RECTL { left = 0, top = 0, right = (int)Math.Round(wMm * 1000), bottom = (int)Math.Round(hMm * 1000) }
                };
                if (AddForm(h, 1, ref info)) return (true, $"Paper size \"{formName}\" created.");
                int err = Marshal.GetLastWin32Error();
                if (err == 80 || err == 183) return (true, $"Paper size \"{formName}\" already exists.");
                if (err == 5) return (false, "Windows denied access — close the app, run it once as administrator, and press this button again.");
                return (false, $"Could not create the paper size (Windows error {err}).");
            }
            finally { ClosePrinter(h); }
        }

        [StructLayout(LayoutKind.Sequential)] struct SIZEL { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential)] struct RECTL { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct FORM_INFO_1
        {
            public uint Flags;
            [MarshalAs(UnmanagedType.LPWStr)] public string pName;
            public SIZEL Size;
            public RECTL ImageableArea;
        }

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "AddFormW")]
        static extern bool AddForm(IntPtr hPrinter, uint level, ref FORM_INFO_1 pForm);

        [DllImport("winspool.drv", SetLastError = true)]
        static extern bool ClosePrinter(IntPtr hPrinter);
    }
}
