/*
 * Created by SharpDevelop.
 * User: bsmith
 * Date: 5/19/2014
 * Time: 3:46 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Windows.Forms;

namespace TIFPDFCounter
{
	/// <summary>
	/// Class with program entry point.
	/// </summary>
	internal sealed class Program
	{
		/// <summary>
		/// Program entry point.
		/// </summary>
		[STAThread]
		private static void Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			// Analyzer callbacks run on thread pool threads. An exception there is not
			// routed through Application.ThreadException -- it terminates the process, and
			// the user loses the whole batch with no message. Several classes in this
			// codebase reason carefully about not throwing on those threads; this is the
			// backstop for the case one of them is wrong.
			AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
			{
				try
				{
					MessageBox.Show(
						"ProFile Counter hit an unexpected error and has to close.\n\n" + e.ExceptionObject,
						"Unexpected error",
						MessageBoxButtons.OK,
						MessageBoxIcon.Error);
				}
				catch { /* nothing useful left to do at this point */ }
			};

			Application.Run(new MainForm());
		}
		
	}
}
