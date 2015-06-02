using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ProFileCounter
{
    public partial class MatchingFilesReportWindow : Form
    {
        public MatchingFilesReportWindow()
        {
            InitializeComponent();
        }

        public MatchingFilesReportWindow(string report)
        {
            InitializeComponent();
            textBox1.Text = report;
        }
    }
}
