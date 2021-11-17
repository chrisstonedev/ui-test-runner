using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using UiTestRunner.TestFramework;

namespace UiTestRunner.TestRunner
{
    public partial class Form1 : Form
    {
        private readonly Dictionary<(string, string), string> errorText = new();
        private string actualImageFile;
        private string expectedImageFile;

        public Form1()
        {
            InitializeComponent();
        }

        private async void RunClientTestButton_Click(object sender, EventArgs e)
        {
            var thisAssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var assemblyPath = Path.Join(thisAssemblyDirectory, @"..\..\..\..\..", @"technical-debt\OrderCore.Client.UiTests\bin\Debug\net6.0-windows\OrderCore.Client.UiTests.dll");

            var classes = Assembly.LoadFile(assemblyPath).GetTypes().ToArray();

            foreach (var type in classes)
            {
                foreach (var method in type.GetMethods().Where(MethodHasTestRunAttribute))
                {
                    try
                    {
                        await (method.Invoke(null, null) as Task);
                        SetTestStatus(type.Name, method.Name, true, DateTime.Now.ToString(CultureInfo.InvariantCulture));
                    }
                    catch (Exception ex)
                    {
                        SetTestStatus(type.Name, method.Name, false, ex.ToString());
                    }
                }
            }
        }

        private bool MethodHasTestRunAttribute(MethodInfo method)
        {
            return method.GetCustomAttribute(typeof(TestRunAttribute), false) is not null;
        }

        private void SetTestStatus(string className, string testName, bool wasSuccessful, string data)
        {
            var status = wasSuccessful ? "Success" : "Failed";
            errorText[(className, testName)] = data;
            foreach (DataGridViewRow row in testResultsDataGridView.Rows)
            {
                if (row.Cells[0].Value.ToString().Equals(className, StringComparison.Ordinal)
                    && row.Cells[1].Value.ToString().Equals(testName, StringComparison.Ordinal))
                {
                    row.Cells[2].Value = status;
                    return;
                }
            }
            _ = testResultsDataGridView.Rows.Add(className, testName, status, data);
        }

        private void CloseProgramButton_Click(object sender, EventArgs e)
        {
            TestClass.CloseWindow(windowTitleTextBox.Text);
        }

        private void CheckProgramIsOpenButton_Click(object sender, EventArgs e)
        {
            try
            {
                _ = TestClass.GetWindowHandle(windowTitleTextBox.Text);
                _ = MessageBox.Show("The program is open! Tests are ready to run.", "TestWinAPI", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (AssertException)
            {
                if (MessageBox.Show("Couldn't find the program. Do you want to start it?", "TestWinAPI", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    var process = Process.Start(@"..\..\..\..\OrderCore.Client\bin\Debug\net5.0-windows\OrderCore.Client.exe");
                    windowControlTextBox.Text = ((int)process.Handle).ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        private void ListWindowControlsButton_Click(object sender, EventArgs e)
        {
            dataGridView.Rows.Clear();

            try
            {
                var hwnd = windowControlTextBox.TextLength > 0
                    ? int.Parse(windowControlTextBox.Text, CultureInfo.InvariantCulture)
                    : TestClass.GetWindowHandle(windowTitleTextBox.Text);
                var children = LegacyMethods.ListAllChildren((IntPtr)hwnd);

                foreach (var something in children)
                {
                    _ = dataGridView.Rows.Add(something.WindowHandle, something.ClassName, something.Parent);
                }
            }
            catch (AssertException)
            {
                _ = MessageBox.Show("Couldn't find the program.", "TestWinAPI", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        private void TestResultsDataGridView_SelectionChanged(object sender, EventArgs e)
        {
            if (testResultsDataGridView.SelectedRows.Count > 0)
            {
                var selectedRow = testResultsDataGridView.SelectedRows[0];
                var className = selectedRow.Cells[0].Value.ToString();
                var testName = selectedRow.Cells[1].Value.ToString();
                errorDetailsTextBox.Text = errorText[(className, testName)];

                string executingAssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string directoryPath = Path.Join(executingAssemblyDirectory, @"..\..\..\..\..\technical-debt\OrderCore.Client.UiTests\Approval");
                const string FILE_FORMAT = "{0}_{1}_*_{2}.bmp";
                string actualFileNamePattern = string.Format(FILE_FORMAT, className, testName, "actual");
                try
                {
                    var files = Directory.GetFiles(directoryPath, actualFileNamePattern, SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                    {
                        actualImageFile = files.First();
                        expectedImageFile = actualImageFile.Replace("_actual.bmp", "_expected.bmp");

                        expectedPictureBox.Image = ImageFromFile(expectedImageFile);
                        actualPictureBox.Image = ImageFromFile(actualImageFile);
                    }
                    else
                    {
                        expectedPictureBox.Image = null;
                        actualPictureBox.Image = null;
                    }
                }
                catch (DirectoryNotFoundException)
                {
                    expectedPictureBox.Image = null;
                    actualPictureBox.Image = null;
                }
            }
            else
            {
                errorDetailsTextBox.Text = string.Empty;
                expectedPictureBox.Image = null;
                actualPictureBox.Image = null;
            }
        }

        private static Image ImageFromFile(string path)
        {
            var bytes = File.ReadAllBytes(path);
            using var ms = new MemoryStream(bytes);
            var img = Image.FromStream(ms);
            return img;
        }

        private void ApproveNewChangeButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to replace the current expected image with the new actual image?", "UI Approval Test Runner", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                File.Move(actualImageFile, expectedImageFile, true);
            }
        }
    }
}
