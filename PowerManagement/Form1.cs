using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PowerManagement
{
    public partial class MainWindow : Form
    {
        [DllImport("PowrProf.dll")]
        public static extern UInt32 PowerEnumerate(IntPtr RootPowerKey, IntPtr SchemeGuid, IntPtr SubGroupOfPowerSettingGuid, UInt32 AcessFlags, UInt32 Index, ref Guid Buffer, ref UInt32 BufferSize);

        [DllImport("PowrProf.dll")]
        public static extern UInt32 PowerReadFriendlyName(IntPtr RootPowerKey, ref Guid SchemeGuid, IntPtr SubGroupOfPowerSettingGuid, IntPtr PowerSettingGuid, IntPtr Buffer, ref UInt32 BufferSize);

        [DllImportAttribute("powrprof.dll", EntryPoint = "PowerSetActiveScheme")]
        public static extern uint PowerSetActiveScheme(IntPtr UserPowerKey, ref Guid ActivePolicyGuid);

        [DllImportAttribute("powrprof.dll", EntryPoint = "PowerGetActiveScheme")]
        public static extern uint PowerGetActiveScheme(IntPtr UserPowerKey, out IntPtr ActivePolicyGuid);
        
        int selected = 0;
        IEnumerable<Guid> powerPlans;
        ArrayList pow;
        public MainWindow()
        {
            InitializeComponent();
            powerPlans = GetAll();
            CurrentPlan.Text = "Current Plan: " + ReadFriendlyName(GetActiveGuid());
            pow = new ArrayList();
            foreach (Guid item in powerPlans) {
                string planString = ReadFriendlyName(item);
                ToolStripMenuItem newMenuItem = new ToolStripMenuItem();
                newMenuItem.Name = planString+"ToolStripMenu";
                newMenuItem.Text = planString;
                contextMenuNotifyIcon.Items.Insert(2,newMenuItem);
                comboBoxPower.Items.Add(planString);
                pow.Add(item);
            }
            comboBoxPower.SelectedIndex = 0;

            if (!System.IO.Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\PowerManagement"))
            {
                System.IO.Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\PowerManagement");
            }

            if (!System.IO.File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\PowerManagement\\settings.ini"))
            {
                string[] lines = { "StartInSystemTray", "0" };
                System.IO.File.WriteAllLines(@"" + Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\PowerManagement\\settings.ini", lines);
            }
        }

        private void formShown_(object sender, EventArgs e)
        {
            String[] lines = System.IO.File.ReadAllLines(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\PowerManagement\\settings.ini");
            if (lines[1] == "0")
            {
                startSystemTrayCheckbox.Checked = false;
            }
            else if (lines[1] == "1")
            {
                startSystemTrayCheckbox.Checked = true;
                this.Hide();
            }
        }

        public enum AccessFlags : uint
        {
            ACCESS_SCHEME = 16,
            ACCESS_SUBGROUP = 17,
            ACCESS_INDIVIDUAL_SETTING = 18
        }
        /*
         Obtiene todos los planes de energía
        */
        public static IEnumerable<Guid> GetAll()
        {
            var schemeGuid = Guid.Empty;

            uint sizeSchemeGuid = (uint)Marshal.SizeOf(typeof(Guid));
            uint schemeIndex = 0;

            while (PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, (uint)AccessFlags.ACCESS_SCHEME, schemeIndex, ref schemeGuid, ref sizeSchemeGuid) == 0)
            {
                yield return schemeGuid;
                schemeIndex++;
            }
        }
        /*
        Obtiene el nombre de un plan de energía dado en el equipo. 
        */
        private static string ReadFriendlyName(Guid schemeGuid)
        {
            uint sizeName = 1024;
            IntPtr pSizeName = Marshal.AllocHGlobal((int)sizeName);

            string friendlyName;

            try
            {
                PowerReadFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, pSizeName, ref sizeName);
                friendlyName = Marshal.PtrToStringUni(pSizeName);
            }
            finally
            {
                Marshal.FreeHGlobal(pSizeName);
            }

            return friendlyName;
        }

       
        /*
        Devuelve el plan de energía usado actualmente 
        */
        private Guid GetActiveGuid()
        {
            Guid ActiveScheme = Guid.Empty;
            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(IntPtr)));
            if (PowerGetActiveScheme((IntPtr)null, out ptr) == 0)
            {
                ActiveScheme = (Guid)Marshal.PtrToStructure(ptr, typeof(Guid));
                if (ptr != null)
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            return ActiveScheme;
        }
        
        /*
        Acción de botón de salida. 
        */
        private void button2_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void comboBoxPower_SelectedIndexChanged(object sender, EventArgs e)
        {
            selected = comboBoxPower.SelectedIndex;
        }

        /*
        Acción del botón de aplicar el plan de energía 
        */
        private void ApplyButton_Click(object sender, EventArgs e)
        {
            int ind = comboBoxPower.SelectedIndex;
            Guid sel = (Guid) pow[ind];
            PowerSetActiveScheme(IntPtr.Zero, ref sel);
            CurrentPlan.Text = "Current Plan: " + ReadFriendlyName(GetActiveGuid());
            notifyIcon1.BalloonTipText = CurrentPlan.Text;
            notifyIcon1.ShowBalloonTip(1);
        }

        /*
        Cambia el plan de energia desde la bandeja de notificaciones 
        */
        private void contextMenuNotifyIcon_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            string name = e.ClickedItem.Name;
            //Elimina ToolStripMenu del nombre del plan de energia del menu de contexto
            name = name.Remove(name.Length-13, 13);

            foreach (Guid i in pow)
            {
                if (ReadFriendlyName(i) == name)
                {
                    Guid sel = (Guid)i;
                    PowerSetActiveScheme(IntPtr.Zero, ref sel);
                    CurrentPlan.Text = "Current Plan: " + ReadFriendlyName(GetActiveGuid());
                }
            }
            if (e.ClickedItem.Name != "openToolStripMenuItem" && e.ClickedItem.Name != "quitToolStripMenuItem2")
            {
                notifyIcon1.BalloonTipText = CurrentPlan.Text;
                notifyIcon1.ShowBalloonTip(1);
            }
        } 


        /*
        Acción de salir en archivo 
        */
        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        /*
        Ventana de acerca de... 
        */
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("PowerManagement 2.0 \nby elssbbboy", "About", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
        }

        private void quitToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
        }      
        

        private void Hide_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void startSystemTrayCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            string settingsPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)+"\\PowerManagement";
            string[] lines = { "StartInSystemTray", "1" };
            if (startSystemTrayCheckbox.Checked)
            {
                System.IO.File.WriteAllLines(@"" + settingsPath + "\\settings.ini", lines);
            }
            else
            {
                lines[1] = "0";
                System.IO.File.WriteAllLines(@"" + settingsPath + "\\settings.ini", lines);
            }
        }
    }
}
