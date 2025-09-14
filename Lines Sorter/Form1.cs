using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace Lines_Sorter
{
    public partial class Form1 : Form
    {
        private System.Threading.CancellationTokenSource _cancellationTokenSource;
        public Form1()
        {
            InitializeComponent();
            this.listBox1.AllowDrop = true;
        }

        private void listBox1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void listBox1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            // Add each file path to the list box.
            foreach (string file in files)
            {
                listBox1.Items.Add(file);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            // Allow the user to select multiple files
            openFileDialog.Multiselect = true;
            // Set the filter to show all file types
            openFileDialog.Filter = "All files (*.*)|*.*";

            // Show the dialog and if the user clicks OK, add the selected file paths to the list box
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                foreach (string file in openFileDialog.FileNames)
                {
                    listBox1.Items.Add(file);
                }
            }
        }
        private void button3_Click(object sender, EventArgs e)
        {
            // Check if any items are selected in the list box.
            if (listBox1.SelectedItems.Count > 0)
            {
                // Create a temporary list to hold the items to be removed to avoid issues with modifying the collection while iterating over it.
                var selectedItems = new System.Collections.ArrayList(listBox1.SelectedItems);
                // Remove each selected item from the list box.
                foreach (var item in selectedItems)
                {
                    listBox1.Items.Remove(item);
                }
            }
            else
            {
                // If no item is selected, show a message to the user.
                MessageBox.Show("Please select at least one file to remove.", "No File Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
            // Check if there are any file paths listed in the ListBox.
private async void button2_Click(object sender, EventArgs e)
        {
            // Initial checks and search term preparation
            if (listBox1.Items.Count == 0)
            {
                MessageBox.Show("Please add file paths to the list box.", "No Files Listed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string searchText = textBox1.Text;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                MessageBox.Show("Please enter the text to search for.", "Search Text Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string[] searchTerms = searchText.Split('|')
                                             .Select(term => term.Trim())
                                             .Where(term => !string.IsNullOrEmpty(term))
                                             .ToArray();
            if (searchTerms.Length == 0)
            {
                MessageBox.Show("The search text is invalid.", "Invalid Search Text", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var comparisonMode = checkBox1.Checked
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;
            bool isExcludeMode = checkBox2.Checked;
            // Initialization and UI setup
            _cancellationTokenSource = new System.Threading.CancellationTokenSource();
            var token = _cancellationTokenSource.Token;
            int totalFiles = listBox1.Items.Count;
            int processedFiles = 0;
            var linesToKeep = new System.Collections.Generic.List<string>();
            long foundLinesCount = 0;
            SetUiStateForProcessing(true);
            progressBar1.Value = 0;
            lblFileCount.Text = "Starting...";
            lblStatus.Text = "";
            label3.Text = "";
            label4.Text = "";
            label5.Text = "";

            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    foreach (var item in listBox1.Items)
                    {
                        if (token.IsCancellationRequested) break;

                        processedFiles++;
                        string filePath = item.ToString();
                        string fileName = System.IO.Path.GetFileName(filePath);
                        string displayName = fileName.Length > 20 ? fileName.Substring(0, 20) + "..." : fileName;

                        this.Invoke((System.Action)delegate {
                            lblFileCount.Text = $"Processing file {processedFiles} of {totalFiles}";
                            progressBar1.Value = 0;
                        });

                        try
                        {
                            if (!System.IO.File.Exists(filePath))
                            {
                                this.Invoke((System.Action)delegate { lblStatus.Text = $"File not found: {displayName}"; });
                                continue;
                            }

                            var fileInfo = new System.IO.FileInfo(filePath);
                            long totalBytes = fileInfo.Length;

                            using (var fs = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                            using (var sr = new System.IO.StreamReader(fs))
                            {
                                string line;
                                long linesProcessedThisFile = 0;
                                long lastUpdateLineCount = 0;
                                var lastUpdateTime = DateTime.UtcNow;
                                const double updateIntervalSeconds = 1.0;
                                long estimatedTotalLines = 0;

                                while ((line = sr.ReadLine()) != null)
                                {
                                    if (token.IsCancellationRequested) break;
                                    linesProcessedThisFile++;


                                    bool matchFound = false;
                                    foreach (var term in searchTerms)
                                    {
                                        if (line.IndexOf(term, comparisonMode) >= 0)
                                        {
                                            matchFound = true;
                                            break;
                                        }
                                    }
                               
                                    // Include mode (false): keep if match (true) -> false != true -> true
                                    // Exclude mode (true): keep if no match (false) -> true != false -> true
                                    if (isExcludeMode != matchFound)
                                    {
                                        foundLinesCount++;
                                        linesToKeep.Add(line);
                                    }


                                    var elapsedSeconds = (DateTime.UtcNow - lastUpdateTime).TotalSeconds;
                                    if (elapsedSeconds >= updateIntervalSeconds)
                                    {
                                        // Continuously update the line estimation
                                        if (totalBytes > 0)
                                        {
                                            double percentRead = (double)fs.Position / totalBytes;
                                            if (percentRead > 0.001) // Avoid wild estimates at the very beginning
                                            {
                                                estimatedTotalLines = (long)(linesProcessedThisFile / percentRead);
                                            }
                                        }

                                        var linesSinceLastUpdate = linesProcessedThisFile - lastUpdateLineCount;
                                        var linesPerSecond = linesSinceLastUpdate / elapsedSeconds;
                                        int percentage = (totalBytes > 0) ? (int)((double)fs.Position * 100 / totalBytes) : 0;

                                        string lineCountText = (estimatedTotalLines > 0)
                                            ? $"Line: {linesProcessedThisFile:N0} of ~{estimatedTotalLines:N0}"
                                            : $"Line: {linesProcessedThisFile:N0}";

                                        this.Invoke((System.Action)delegate {
                                            progressBar1.Value = percentage;
                                            lblStatus.Text = $"Processing: {displayName}... {percentage}%";
                                            label3.Text = lineCountText;
                                            label4.Text = $"Speed: {linesPerSecond:N0} lines/sec";
                                            label5.Text = $"Found: {foundLinesCount:N0}";
                                        });

                                        lastUpdateTime = DateTime.UtcNow;
                                        lastUpdateLineCount = linesProcessedThisFile;
                                    }
                                }

                                // Shows true line count
                                this.Invoke((System.Action)delegate {
                                    progressBar1.Value = 100;
                                    lblStatus.Text = $"Finished: {displayName}";
                                    label3.Text = $"Line: {linesProcessedThisFile:N0} of {linesProcessedThisFile:N0}";
                                    label4.Text = "";
                                    label5.Text = $"Found: {foundLinesCount:N0}";
                                });
                            }
                        }
                        catch (System.Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"An error occurred processing {filePath}: {ex.Message}");
                            this.Invoke((System.Action)delegate { lblStatus.Text = $"Error on: {displayName}"; });
                        }
                    }
                }, token);
            }
            finally
            {
                SetUiStateForProcessing(false);
                _cancellationTokenSource.Dispose();
            }
            label5.Text = $"Found: {foundLinesCount:N0}";

            // --- Post-Processing Logic ---
            if (token.IsCancellationRequested)
            {
                lblStatus.Text = "Operation Cancelled.";
                if (linesToKeep.Count > 0)
                {
                    var result = MessageBox.Show(
                        "The process was cancelled. Do you want to save the results found so far?",
                        "Save Partial Results?",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        SaveResults(linesToKeep);
                    }
                }
            }
            else // Process completed normally
            {
                lblFileCount.Text = $"Processed {processedFiles} of {totalFiles} files.";
                lblStatus.Text = "Processing complete.";
                progressBar1.Value = 100;

                if (linesToKeep.Count > 0)
                {
                    SaveResults(linesToKeep);
                }
                else
                {
                    MessageBox.Show("No lines containing the specified text were found.", "No Matches Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        private void SetUiStateForProcessing(bool isProcessing)
        {
            button2.Enabled = !isProcessing;
            button4.Enabled = isProcessing;
            listBox1.Enabled = !isProcessing;
            textBox1.Enabled = !isProcessing;
            checkBox1.Enabled = !isProcessing;
            checkBox2.Enabled = !isProcessing;
        }

        // Helper method to handle saving the file, avoiding code duplication.
        private void SaveResults(System.Collections.Generic.List<string> lines)
        {
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Text Files (*.txt)|*.txt|All files (*.*)|*.*";
                saveFileDialog.Title = "Save the processed file";
                saveFileDialog.FileName = "filtered_output.txt";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        System.IO.File.WriteAllLines(saveFileDialog.FileName, lines);
                        MessageBox.Show("File saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        lblStatus.Text = "File saved successfully.";
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show($"An error occurred while saving the file:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        lblStatus.Text = "Error saving file.";
                    }
                }
                else
                {
                    lblStatus.Text = "Save operation cancelled.";
                }
            }
        }

        private void saveFileDialog1_FileOk(object sender, CancelEventArgs e)
        {

        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                button4.Enabled = false; // Disable the button to prevent multiple clicks.
            }
        }

        private void lblStatus_Click(object sender, EventArgs e)
        {

        }
    }
}
