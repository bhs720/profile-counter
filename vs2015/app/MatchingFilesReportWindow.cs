using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace TIFPDFCounter
{
    public partial class MatchingFilesReportWindow : Form
    {
        IEnumerable<IGrouping<string, TPCFile>> matchingFiles;

        public MatchingFilesReportWindow()
        {
            InitializeComponent();
        }

        public MatchingFilesReportWindow(IEnumerable<IGrouping<string,TPCFile>> matchingFiles)
        {
            InitializeComponent();
            this.matchingFiles = matchingFiles;
        }

        private void MatchingFilesReportWindow_Load(object sender, EventArgs e)
        {
            foreach (var hash in matchingFiles)
            {
                var root = treeView1.Nodes.Add(hash.Key);
                foreach (var file in hash)
                {
                    var child = new TreeNode(file.Filename);
                    child.Tag = file;
                    root.Nodes.Add(child);
                }
            }
        }

        private void btnOpenFolder_Click(object sender, EventArgs e)
        {
            var node = treeView1.SelectedNode;
            if (node != null)
            {
                var file = node.Tag as TPCFile;
                if (file != null)
                {
                    System.Diagnostics.Process.Start("explorer.exe", "/select, \"" + file.Filename + "\"");
                }
            }
        }

        private void treeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node != null)
            {
                var file = e.Node.Tag as TPCFile;
                if (file != null)
                {
                    System.Diagnostics.Process.Start(file.Filename);
                }
            }
        }
    }
}
